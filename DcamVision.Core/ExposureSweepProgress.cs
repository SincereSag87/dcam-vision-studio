namespace DcamVision.Core;

public sealed record ExposureSweepProgress(
    int ExposureIndex,
    int ExposureCount,
    TimeSpan CurrentExposure,
    int CapturedFrames,
    int TotalFrames)
{
    public double CompletionPercentage => TotalFrames == 0 ? 0 : CapturedFrames * 100.0 / TotalFrames;
}
