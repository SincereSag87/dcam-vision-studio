namespace DcamVision.Dcam.Devices;

public sealed record DcamDeviceInfo
{
    public required int Index { get; init; }

    public required string DeviceId { get; init; }

    public string Vendor { get; init; } = "Hamamatsu";

    public string Model { get; init; } = "Hamamatsu DCAM Camera";

    public string CameraId { get; init; } = string.Empty;

    public string? Bus { get; init; }

    public string? CameraVersion { get; init; }

    public string? DriverVersion { get; init; }

    public string? ModuleVersion { get; init; }

    public string? DcamApiVersion { get; init; }
}
