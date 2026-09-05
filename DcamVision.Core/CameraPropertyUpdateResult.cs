namespace DcamVision.Core;

public sealed record CameraPropertyUpdateResult(
    bool Success,
    CameraProperty? UpdatedProperty,
    string? ErrorMessage)
{
    public static CameraPropertyUpdateResult Updated(CameraProperty property) => new(true, property, null);

    public static CameraPropertyUpdateResult Failed(string errorMessage) => new(false, null, errorMessage);
}
