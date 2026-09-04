using DcamVision.Core;

namespace DcamVision.Imaging;

public static class FrameConverter
{
    public static byte[] ToGrayscale8(CameraFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();

        var pixels = new byte[frame.Pixels.Length];
        for (var i = 0; i < frame.Pixels.Length; i++)
        {
            pixels[i] = (byte)(frame.Pixels[i] >> 8);
        }

        return pixels;
    }
}
