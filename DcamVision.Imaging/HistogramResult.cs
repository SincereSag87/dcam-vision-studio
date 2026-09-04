namespace DcamVision.Imaging;

public sealed record HistogramResult(
    int[] Bins,
    ushort Minimum,
    ushort Maximum,
    double Mean,
    double Median,
    double StandardDeviation,
    long PixelCount,
    int SaturatedPixelCount,
    double SaturationPercentage);
