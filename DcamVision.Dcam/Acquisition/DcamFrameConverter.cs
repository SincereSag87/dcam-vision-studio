using DcamVision.Core;
using DcamVision.Dcam.Interop;

namespace DcamVision.Dcam.Acquisition;

public static class DcamFrameConverter
{
    public static CameraFrame Convert(DcamRawFrame rawFrame, TimeSpan exposure, long fallbackFrameNumber)
    {
        ArgumentNullException.ThrowIfNull(rawFrame);

        var pixelFormat = MapPixelFormat(rawFrame.PixelType);
        var pixels = pixelFormat switch
        {
            CameraPixelFormat.Mono16 => CopyMono16(rawFrame),
            CameraPixelFormat.Mono8 => CopyMono8ToMono16(rawFrame),
            _ => throw new NotSupportedException($"Unsupported DCAM pixel format '{rawFrame.PixelType}'.")
        };

        var frameNumber = rawFrame.FrameStamp > 0 ? rawFrame.FrameStamp : fallbackFrameNumber;
        var frame = new CameraFrame
        {
            Width = rawFrame.Width,
            Height = rawFrame.Height,
            PixelFormat = pixelFormat,
            Pixels = pixels,
            Timestamp = rawFrame.Timestamp ?? DateTimeOffset.UtcNow,
            FrameNumber = frameNumber,
            Exposure = exposure
        };
        frame.Validate();
        return frame;
    }

    public static CameraPixelFormat MapPixelFormat(int pixelType)
    {
        return pixelType switch
        {
            DcamConstants.PixelTypeMono8 => CameraPixelFormat.Mono8,
            DcamConstants.PixelTypeMono16 => CameraPixelFormat.Mono16,
            _ => throw new NotSupportedException($"DCAM pixel type '{pixelType}' is not supported in Phase 9.")
        };
    }

    private static ushort[] CopyMono16(DcamRawFrame rawFrame)
    {
        if (rawFrame.RowBytes < rawFrame.Width * 2)
        {
            throw new InvalidOperationException("DCAM Mono16 row pitch is smaller than the frame width.");
        }

        var pixels = new ushort[checked(rawFrame.Width * rawFrame.Height)];
        for (var y = 0; y < rawFrame.Height; y++)
        {
            var rowOffset = y * rawFrame.RowBytes;
            for (var x = 0; x < rawFrame.Width; x++)
            {
                var source = rowOffset + x * 2;
                pixels[y * rawFrame.Width + x] = (ushort)(rawFrame.Buffer[source] | (rawFrame.Buffer[source + 1] << 8));
            }
        }

        return pixels;
    }

    private static ushort[] CopyMono8ToMono16(DcamRawFrame rawFrame)
    {
        if (rawFrame.RowBytes < rawFrame.Width)
        {
            throw new InvalidOperationException("DCAM Mono8 row pitch is smaller than the frame width.");
        }

        var pixels = new ushort[checked(rawFrame.Width * rawFrame.Height)];
        for (var y = 0; y < rawFrame.Height; y++)
        {
            var rowOffset = y * rawFrame.RowBytes;
            for (var x = 0; x < rawFrame.Width; x++)
            {
                pixels[y * rawFrame.Width + x] = (ushort)(rawFrame.Buffer[rowOffset + x] * 257);
            }
        }

        return pixels;
    }
}
