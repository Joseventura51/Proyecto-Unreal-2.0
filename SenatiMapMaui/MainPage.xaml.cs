using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SenatiMapMaui.Models;
using SenatiMapMaui.Services;

namespace SenatiMapMaui;

public partial class MainPage : ContentPage
{
    private WebView? _webView;
    private ILocationTrackingService? _locationService;
    private IBleScannerService? _bleService;
    private bool _webReady;
    private bool _mapLoaded;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MainPage()
    {
        InitializeComponent();
    }

    private IServiceProvider? Services =>
        Handler?.MauiContext?.Services;

    private ILocationTrackingService GetLocationService()
    {
        if (_locationService is not null)
            return _locationService;

        var services = Services ?? throw new InvalidOperationException("Servicios MAUI no disponibles.");
        _locationService = ActivatorUtilities.CreateInstance<MauiLocationTrackingService>(services);
        _locationService.LocationChanged += OnLocationChanged;
        _locationService.TrackingStateChanged += OnTrackingStateChanged;
        return _locationService;
    }

    private IBleScannerService GetBleService()
    {
        if (_bleService is not null)
            return _bleService;

#if ANDROID
        _bleService = new Platforms.Android.AndroidBleScannerService();
#else
        throw new PlatformNotSupportedException("BLE solo está habilitado en Android.");
#endif
        _bleService.BeaconFound += OnBeaconFound;
        _bleService.ScanStateChanged += OnScanStateChanged;
        return _bleService;
    }

    private async void OnLoadMapClicked(object sender, EventArgs e)
    {
        if (_mapLoaded)
            return;

        LoadMapButton.IsEnabled = false;
        StateLabel.Text = "Creando WebView…";

        try
        {
            _webView = new WebView();
            _webView.Navigating += OnWebViewNavigating;
            _webView.Navigated += OnWebViewNavigated;

            MapHost.Children.Clear();
            MapHost.Children.Add(_webView);

            StateLabel.Text = "Leyendo mapa local…";

            await using var stream = await FileSystem.OpenAppPackageFileAsync("wwwroot/index.html");
            using var reader = new StreamReader(stream);
            var html = await reader.ReadToEndAsync();

            _webView.Source = new HtmlWebViewSource { Html = html };
            _mapLoaded = true;
            StateLabel.Text = "Mapa enviado al WebView.";
        }
        catch (Exception ex)
        {
            StateLabel.Text = "Error: " + ex.Message;
            LoadMapButton.IsEnabled = true;
            await DisplayAlert("Error al cargar mapa", ex.ToString(), "OK");
        }
    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
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
                    await GetLocationService().StartAsync();
                    break;
                case "ble/start":
                    await GetBleService().StartScanAsync();
                    break;
                case "ble/stop":
                    _bleService?.StopScan("Escaneo detenido");
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

    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _webReady = e.Result == WebNavigationResult.Success;
        StateLabel.Text = _webReady
            ? "Mapa cargado."
            : "El WebView no pudo terminar la carga.";
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
        if (!_webReady || _webView is null)
            return Task.CompletedTask;

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try { await _webView.EvaluateJavaScriptAsync(script); }
            catch { }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _locationService?.Stop();
        _bleService?.StopScan("Escaneo detenido");
    }
}
