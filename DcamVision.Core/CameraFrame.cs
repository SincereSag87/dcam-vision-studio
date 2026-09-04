namespace DcamVision.Core;

public sealed record CameraFrame
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required CameraPixelFormat PixelFormat { get; init; }

    public required ushort[] Pixels { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required long FrameNumber { get; init; }

    public required TimeSpan Exposure { get; init; }

    public void Validate()
    {
        if (Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Frame width must be positive.");
        }

        if (Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Height), "Frame height must be positive.");
        }

        if (Pixels.Length != Width * Height)
        {
            throw new ArgumentException("Pixel buffer length must match frame dimensions.", nameof(Pixels));
        }
    }
}
