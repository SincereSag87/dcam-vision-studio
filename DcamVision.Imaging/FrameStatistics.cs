namespace DcamVision.Imaging;

public sealed record FrameStatistics(
    ushort Minimum,
    ushort Maximum,
    double Mean,
    int Width,
    int Height,
    long FrameNumber,
    int SaturatedPixelCount,
    double SaturationPercentage);
