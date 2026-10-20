using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net;
using System.Text.Json;
using SenatiMapMaui.Models;
using SenatiMapMaui.Services;

namespace SenatiMapMaui;

public partial class MainPage : ContentPage
{
    private readonly ILocationTrackingService _locationService;
    private readonly IServiceProvider _services;
    private IBleScannerService? _bleService;
    private bool _webReady;
    private bool _mapLoaded;

    private const string BeaconRegistryKey = "senati_beacon_registry_v1";
    private const string SavedMapViewKey = "senati_map_view_v1";
    private readonly Dictionary<string, BeaconRegistration> _knownBeacons =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MainPage(ILocationTrackingService locationService, IServiceProvider services)
    {
        InitializeComponent();

        _locationService = locationService;
        _services = services;

        _locationService.LocationChanged += OnLocationChanged;
        _locationService.TrackingStateChanged += OnTrackingStateChanged;

        LoadBeaconRegistry();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_mapLoaded)
        {
            _mapLoaded = true;
            _ = LoadMapAsync();
        }
    }

    private IBleScannerService GetBleService()
    {
        if (_bleService is not null)
            return _bleService;

        _bleService = _services.GetRequiredService<IBleScannerService>();
        _bleService.BeaconFound += OnBeaconFound;
        _bleService.ScanStateChanged += OnScanStateChanged;

        foreach (var beacon in _knownBeacons.Values)
        {
            _bleService.ConfigureKnownBeacon(
                beacon.Address,
                beacon.Name,
                beacon.CalibrationPower);
        }

        return _bleService;
    }

    private async Task LoadMapAsync()
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("wwwroot/index.html");
            using var reader = new StreamReader(stream);
            var html = await reader.ReadToEndAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                MapWebView.Source = new HtmlWebViewSource { Html = html };
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("SENATI Map", $"No se pudo cargar el mapa: {ex.Message}", "OK");
        }
    }

    private async void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Url) ||
            !e.Url.StartsWith("senatimaui://", StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;

        try
        {
            var uri = new Uri(e.Url);
            var command = $"{uri.Host}{uri.AbsolutePath}".TrimEnd('/').ToLowerInvariant();
            var query = ParseQuery(uri);

            switch (command)
            {
                case "location/start":
                    await _locationService.StartAsync();
                    break;
                case "map/save":
                    var state = query.GetValueOrDefault("state", "");
                    if (state.Length > 0 && state.Length <= 4096)
                    {
                        using var document = JsonDocument.Parse(state);
                        Preferences.Default.Set(SavedMapViewKey, state);
                    }
                    break;
                case "ble/start":
                    await GetBleService().StartScanAsync();
                    break;
                case "ble/stop":
                    GetBleService().StopScan("Escaneo detenido");
                    break;
                case "bluetooth/settings":
                    GetBleService().OpenBluetoothSettings();
                    break;

                case "beacon/register":
                    RegisterBeacon(
                        query.GetValueOrDefault("address", ""),
                        query.GetValueOrDefault("name", "Beacon SENATI"));
                    break;

                case "beacon/calibrate":
                    CalibrateBeacon(query.GetValueOrDefault("address", ""));
                    break;

                case "beacon/remove":
                    RemoveBeacon(query.GetValueOrDefault("address", ""));
                    break;

                case "beacon/registry":
                    PushBeaconRegistry();
                    break;
                case "beacon/anchor":
                    AssignBeaconAnchor(
                        query.GetValueOrDefault("address", ""),
                        query.GetValueOrDefault("lat", ""),
                        query.GetValueOrDefault("lon", ""),
                        query.GetValueOrDefault("floor", "0"));
                    break;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("SENATI Map", ex.Message, "OK");
        }
    }

    private void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
    {
        _webReady = e.Result == WebNavigationResult.Success;
        if (_webReady)
        {
            PushBeaconRegistry();
            var savedMap = Preferences.Default.Get(SavedMapViewKey, "");
            if (!string.IsNullOrWhiteSpace(savedMap))
                _ = RunJavaScriptAsync(
                    $"window.SenatiMap && SenatiMap.restoreSavedMapState({savedMap});");
        }
    }


    private static Dictionary<string, string> ParseQuery(Uri uri)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var query = uri.Query.TrimStart('?');

        if (string.IsNullOrWhiteSpace(query))
            return result;

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            var key = WebUtility.UrlDecode(pieces[0]);
            var value = pieces.Length > 1 ? WebUtility.UrlDecode(pieces[1]) : "";

            if (!string.IsNullOrWhiteSpace(key))
                result[key] = value ?? "";
        }

        return result;
    }

    private void LoadBeaconRegistry()
    {
        try
        {
            var json = Preferences.Default.Get(BeaconRegistryKey, "");
            if (string.IsNullOrWhiteSpace(json))
                return;

            var items = JsonSerializer.Deserialize<List<BeaconRegistration>>(json,
                new JsonSerializerOptions(JsonOptions) { PropertyNameCaseInsensitive = true }) ?? [];
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.Address))
                    _knownBeacons[item.Address] = item;
            }
        }
        catch
        {
            _knownBeacons.Clear();
        }
    }

    private void SaveBeaconRegistry()
    {
        var json = JsonSerializer.Serialize(_knownBeacons.Values.ToList(), JsonOptions);
        Preferences.Default.Set(BeaconRegistryKey, json);
    }

    private void RegisterBeacon(string address, string name)
    {
        if (string.IsNullOrWhiteSpace(address))
            return;

        var cleanName = string.IsNullOrWhiteSpace(name) ? "Beacon SENATI" : name.Trim();

        if (!_knownBeacons.TryGetValue(address, out var item))
        {
            item = new BeaconRegistration { Address = address };
            _knownBeacons[address] = item;
        }

        item.Name = cleanName;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        SaveBeaconRegistry();

        if (_bleService is not null)
            _bleService.ConfigureKnownBeacon(address, item.Name, item.CalibrationPower);

        PushBeaconRegistry();
    }

    private void RemoveBeacon(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return;

        _knownBeacons.Remove(address);
        SaveBeaconRegistry();
        _bleService?.RemoveKnownBeacon(address);
        PushBeaconRegistry();
    }

    private void AssignBeaconAnchor(string address, string latValue, string lonValue, string floorValue)
    {
        if (!_knownBeacons.TryGetValue(address, out var beacon) ||
            !double.TryParse(latValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !double.TryParse(lonValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
            !int.TryParse(floorValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var floor) ||
            latitude < -12.01 || latitude > -11.99 ||
            longitude < -77.07 || longitude > -77.05 ||
            floor < 0 || floor > 10)
        {
            _ = MainThread.InvokeOnMainThreadAsync(() =>
                DisplayAlert("SENATI Map", "Registra el beacon y proporciona coordenadas válidas del campus.", "Aceptar"));
            return;
        }

        beacon.Latitude = latitude;
        beacon.Longitude = longitude;
        beacon.Floor = floor;
        beacon.UpdatedAt = DateTimeOffset.UtcNow;
        SaveBeaconRegistry();
        PushBeaconRegistry();
    }

    private void CalibrateBeacon(string address)
    {
        if (string.IsNullOrWhiteSpace(address) ||
            !_knownBeacons.TryGetValue(address, out var item))
        {
            PushCalibrationResult(address, false, null, 0, 0,
                "Primero registra este dispositivo como Beacon SENATI.");
            return;
        }

        var ble = GetBleService();
        if (!ble.TryCalibrateBeacon(
            address,
            out var measuredPower,
            out var sampleCount,
            out var deviation))
        {
            PushCalibrationResult(
                address,
                false,
                null,
                sampleCount,
                deviation,
                "Necesito al menos 5 lecturas estables. Escanea unos segundos con el beacon a 1 metro.");
            return;
        }

        item.CalibrationPower = measuredPower;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        SaveBeaconRegistry();
        ble.ConfigureKnownBeacon(address, item.Name, measuredPower);

        PushCalibrationResult(
            address,
            true,
            measuredPower,
            sampleCount,
            deviation,
            $"Calibrado a 1 m: {measuredPower:0.0} dBm");

        PushBeaconRegistry();
    }

    private void PushBeaconRegistry()
    {
        if (!_webReady)
            return;

        var payload = JsonSerializer.Serialize(_knownBeacons.Values.ToList(), JsonOptions);
        _ = RunJavaScriptAsync(
            $"window.SenatiMap && SenatiMap.knownBeaconsState({payload});");
    }

    private void PushCalibrationResult(
        string address,
        bool success,
        double? power,
        int sampleCount,
        double deviation,
        string message)
    {
        var payload = JsonSerializer.Serialize(new
        {
            address,
            success,
            power,
            sampleCount,
            deviation,
            message
        }, JsonOptions);

        _ = RunJavaScriptAsync(
            $"window.SenatiMap && SenatiMap.beaconCalibrationResult({payload});");
    }

    private void OnLocationChanged(object? sender, LocationUpdate update)
    {
        var js = string.Create(
            CultureInfo.InvariantCulture,
            $"window.SenatiMap && SenatiMap.setUserLocation({update.Latitude},{update.Longitude},{update.Accuracy},false);");
        _ = RunJavaScriptAsync(js);
    }

    private void OnTrackingStateChanged(object? sender, TrackingState state)
    {
        var message = JsonSerializer.Serialize(state.Message);
        var active = state.Active ? "true" : "false";
        _ = RunJavaScriptAsync(
            $"window.SenatiMap && SenatiMap.locationTrackingState({active},{message});");
    }

    private void OnBeaconFound(object? sender, BeaconReading beacon)
    {
        var payload = JsonSerializer.Serialize(beacon, JsonOptions);
        _ = RunJavaScriptAsync($"window.SenatiMap && SenatiMap.onBeacon({payload});");
    }

    private void OnScanStateChanged(object? sender, ScanState state)
    {
        var active = state.Active ? "true" : "false";
        var message = JsonSerializer.Serialize(state.Message);
        _ = RunJavaScriptAsync(
            $"window.SenatiMap && SenatiMap.beaconScanState({active},{message});");
    }

    private Task RunJavaScriptAsync(string script)
    {
        if (!_webReady)
            return Task.CompletedTask;

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try { await MapWebView.EvaluateJavaScriptAsync(script); }
            catch { }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _locationService.Stop();
        _bleService?.StopScan("Escaneo detenido");
    }
}
