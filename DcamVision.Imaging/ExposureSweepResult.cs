namespace DcamVision.Imaging;

public sealed record ExposureSweepResult(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    IReadOnlyList<ExposureSweepFrame> Frames);
