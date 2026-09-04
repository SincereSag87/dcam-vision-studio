namespace DcamVision.Core;

public sealed record ExposureSweepSettings
{
    public required TimeSpan Start { get; init; }

    public required TimeSpan End { get; init; }

    public required TimeSpan Step { get; init; }

    public int FramesPerExposure { get; init; } = 1;

    public TimeSpan DelayBetweenCaptures { get; init; } = TimeSpan.Zero;

    public CameraExposureRange ExposureRange { get; init; } = CameraExposureRange.Default;
}
