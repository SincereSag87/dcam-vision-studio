using DcamVision.Core;

namespace DcamVision.Imaging;

public sealed record ProcessedFrame(
    CameraFrame Frame,
    FrameStatistics Statistics,
    HistogramResult HistogramResult)
{
    public ProcessedFrame(CameraFrame frame, FrameStatistics statistics, int[] histogram)
        : this(
            frame,
            statistics,
            new HistogramResult(
                histogram,
                statistics.Minimum,
                statistics.Maximum,
                statistics.Mean,
                0,
                0,
                frame.Pixels.LongLength,
                statistics.SaturatedPixelCount,
                statistics.SaturationPercentage))
    {
    }

    public int[] Histogram => HistogramResult.Bins;
}
