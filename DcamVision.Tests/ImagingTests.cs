using DcamVision.Core;
using DcamVision.Imaging;

namespace DcamVision.Tests;

public sealed class ImagingTests
{
    [Fact]
    public void ToGrayscale8_ConvertsMono16Frame()
    {
        var frame = new CameraFrame
        {
            Width = 2,
            Height = 2,
            PixelFormat = CameraPixelFormat.Mono16,
            Pixels = [0, 256, 32768, 65535],
            Timestamp = DateTimeOffset.UtcNow,
            FrameNumber = 1,
            Exposure = TimeSpan.FromMilliseconds(25)
        };

        var bytes = FrameConverter.ToGrayscale8(frame);

        Assert.Equal([0, 1, 128, 255], bytes);
    }

    [Fact]
    public void ToGrayscale8_UsesLutWhenProvided()
    {
        var frame = new CameraFrame
        {
            Width = 3,
            Height = 1,
            PixelFormat = CameraPixelFormat.Mono16,
            Pixels = [100, 200, 300],
            Timestamp = DateTimeOffset.UtcNow,
            FrameNumber = 12,
            Exposure = TimeSpan.FromMilliseconds(25)
        };

        var bytes = FrameConverter.ToGrayscale8(frame, new LinearLut(100, 300));

        Assert.Equal([0, 128, 255], bytes);
    }

    [Fact]
    public void LinearLut_ClampsOutsideRange()
    {
        var lut = new LinearLut(1000, 2000);

        Assert.Equal(0, lut.Map(500));
        Assert.Equal(255, lut.Map(2500));
    }

    [Fact]
    public void HistogramCalculator_CountsPixels()
    {
        var frame = new CameraFrame
        {
            Width = 2,
            Height = 2,
            PixelFormat = CameraPixelFormat.Mono16,
            Pixels = [0, 0, 32768, 65535],
            Timestamp = DateTimeOffset.UtcNow,
            FrameNumber = 1,
            Exposure = TimeSpan.FromMilliseconds(25)
        };

        var histogram = HistogramCalculator.Calculate16Bit(frame, bins: 2);

        Assert.Equal([2, 2], histogram);
    }

    [Fact]
    public void FrameStatisticsCalculator_ReturnsMinimumMaximumMeanAndDimensions()
    {
        var frame = new CameraFrame
        {
            Width = 2,
            Height = 2,
            PixelFormat = CameraPixelFormat.Mono16,
            Pixels = [10, 20, 30, 40],
            Timestamp = DateTimeOffset.UtcNow,
            FrameNumber = 7,
            Exposure = TimeSpan.FromMilliseconds(25)
        };

        var statistics = FrameStatisticsCalculator.Calculate(frame);

        Assert.Equal(10, statistics.Minimum);
        Assert.Equal(40, statistics.Maximum);
        Assert.Equal(25, statistics.Mean);
        Assert.Equal(2, statistics.Width);
        Assert.Equal(2, statistics.Height);
        Assert.Equal(7, statistics.FrameNumber);
    }

    [Fact]
    public void FrameStatisticsCalculator_RejectsInvalidFrameBuffer()
    {
        var frame = new CameraFrame
        {
            Width = 2,
            Height = 2,
            PixelFormat = CameraPixelFormat.Mono16,
            Pixels = [1, 2, 3],
            Timestamp = DateTimeOffset.UtcNow,
            FrameNumber = 1,
            Exposure = TimeSpan.FromMilliseconds(25)
        };

        Assert.Throws<ArgumentException>(() => FrameStatisticsCalculator.Calculate(frame));
    }
}
