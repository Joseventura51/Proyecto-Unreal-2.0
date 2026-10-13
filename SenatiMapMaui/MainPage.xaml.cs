using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
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

            switch (command)
            {
                case "location/start":
                    await _locationService.StartAsync();
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
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("SENATI Map", ex.Message, "OK");
        }
    }

    private void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
        => _webReady = e.Result == WebNavigationResult.Success;

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
