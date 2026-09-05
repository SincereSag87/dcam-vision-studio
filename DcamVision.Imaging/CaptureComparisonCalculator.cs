namespace DcamVision.Imaging;

public static class CaptureComparisonCalculator
{
    public static CaptureComparison Compare(CaptureRecord captureA, CaptureRecord captureB)
    {
        ArgumentNullException.ThrowIfNull(captureA);
        ArgumentNullException.ThrowIfNull(captureB);

        double? meanAbsolutePixelDifference = null;
        if (captureA.Frame.Width == captureB.Frame.Width &&
            captureA.Frame.Height == captureB.Frame.Height &&
            captureA.Frame.Pixels.Length == captureB.Frame.Pixels.Length)
        {
            ulong sum = 0;
            for (var i = 0; i < captureA.Frame.Pixels.Length; i++)
            {
                sum += (ulong)Math.Abs(captureA.Frame.Pixels[i] - captureB.Frame.Pixels[i]);
            }

            meanAbsolutePixelDifference = sum / (double)captureA.Frame.Pixels.Length;
        }

        return new CaptureComparison(
            captureA,
            captureB,
            (captureB.Metadata.Exposure - captureA.Metadata.Exposure).Duration(),
            captureB.Statistics.Mean - captureA.Statistics.Mean,
            captureB.Statistics.SaturationPercentage - captureA.Statistics.SaturationPercentage,
            meanAbsolutePixelDifference);
    }
}
