namespace DcamVision.Imaging;

public sealed record CaptureComparison(
    CaptureRecord CaptureA,
    CaptureRecord CaptureB,
    TimeSpan ExposureDifference,
    double MeanDifference,
    double SaturationDifference,
    double? MeanAbsolutePixelDifference);
