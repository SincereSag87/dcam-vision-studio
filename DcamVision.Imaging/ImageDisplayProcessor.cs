using System.Diagnostics;
using DcamVision.Core;

namespace DcamVision.Imaging;

public sealed class ImageDisplayProcessor
{
    public DisplayFrame Process(CameraFrame frame, ImageDisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(settings);
        frame.Validate();
        settings.Validate();

        var stopwatch = Stopwatch.StartNew();
        var (effectiveBlack, effectiveWhite) = ResolveRange(frame, settings);

        if (effectiveWhite <= effectiveBlack)
        {
            effectiveBlack = 0;
            effectiveWhite = ushort.MaxValue;
        }

        var output = new byte[frame.Pixels.Length];
        var blackClipped = 0;
        var whiteClipped = 0;
        var thresholdBlack = 0;
        var thresholdWhite = 0;

        for (var i = 0; i < frame.Pixels.Length; i++)
        {
            var pixel = frame.Pixels[i];

            if (pixel <= effectiveBlack)
            {
                blackClipped++;
            }

            if (pixel >= effectiveWhite)
            {
                whiteClipped++;
            }

            if (settings.ThresholdEnabled)
            {
                if (pixel < settings.ThresholdValue)
                {
                    thresholdBlack++;
                    output[i] = settings.Invert ? byte.MaxValue : byte.MinValue;
                }
                else
                {
                    thresholdWhite++;
                    output[i] = settings.Invert ? byte.MinValue : byte.MaxValue;
                }

                continue;
            }

            var mapped = MapLinear(pixel, effectiveBlack, effectiveWhite, settings.Gamma);
            output[i] = settings.Invert ? (byte)(byte.MaxValue - mapped) : mapped;
        }

        stopwatch.Stop();
        var pixelCount = frame.Pixels.Length;
        return new DisplayFrame(
            output,
            effectiveBlack,
            effectiveWhite,
            new DisplayClippingStatistics(
                blackClipped,
                whiteClipped,
                blackClipped * 100.0 / pixelCount,
                whiteClipped * 100.0 / pixelCount,
                thresholdBlack,
                thresholdWhite),
            stopwatch.Elapsed);
    }

    private static (ushort Black, ushort White) ResolveRange(CameraFrame frame, ImageDisplaySettings settings)
    {
        return settings.AutoContrastMode switch
        {
            AutoContrastMode.MinMax => ResolveMinMax(frame),
            AutoContrastMode.Percentile => ResolvePercentile(frame, settings),
            _ => (settings.BlackPoint, settings.WhitePoint)
        };
    }

    private static (ushort Black, ushort White) ResolveMinMax(CameraFrame frame)
    {
        ushort minimum = ushort.MaxValue;
        ushort maximum = ushort.MinValue;

        foreach (var pixel in frame.Pixels)
        {
            minimum = Math.Min(minimum, pixel);
            maximum = Math.Max(maximum, pixel);
        }

        return minimum < maximum ? (minimum, maximum) : (ushort.MinValue, ushort.MaxValue);
    }

    private static (ushort Black, ushort White) ResolvePercentile(CameraFrame frame, ImageDisplaySettings settings)
    {
        var black = HistogramPercentileCalculator.Calculate(frame, settings.LowerPercentile);
        var white = HistogramPercentileCalculator.Calculate(frame, settings.UpperPercentile);
        return black < white ? (black, white) : ResolveMinMax(frame);
    }

    private static byte MapLinear(ushort value, ushort blackPoint, ushort whitePoint, double gamma)
    {
        if (value <= blackPoint)
        {
            return byte.MinValue;
        }

        if (value >= whitePoint)
        {
            return byte.MaxValue;
        }

        var normalized = (value - blackPoint) / (double)(whitePoint - blackPoint);
        var corrected = Math.Pow(normalized, 1.0 / gamma);
        return (byte)Math.Round(corrected * byte.MaxValue);
    }
}
