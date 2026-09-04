using System.Globalization;

namespace DcamVision.Core;

public static class CameraExposureRange
{
    public static readonly TimeSpan Minimum = TimeSpan.FromMilliseconds(0.1);

    public static readonly TimeSpan Maximum = TimeSpan.FromSeconds(10);

    public static bool TryParseMilliseconds(string value, out TimeSpan exposure, out string errorMessage)
    {
        exposure = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Exposure time is required.";
            return false;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
        {
            errorMessage = "Exposure time must be a number in milliseconds.";
            return false;
        }

        exposure = TimeSpan.FromMilliseconds(milliseconds);
        return TryValidate(exposure, out errorMessage);
    }

    public static bool TryValidate(TimeSpan exposure, out string errorMessage)
    {
        if (exposure < Minimum)
        {
            errorMessage = $"Exposure time must be at least {Minimum.TotalMilliseconds:0.###} ms.";
            return false;
        }

        if (exposure > Maximum)
        {
            errorMessage = $"Exposure time must be {Maximum.TotalMilliseconds:0.###} ms or less.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
