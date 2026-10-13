namespace SenatiMapMaui.Models;

public sealed class BeaconRegistration
{
    public string Address { get; set; } = "";
    public string Name { get; set; } = "Beacon SENATI";
    public double? CalibrationPower { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
