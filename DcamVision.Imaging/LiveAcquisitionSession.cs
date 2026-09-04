namespace DcamVision.Imaging;

public sealed record LiveAcquisitionSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StoppedAt { get; init; }
}
