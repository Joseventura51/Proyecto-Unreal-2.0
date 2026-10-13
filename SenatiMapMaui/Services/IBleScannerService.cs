using SenatiMapMaui.Models;

namespace SenatiMapMaui.Services;

public sealed record ScanState(bool Active, string Message);

public interface IBleScannerService
{
    event EventHandler<BeaconReading>? BeaconFound;
    event EventHandler<ScanState>? ScanStateChanged;

    bool IsScanning { get; }

    Task StartScanAsync();
    void StopScan(string message = "Escaneo detenido");
    void OpenBluetoothSettings();

    void ConfigureKnownBeacon(string address, string name, double? calibrationPower);
    void RemoveKnownBeacon(string address);
    bool TryCalibrateBeacon(
        string address,
        out double measuredPower,
        out int sampleCount,
        out double deviation);
}
