namespace SenatiMapMaui.Models;

public sealed class BeaconRegistration
{
    public string Address { get; set; } = "";
    public string Name { get; set; } = "Beacon SENATI";
    public double? CalibrationPower { get; set; }
    // Surveyed or QR-confirmed fixed beacon position; never inferred from RSSI.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int Floor { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
