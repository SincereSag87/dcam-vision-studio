namespace DcamVision.Imaging;

public sealed record LiveAcquisitionOptions
{
    public int BufferCapacity { get; init; } = 4;

    public LiveBufferOverflowStrategy OverflowStrategy { get; init; } = LiveBufferOverflowStrategy.DropOldest;

    public int MaximumPreviewFps { get; init; } = 30;

    public TimeSpan MinimumPreviewInterval => TimeSpan.FromSeconds(1.0 / MaximumPreviewFps);

    public void Validate()
    {
        if (BufferCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(BufferCapacity), "Buffer capacity must be at least 1.");
        }

        if (MaximumPreviewFps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumPreviewFps), "Maximum preview FPS must be at least 1.");
        }
    }
}
