using DcamVision.Core;

namespace DcamVision.Imaging;

public static class FrameConverter
{
    public static byte[] ToGrayscale8(CameraFrame frame)
    {
        return ToGrayscale8(frame, lut: null);
    }

    public static byte[] ToGrayscale8(CameraFrame frame, LinearLut? lut)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();

        var pixels = new byte[frame.Pixels.Length];
        for (var i = 0; i < frame.Pixels.Length; i++)
        {
            pixels[i] = lut is null ? (byte)(frame.Pixels[i] >> 8) : lut.Map(frame.Pixels[i]);
        }

        return pixels;
    }
}
