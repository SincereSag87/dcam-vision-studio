using System.Windows.Media;
using System.Windows.Media.Imaging;
using DcamVision.Core;
using DcamVision.Imaging;

namespace DcamVision.App.Services;

public sealed class ImagePreviewService
{
    public ImageSource CreatePreview(CameraFrame frame, bool autoContrast)
    {
        var statistics = FrameStatisticsCalculator.Calculate(frame);
        var lut = autoContrast && statistics.Maximum > statistics.Minimum
            ? new LinearLut(statistics.Minimum, statistics.Maximum)
            : null;

        var pixels = FrameConverter.ToGrayscale8(frame, lut);
        var bitmap = BitmapSource.Create(
            frame.Width,
            frame.Height,
            96,
            96,
            PixelFormats.Gray8,
            null,
            pixels,
            frame.Width);
        bitmap.Freeze();

        return bitmap;
    }
}
