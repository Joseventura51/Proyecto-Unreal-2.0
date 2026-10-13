using System.Collections.Concurrent;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Android.Provider;
using Java.Util;
using Microsoft.Maui.ApplicationModel;
using SenatiMapMaui.Models;
using SenatiMapMaui.Services;

namespace SenatiMapMaui.Platforms.Android;

public sealed class AndroidBleScannerService : IBleScannerService
{
    private static readonly ParcelUuid EddystoneUuid =
        ParcelUuid.FromString("0000feaa-0000-1000-8000-00805f9b34fb")!;

    private readonly ConcurrentDictionary<string, Queue<int>> _rssiWindows = new();
    private readonly ConcurrentDictionary<string, double> _filteredRssi = new();
    private readonly ConcurrentDictionary<string, double> _filteredDistance = new();

    private BluetoothLeScanner? _scanner;
    private InternalScanCallback? _callback;
    private CancellationTokenSource? _scanCts;

    public event EventHandler<BeaconReading>? BeaconFound;
    public event EventHandler<ScanState>? ScanStateChanged;

    public bool IsScanning { get; private set; }

    public async Task StartScanAsync()
    {
        var permission = await Permissions.RequestAsync<BluetoothRuntimePermission>();
        if (permission != PermissionStatus.Granted)
        {
            ScanStateChanged?.Invoke(this, new(false, "Permisos Bluetooth denegados"));
            return;
        }

        var context = global::Android.App.Application.Context;
        var manager = (BluetoothManager?)context.GetSystemService(Context.BluetoothService);
        var adapter = manager?.Adapter;

        if (adapter is null)
        {
            ScanStateChanged?.Invoke(this, new(false, "Este teléfono no tiene Bluetooth"));
            return;
        }

        if (!adapter.IsEnabled)
        {
            ScanStateChanged?.Invoke(this, new(false, "Bluetooth está apagado"));
            return;
        }

        StopScan("Reiniciando escaneo");
        _rssiWindows.Clear();
        _filteredRssi.Clear();
        _filteredDistance.Clear();

        _scanner = adapter.BluetoothLeScanner;
        if (_scanner is null)
        {
            ScanStateChanged?.Invoke(this, new(false, "No se pudo iniciar el escáner BLE"));
            return;
        }

        _callback = new InternalScanCallback(this);
        var settings = new ScanSettings.Builder()
            .SetScanMode(ScanMode.LowLatency)
            .Build();

        _scanner.StartScan(null, settings, _callback);

        IsScanning = true;
        ScanStateChanged?.Invoke(this, new(true, "Buscando anuncios BLE durante 15 segundos…"));

        _scanCts = new CancellationTokenSource();
        _ = AutoStopAsync(_scanCts.Token);
    }

    private async Task AutoStopAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), token);
            if (!token.IsCancellationRequested)
                StopScan("Escaneo completado");
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void StopScan(string message = "Escaneo detenido")
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;

        if (IsScanning && _scanner is not null && _callback is not null)
        {
            try { _scanner.StopScan(_callback); } catch { }
        }

        IsScanning = false;
        ScanStateChanged?.Invoke(this, new(false, message));
    }

    public void OpenBluetoothSettings()
    {
        var intent = new Intent(Settings.ActionBluetoothSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        global::Android.App.Application.Context.StartActivity(intent);
    }

    private void Process(ScanResult result)
    {
        try
        {
            var record = result.ScanRecord;
            var name = record?.DeviceName;

            if (string.IsNullOrWhiteSpace(name))
            {
                try { name = result.Device?.Name; } catch { }
            }

            if (string.IsNullOrWhiteSpace(name))
                name = "Dispositivo BLE sin nombre";

            var address = result.Device?.Address ?? "Sin MAC";
            var rawRssi = result.Rssi;
            var rssi = SmoothRssi(address, rawRssi);

            var protocol = "BLE";
            var isBeacon = false;
            double? measuredAtOneMeter = null;
            var beaconId = "";

            if (record is not null)
            {
                var apple = record.GetManufacturerSpecificData(0x004C);
                if (apple is { Length: >= 23 } && apple[0] == 0x02 && apple[1] == 0x15)
                {
                    protocol = "iBeacon";
                    isBeacon = true;

                    var measuredPower = unchecked((sbyte)apple[22]);
                    if (ValidCalibration(measuredPower))
                        measuredAtOneMeter = measuredPower;

                    var major = (apple[18] << 8) | apple[19];
                    var minor = (apple[20] << 8) | apple[21];
                    beaconId = $"{FormatUuid(apple, 2)} · {major}/{minor}";
                }
            }

            if (record is not null && !isBeacon)
            {
                var eddystone = record.GetServiceData(EddystoneUuid);
                if (eddystone is { Length: >= 2 })
                {
                    var frameType = eddystone[0];
                    protocol = frameType switch
                    {
                        0x00 => "Eddystone UID",
                        0x10 => "Eddystone URL",
                        0x20 => "Eddystone TLM",
                        0x30 => "Eddystone EID",
                        _ => "Eddystone"
                    };
                    isBeacon = true;

                    if (frameType is 0x00 or 0x10 or 0x30)
                    {
                        var calibratedAtZero = unchecked((sbyte)eddystone[1]);
                        var oneMeter = calibratedAtZero - 41.0;
                        if (ValidCalibration(oneMeter))
                            measuredAtOneMeter = oneMeter;
                    }

                    if (frameType == 0x00 && eddystone.Length >= 18)
                        beaconId = ToHexRange(eddystone, 2, 16);
                }
            }

            var distance = measuredAtOneMeter is null
                ? null
                : EstimateDistance(address, measuredAtOneMeter.Value, rssi);

            var deviation = RssiStdDev(address);
            var sampleCount = RssiSampleCount(address);
            var confidence = RssiConfidence(deviation, sampleCount);
            var approximateRange = ApproximateRange(rssi, measuredAtOneMeter is not null, distance);

            int? radioTxPower = null;
            if (record is not null && record.TxPowerLevel != int.MinValue)
                radioTxPower = record.TxPowerLevel;

            var services = record?.ServiceUuids?
                .Select(x => x.ToString() ?? "")
                .Where(x => x.Length > 0)
                .ToArray()
                ?? Array.Empty<string>();

            var manufacturers = new List<ManufacturerPayload>();
            if (record is not null)
            {
                var apple = record.GetManufacturerSpecificData(0x004C);
                if (apple is { Length: > 0 })
                {
                    manufacturers.Add(new ManufacturerPayload
                    {
                        Id = 0x004C,
                        Hex = ToHex(apple, 24)
                    });
                }
            }

            BeaconFound?.Invoke(this, new BeaconReading
            {
                Name = name,
                Address = address,
                Rssi = (int)Math.Round(rssi),
                RawRssi = rawRssi,
                Protocol = protocol,
                IsBeacon = isBeacon,
                Calibrated = measuredAtOneMeter is not null,
                CalibrationPower = measuredAtOneMeter is null ? null : (int)Math.Round(measuredAtOneMeter.Value),
                RadioTxPower = radioTxPower,
                Distance = distance,
                ApproximateRange = approximateRange,
                RssiStdDev = deviation,
                SampleCount = sampleCount,
                Confidence = confidence,
                BeaconId = beaconId,
                Services = services,
                Manufacturers = manufacturers,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Connectable = Build.VERSION.SdkInt >= BuildVersionCodes.O ? result.IsConnectable : null
            });
        }
        catch
        {
        }
    }

    private double SmoothRssi(string key, int raw)
    {
        var queue = _rssiWindows.GetOrAdd(key, _ => new Queue<int>());
        double median;

        lock (queue)
        {
            queue.Enqueue(raw);
            while (queue.Count > 7)
                queue.Dequeue();

            var values = queue.OrderBy(x => x).ToArray();
            var middle = values.Length / 2;
            median = values.Length % 2 == 0
                ? (values[middle - 1] + values[middle]) / 2.0
                : values[middle];
        }

        return _filteredRssi.AddOrUpdate(
            key,
            median,
            (_, old) => old * 0.68 + median * 0.32);
    }

    private int RssiSampleCount(string key)
    {
        if (!_rssiWindows.TryGetValue(key, out var queue))
            return 0;

        lock (queue) return queue.Count;
    }

    private double RssiStdDev(string key)
    {
        if (!_rssiWindows.TryGetValue(key, out var queue))
            return 0;

        lock (queue)
        {
            if (queue.Count < 2) return 0;
            var values = queue.ToArray();
            var mean = values.Average();
            return Math.Sqrt(values.Select(v => Math.Pow(v - mean, 2)).Average());
        }
    }

    private static string RssiConfidence(double deviation, int count)
    {
        if (count < 3) return "Inicial";
        if (deviation <= 2.5) return "Alta";
        if (deviation <= 5.0) return "Media";
        return "Baja";
    }

    private double? EstimateDistance(string key, double measuredAtOneMeter, double rssi)
    {
        if (!ValidCalibration(measuredAtOneMeter) || rssi < -100)
            return null;

        const double pathLossExponent = 2.2;
        var raw = Math.Pow(10.0, (measuredAtOneMeter - rssi) / (10.0 * pathLossExponent));

        if (!double.IsFinite(raw) || raw < 0.05 || raw > 50)
            return null;

        return _filteredDistance.AddOrUpdate(
            key,
            raw,
            (_, old) => old * 0.65 + raw * 0.35);
    }

    private static string ApproximateRange(double rssi, bool calibrated, double? distance)
    {
        if (calibrated && distance is not null)
        {
            var d = distance.Value;
            if (d < 0.8) return "menos de 1 m";
            if (d < 2) return "1–2 m";
            if (d < 4) return "2–4 m";
            if (d < 8) return "4–8 m";
            if (d < 15) return "8–15 m";
            return "más de 15 m";
        }

        if (rssi >= -55) return "≈ menos de 1 m";
        if (rssi >= -65) return "≈ 1–3 m";
        if (rssi >= -75) return "≈ 3–8 m";
        if (rssi >= -85) return "≈ 8–15 m";
        return "≈ más de 15 m";
    }

    private static bool ValidCalibration(double value)
        => value is >= -110 and <= -20;

    private static string FormatUuid(byte[] data, int offset)
    {
        if (data.Length < offset + 16) return "";
        var hex = ToHexRange(data, offset, 16).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}";
    }

    private static string ToHexRange(byte[] bytes, int offset, int length)
        => Convert.ToHexString(bytes, offset, Math.Min(length, bytes.Length - offset));

    private static string ToHex(byte[] bytes, int maxBytes)
    {
        var length = Math.Min(bytes.Length, maxBytes);
        var value = Convert.ToHexString(bytes, 0, length);
        return bytes.Length > maxBytes ? value + "…" : value;
    }

    private sealed class InternalScanCallback(AndroidBleScannerService owner) : ScanCallback
    {
        public override void OnScanResult(ScanCallbackType callbackType, ScanResult? result)
        {
            if (result is not null)
                owner.Process(result);
        }

        public override void OnBatchScanResults(IList<ScanResult>? results)
        {
            if (results is null) return;
            foreach (var result in results)
                owner.Process(result);
        }

        public override void OnScanFailed(ScanFailure errorCode)
        {
            owner.IsScanning = false;
            owner.ScanStateChanged?.Invoke(owner, new(false, $"Error BLE: {(int)errorCode}"));
        }
    }
}
