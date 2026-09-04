namespace DcamVision.Imaging;

public sealed record DisplayClippingStatistics(
    int BlackClippedPixels,
    int WhiteClippedPixels,
    double BlackClippedPercentage,
    double WhiteClippedPercentage,
    int ThresholdBlackPixels,
    int ThresholdWhitePixels);
