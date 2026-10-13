namespace SenatiMapMaui.Services;

public sealed record LocationUpdate(double Latitude, double Longitude, double Accuracy);
public sealed record TrackingState(bool Active, string Message);

public interface ILocationTrackingService
{
    event EventHandler<LocationUpdate>? LocationChanged;
    event EventHandler<TrackingState>? TrackingStateChanged;
    bool IsRunning { get; }
    Task StartAsync();
    void Stop();
}
