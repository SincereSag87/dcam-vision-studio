using DcamVision.Core;

namespace DcamVision.Imaging;

public static class HistogramAnalyzer
{
    public static HistogramResult Analyze(CameraFrame frame, int bins = 256)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();

        if (bins <= 0 || bins > ushort.MaxValue + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bins), "Histogram bin count must be between 1 and 65536.");
        }

        if (frame.Pixels.Length == 0)
        {
            throw new ArgumentException("Frame must contain at least one pixel.", nameof(frame));
        }

        var displayBins = new int[bins];
        var fullBins = new int[ushort.MaxValue + 1];
        ushort minimum = ushort.MaxValue;
        ushort maximum = ushort.MinValue;
        double mean = 0;
        double sumSquares = 0;
        var saturatedPixelCount = 0;
        long count = 0;

        foreach (var pixel in frame.Pixels)
        {
            count++;
            minimum = Math.Min(minimum, pixel);
            maximum = Math.Max(maximum, pixel);
            mean += pixel;
            sumSquares += (double)pixel * pixel;

            if (pixel >= FrameStatisticsCalculator.SaturationThreshold)
            {
                saturatedPixelCount++;
            }

            displayBins[(int)Math.Min(bins - 1, pixel * (long)bins / (ushort.MaxValue + 1L))]++;
            fullBins[pixel]++;
        }

        mean /= count;
        var variance = Math.Max(0, (sumSquares / count) - (mean * mean));
        var median = CalculateMedian(fullBins, count);

        return new HistogramResult(
            displayBins,
            minimum,
            maximum,
            mean,
            median,
            Math.Sqrt(variance),
            count,
            saturatedPixelCount,
            saturatedPixelCount * 100.0 / count);
    }

    private static double CalculateMedian(int[] bins, long pixelCount)
    {
        var lowerTarget = (pixelCount - 1) / 2;
        var upperTarget = pixelCount / 2;
        long cumulative = 0;
        int? lower = null;

        for (var value = 0; value < bins.Length; value++)
        {
            cumulative += bins[value];
            if (lower is null && cumulative > lowerTarget)
            {
                lower = value;
            }

            if (cumulative > upperTarget)
            {
                return (lower.GetValueOrDefault(value) + value) / 2.0;
            }
        }

        return 0;
    }
}
