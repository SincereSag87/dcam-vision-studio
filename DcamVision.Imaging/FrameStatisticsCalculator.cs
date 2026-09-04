using DcamVision.Core;

namespace DcamVision.Imaging;

public static class FrameStatisticsCalculator
{
    public static FrameStatistics Calculate(CameraFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();

        if (frame.Pixels.Length == 0)
        {
            throw new ArgumentException("Frame must contain at least one pixel.", nameof(frame));
        }

        ushort minimum = ushort.MaxValue;
        ushort maximum = ushort.MinValue;
        ulong sum = 0;

        foreach (var pixel in frame.Pixels)
        {
            minimum = Math.Min(minimum, pixel);
            maximum = Math.Max(maximum, pixel);
            sum += pixel;
        }

        return new FrameStatistics(
            minimum,
            maximum,
            sum / (double)frame.Pixels.Length,
            frame.Width,
            frame.Height,
            frame.FrameNumber);
    }
}
