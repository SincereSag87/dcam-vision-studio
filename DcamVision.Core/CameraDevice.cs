namespace DcamVision.Core;

public sealed record CameraDevice(
    string Id,
    string DisplayName,
    string Manufacturer,
    string Model,
    string SerialNumber,
    bool IsSimulated);
