namespace DcamVision.Imaging;

public sealed record CaptureHistoryOptions
{
    public int MaximumCaptures { get; init; } = 250;

    public void Validate()
    {
        if (MaximumCaptures < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumCaptures), "Maximum captures must be at least 1.");
        }
    }
}
