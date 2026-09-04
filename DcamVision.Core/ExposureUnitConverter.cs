using System.Globalization;

namespace DcamVision.Core;

public static class ExposureUnitConverter
{
    public static double ToDisplayValue(TimeSpan exposure, ExposureUnit unit)
    {
        return unit switch
        {
            ExposureUnit.Microseconds => exposure.TotalMicroseconds,
            ExposureUnit.Milliseconds => exposure.TotalMilliseconds,
            ExposureUnit.Seconds => exposure.TotalSeconds,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported exposure unit.")
        };
    }

    public static TimeSpan FromDisplayValue(double value, ExposureUnit unit)
    {
        return unit switch
        {
            ExposureUnit.Microseconds => TimeSpan.FromMicroseconds(value),
            ExposureUnit.Milliseconds => TimeSpan.FromMilliseconds(value),
            ExposureUnit.Seconds => TimeSpan.FromSeconds(value),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported exposure unit.")
        };
    }

    public static bool TryFromDisplayValue(string value, ExposureUnit unit, out TimeSpan exposure, out string errorMessage)
    {
        exposure = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Exposure time is required.";
            return false;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var displayValue))
        {
            errorMessage = "Exposure time must be a number.";
            return false;
        }

        if (displayValue <= 0)
        {
            errorMessage = "Exposure time must be greater than zero.";
            return false;
        }

        exposure = FromDisplayValue(displayValue, unit);
        errorMessage = string.Empty;
        return true;
    }

    public static string GetUnitLabel(ExposureUnit unit)
    {
        return unit switch
        {
            ExposureUnit.Microseconds => "µs",
            ExposureUnit.Milliseconds => "ms",
            ExposureUnit.Seconds => "s",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported exposure unit.")
        };
    }

    public static string Format(TimeSpan exposure)
    {
        if (exposure.TotalMilliseconds < 1)
        {
            return $"{exposure.TotalMicroseconds:0.###} µs";
        }

        if (exposure.TotalSeconds >= 1)
        {
            return $"{exposure.TotalSeconds:0.###} s";
        }

        return $"{exposure.TotalMilliseconds:0.###} ms";
    }
}
