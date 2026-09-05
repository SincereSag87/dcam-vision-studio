namespace DcamVision.Dcam.Acquisition;

public sealed record DcamRawFrame
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required int RowBytes { get; init; }

    public required int PixelType { get; init; }

    public required byte[] Buffer { get; init; }

    public required long FrameStamp { get; init; }

    public DateTimeOffset? Timestamp { get; init; }
}
