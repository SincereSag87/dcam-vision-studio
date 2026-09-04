using DcamVision.Core;

namespace DcamVision.Imaging;

public static class HistogramPercentileCalculator
{
    public static ushort Calculate(CameraFrame frame, double percentile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();

        if (percentile is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 100.");
        }

        if (frame.Pixels.Length == 0)
        {
            throw new ArgumentException("Frame must contain at least one pixel.", nameof(frame));
        }

        var bins = new int[ushort.MaxValue + 1];
        foreach (var pixel in frame.Pixels)
        {
            bins[pixel]++;
        }

        return Calculate(bins, frame.Pixels.Length, percentile);
    }

    public static ushort Calculate(IReadOnlyList<int> histogram, long pixelCount, double percentile)
    {
        ArgumentNullException.ThrowIfNull(histogram);

        if (percentile is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 100.");
        }

        if (pixelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelCount), "Pixel count must be greater than zero.");
        }

        var target = (long)Math.Ceiling((percentile / 100.0) * pixelCount);
        target = Math.Clamp(target, 1, pixelCount);
        long cumulative = 0;

        for (var i = 0; i < histogram.Count; i++)
        {
            cumulative += histogram[i];
            if (cumulative >= target)
            {
                return (ushort)Math.Round(i * (ushort.MaxValue / Math.Max(1.0, histogram.Count - 1.0)));
            }
        }

        return ushort.MaxValue;
    }
}
