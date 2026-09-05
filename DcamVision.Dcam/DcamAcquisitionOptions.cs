namespace DcamVision.Dcam;

public sealed record DcamAcquisitionOptions
{
    public TimeSpan FrameTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public int BufferCount { get; init; } = 8;

    public void Validate()
    {
        if (FrameTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(FrameTimeout), "Frame timeout must be positive.");
        }

        if (BufferCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(BufferCount), "DCAM buffer count must be positive.");
        }
    }
}
