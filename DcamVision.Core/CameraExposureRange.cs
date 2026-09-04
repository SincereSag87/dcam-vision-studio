namespace DcamVision.Core;

public sealed record CameraExposureRange(TimeSpan Minimum, TimeSpan Maximum)
{
    public static readonly CameraExposureRange Default = new(
        TimeSpan.FromMilliseconds(0.1),
        TimeSpan.FromSeconds(10));

    public bool Contains(TimeSpan exposure)
    {
        return exposure >= Minimum && exposure <= Maximum;
    }

    public bool TryValidate(TimeSpan exposure, out string errorMessage)
    {
        if (exposure < Minimum)
        {
            errorMessage = $"Exposure time must be at least {ExposureUnitConverter.Format(Minimum)}.";
            return false;
        }

        if (exposure > Maximum)
        {
            errorMessage = $"Exposure time must be {ExposureUnitConverter.Format(Maximum)} or less.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public bool TryParse(string value, ExposureUnit unit, out TimeSpan exposure, out string errorMessage)
    {
        if (!ExposureUnitConverter.TryFromDisplayValue(value, unit, out exposure, out errorMessage))
        {
            return false;
        }

        return TryValidate(exposure, out errorMessage);
    }
}
