using DcamVision.Core;

namespace DcamVision.Imaging;

public static class HistogramCalculator
{
    public static int[] Calculate16Bit(CameraFrame frame, int bins = 256)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();

        if (bins <= 0 || bins > ushort.MaxValue + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bins), "Histogram bin count must be between 1 and 65536.");
        }

        var histogram = new int[bins];
        foreach (var pixel in frame.Pixels)
        {
            var bin = (int)Math.Min(bins - 1, pixel * (long)bins / (ushort.MaxValue + 1L));
            histogram[bin]++;
        }

        return histogram;
    }
}
