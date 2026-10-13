namespace SenatiMapMaui.Models;

public sealed class BeaconReading
{
    public string Name { get; init; } = "Dispositivo BLE";
    public string Address { get; init; } = "";
    public int Rssi { get; init; }
    public int RawRssi { get; init; }
    public string Protocol { get; init; } = "BLE";
    public bool IsBeacon { get; init; }
    public bool Calibrated { get; init; }
    public int? CalibrationPower { get; init; }
    public int? RadioTxPower { get; init; }
    public double? Distance { get; init; }
    public string ApproximateRange { get; init; } = "";
    public double RssiStdDev { get; init; }
    public int SampleCount { get; init; }
    public string Confidence { get; init; } = "Inicial";
    public string BeaconId { get; init; } = "";
    public IReadOnlyList<string> Services { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ManufacturerPayload> Manufacturers { get; init; } = Array.Empty<ManufacturerPayload>();
    public long Timestamp { get; init; }
    public bool? Connectable { get; init; }
}

public sealed class ManufacturerPayload
{
    public int Id { get; init; }
    public string Hex { get; init; } = "";
}
