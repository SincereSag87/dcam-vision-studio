namespace DcamVision.Core;

public static class CameraPropertyCategories
{
    public const string Acquisition = "Acquisition";
    public const string Exposure = "Exposure";
    public const string Sensor = "Sensor";
    public const string Image = "Image";
    public const string Trigger = "Trigger";
    public const string Device = "Device";
    public const string Advanced = "Advanced";

    public static readonly IReadOnlyList<string> DefaultOrder =
    [
        Acquisition,
        Exposure,
        Sensor,
        Image,
        Trigger,
        Device,
        Advanced
    ];
}
