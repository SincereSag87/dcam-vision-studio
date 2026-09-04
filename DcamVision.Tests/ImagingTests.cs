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
}
