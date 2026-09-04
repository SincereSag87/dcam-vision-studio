namespace DcamVision.Core;

public sealed record CaptureSettings
{
    public static readonly TimeSpan DefaultExposure = TimeSpan.FromMilliseconds(25);

    public TimeSpan Exposure { get; init; } = DefaultExposure;

    public double Gain { get; init; } = 1.0;

    public int Width { get; init; } = 512;

    public int Height { get; init; } = 384;

    public CameraPixelFormat PixelFormat { get; init; } = CameraPixelFormat.Mono16;

    public string TriggerMode { get; init; } = "Internal";
}
