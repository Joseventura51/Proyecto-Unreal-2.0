using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace SenatiMapMaui.Services;

public sealed class MauiLocationTrackingService : ILocationTrackingService
{
    private CancellationTokenSource? _cts;

    public event EventHandler<LocationUpdate>? LocationChanged;
    public event EventHandler<TrackingState>? TrackingStateChanged;

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public async Task StartAsync()
    {
        if (IsRunning)
        {
            TrackingStateChanged?.Invoke(this, new(true, "Seguimiento en vivo activo"));
            return;
        }

        var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (permission != PermissionStatus.Granted)
        {
            TrackingStateChanged?.Invoke(this, new(false, "Permiso de ubicación denegado"));
            return;
        }

        _cts = new CancellationTokenSource();
        TrackingStateChanged?.Invoke(this, new(true, "Seguimiento en vivo activo"));
        _ = TrackLoopAsync(_cts.Token);
    }

    private async Task TrackLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var request = new GeolocationRequest(
                    GeolocationAccuracy.Best,
                    TimeSpan.FromSeconds(5));

                var location = await Geolocation.Default.GetLocationAsync(request, token)
                    ?? await Geolocation.Default.GetLastKnownLocationAsync();

                if (location is not null)
                {
                    LocationChanged?.Invoke(
                        this,
                        new LocationUpdate(
                            location.Latitude,
                            location.Longitude,
                            location.Accuracy ?? 0));
                }

                await Task.Delay(1200, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (FeatureNotEnabledException)
            {
                TrackingStateChanged?.Invoke(this, new(false, "Activa la ubicación del teléfono"));
                Stop();
                break;
            }
            catch (PermissionException)
            {
                TrackingStateChanged?.Invoke(this, new(false, "Permiso de ubicación denegado"));
                Stop();
                break;
            }
            catch
            {
                await Task.Delay(1800, token);
            }
        }
    }

    public void Stop()
    {
        if (_cts is null)
            return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
        TrackingStateChanged?.Invoke(this, new(false, "Seguimiento detenido"));
    }
}
