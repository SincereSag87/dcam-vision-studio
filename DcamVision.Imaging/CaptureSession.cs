namespace DcamVision.Imaging;

public sealed record CaptureSession
{
    public required Guid SessionId { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    public required string Name { get; init; }

    public string Notes { get; init; } = string.Empty;
}
