using System.Globalization;

namespace DcamVision.Core;

public static class CameraPropertyValueConverter
{
    public static bool TryConvert(CameraProperty property, object? input, out object value, out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (input is null)
        {
            value = string.Empty;
            errorMessage = $"{property.DisplayName} requires a value.";
            return false;
        }

        try
        {
            switch (property.PropertyType)
            {
                case CameraPropertyType.Integer:
                    if (input is int integer)
                    {
                        value = integer;
                    }
                    else if (!int.TryParse(Convert.ToString(input, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                    {
                        value = 0;
                        errorMessage = $"{property.DisplayName} must be a whole number.";
                        return false;
                    }
                    else
                    {
                        value = integer;
                    }

                    break;

                case CameraPropertyType.FloatingPoint:
                    if (input is double floating)
                    {
                        value = floating;
                    }
                    else if (!double.TryParse(Convert.ToString(input, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out floating))
                    {
                        value = 0.0;
                        errorMessage = $"{property.DisplayName} must be a number.";
                        return false;
                    }
                    else
                    {
                        value = floating;
                    }

                    break;

                case CameraPropertyType.Boolean:
                    if (input is bool boolean)
                    {
                        value = boolean;
                    }
                    else if (!bool.TryParse(Convert.ToString(input, CultureInfo.InvariantCulture), out boolean))
                    {
                        value = false;
                        errorMessage = $"{property.DisplayName} must be true or false.";
                        return false;
                    }
                    else
                    {
                        value = boolean;
                    }

                    break;

                case CameraPropertyType.Enumeration:
                    var text = Convert.ToString(input, CultureInfo.InvariantCulture) ?? string.Empty;
                    var option = property.Options.FirstOrDefault(candidate =>
                        string.Equals(Convert.ToString(candidate.Value, CultureInfo.InvariantCulture), text, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(candidate.DisplayName, text, StringComparison.OrdinalIgnoreCase));
                    if (option is null)
                    {
                        value = text;
                        errorMessage = $"{property.DisplayName} must be one of: {string.Join(", ", property.Options.Select(candidate => candidate.DisplayName))}.";
                        return false;
                    }

                    value = option.Value;
                    break;

                case CameraPropertyType.Text:
                    value = Convert.ToString(input, CultureInfo.InvariantCulture) ?? string.Empty;
                    break;

                default:
                    value = input;
                    break;
            }
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            value = string.Empty;
            errorMessage = $"{property.DisplayName} has an invalid value.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public static string Format(object? value, CameraPropertyType propertyType)
    {
        return propertyType switch
        {
            CameraPropertyType.FloatingPoint when value is double floating => floating.ToString("0.###", CultureInfo.InvariantCulture),
            CameraPropertyType.FloatingPoint => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0.###", CultureInfo.InvariantCulture),
            CameraPropertyType.Boolean when value is bool boolean => boolean ? "On" : "Off",
            CameraPropertyType.Enumeration => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }
}
