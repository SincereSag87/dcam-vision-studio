using System.Globalization;

namespace DcamVision.Core;

public static class CameraPropertyValidator
{
    public static CameraPropertyUpdateResult ValidateWrite(
        CameraProperty property,
        object? input,
        bool isStreaming = false)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (property.IsReadOnly)
        {
            return CameraPropertyUpdateResult.Failed($"{property.DisplayName} is read-only.");
        }

        if (!property.IsAvailable)
        {
            return CameraPropertyUpdateResult.Failed(property.AvailabilityReason ?? $"{property.DisplayName} is not available.");
        }

        if (isStreaming && !property.IsWritableWhileStreaming)
        {
            return CameraPropertyUpdateResult.Failed($"{property.DisplayName} cannot be changed while live acquisition is running.");
        }

        if (!CameraPropertyValueConverter.TryConvert(property, input, out var value, out var errorMessage))
        {
            return CameraPropertyUpdateResult.Failed(errorMessage);
        }

        if (!ValidateRange(property, value, out errorMessage))
        {
            return CameraPropertyUpdateResult.Failed(errorMessage);
        }

        if (!ValidateStep(property, value, out errorMessage))
        {
            return CameraPropertyUpdateResult.Failed(errorMessage);
        }

        return CameraPropertyUpdateResult.Updated(property with { Value = value });
    }

    private static bool ValidateRange(CameraProperty property, object value, out string errorMessage)
    {
        if (property.PropertyType is not (CameraPropertyType.Integer or CameraPropertyType.FloatingPoint))
        {
            errorMessage = string.Empty;
            return true;
        }

        var numeric = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (property.Minimum is not null && numeric < Convert.ToDouble(property.Minimum, CultureInfo.InvariantCulture))
        {
            errorMessage = $"{property.DisplayName} must be at least {FormatLimit(property.Minimum, property.Unit)}.";
            return false;
        }

        if (property.Maximum is not null && numeric > Convert.ToDouble(property.Maximum, CultureInfo.InvariantCulture))
        {
            errorMessage = $"{property.DisplayName} must be no more than {FormatLimit(property.Maximum, property.Unit)}.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool ValidateStep(CameraProperty property, object value, out string errorMessage)
    {
        if (property.Step is null || property.Minimum is null || property.PropertyType is not (CameraPropertyType.Integer or CameraPropertyType.FloatingPoint))
        {
            errorMessage = string.Empty;
            return true;
        }

        var numeric = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        var minimum = Convert.ToDouble(property.Minimum, CultureInfo.InvariantCulture);
        var step = Convert.ToDouble(property.Step, CultureInfo.InvariantCulture);
        if (step <= 0)
        {
            errorMessage = string.Empty;
            return true;
        }

        var steps = (numeric - minimum) / step;
        if (Math.Abs(steps - Math.Round(steps)) > 0.000_001)
        {
            errorMessage = $"{property.DisplayName} must use increments of {FormatLimit(property.Step, property.Unit)}.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static string FormatLimit(object value, string? unit)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return unit is null ? text : $"{text} {unit}";
    }
}
