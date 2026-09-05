using System.Globalization;
using DcamVision.Core;
using DcamVision.Dcam.Interop;

namespace DcamVision.Dcam.Properties;

public static class DcamPropertyMapper
{
    private static readonly Dictionary<int, string> SemanticIds = new()
    {
        [DcamConstants.PropertyExposureTime] = "exposure.time",
        [DcamConstants.PropertyImageWidth] = "image.width",
        [DcamConstants.PropertyImageHeight] = "image.height",
        [DcamConstants.PropertyImagePixelType] = "image.pixelFormat",
        [DcamConstants.PropertyTriggerSource] = "trigger.mode"
    };

    public static CameraProperty MapProperty(
        int nativePropertyId,
        string displayName,
        double nativeValue,
        DcamPropertyAttribute attribute,
        IReadOnlyList<CameraPropertyOption>? options = null)
    {
        var type = MapPropertyType(attribute, options);
        var id = ToApplicationPropertyId(nativePropertyId, displayName);
        var unit = MapUnit(id, displayName);
        var value = FromNativeValue(type, nativeValue, options);
        var access = MapAccess(attribute);

        return new CameraProperty
        {
            Id = id,
            DisplayName = displayName,
            Description = $"Native DCAM property 0x{nativePropertyId:X8}.",
            Category = MapCategory(id, displayName),
            DisplayOrder = MapDisplayOrder(id, nativePropertyId),
            PropertyType = type,
            Value = value,
            Minimum = type is CameraPropertyType.Integer ? (object)(int)Math.Round(attribute.Minimum) : attribute.Minimum,
            Maximum = type is CameraPropertyType.Integer ? (object)(int)Math.Round(attribute.Maximum) : attribute.Maximum,
            Step = type is CameraPropertyType.Integer ? (object)Math.Max(1, (int)Math.Round(attribute.Step)) : attribute.Step,
            Unit = unit,
            Access = access,
            Options = options ?? []
        };
    }

    public static string ToApplicationPropertyId(int nativePropertyId, string displayName)
    {
        if (SemanticIds.TryGetValue(nativePropertyId, out var semanticId))
        {
            return semanticId;
        }

        return $"dcam.{nativePropertyId:X8}";
    }

    public static bool TryGetNativePropertyId(string propertyId, out int nativePropertyId)
    {
        foreach (var pair in SemanticIds)
        {
            if (string.Equals(pair.Value, propertyId, StringComparison.OrdinalIgnoreCase))
            {
                nativePropertyId = pair.Key;
                return true;
            }
        }

        if (propertyId.StartsWith("dcam.", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(propertyId[5..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out nativePropertyId))
        {
            return true;
        }

        nativePropertyId = 0;
        return false;
    }

    public static double ToNativeValue(CameraProperty property, object value)
    {
        if (property.PropertyType is CameraPropertyType.Enumeration)
        {
            var option = property.Options.FirstOrDefault(candidate =>
                string.Equals(Convert.ToString(candidate.Value, CultureInfo.InvariantCulture), Convert.ToString(value, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.DisplayName, Convert.ToString(value, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase));
            return option is null
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture)
                : Convert.ToDouble(option.Value, CultureInfo.InvariantCulture);
        }

        if (property.Id == "exposure.time")
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture) / 1000.0;
        }

        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    public static CameraExposureRange MapExposureRange(CameraProperty exposureProperty)
    {
        var minimumMs = Convert.ToDouble(exposureProperty.Minimum, CultureInfo.InvariantCulture);
        var maximumMs = Convert.ToDouble(exposureProperty.Maximum, CultureInfo.InvariantCulture);
        return new CameraExposureRange(TimeSpan.FromMilliseconds(minimumMs), TimeSpan.FromMilliseconds(maximumMs));
    }

    private static CameraPropertyType MapPropertyType(DcamPropertyAttribute attribute, IReadOnlyList<CameraPropertyOption>? options)
    {
        if (options is { Count: > 0 })
        {
            return CameraPropertyType.Enumeration;
        }

        return attribute.Group switch
        {
            DcamConstants.PropertyTypeLong => CameraPropertyType.Integer,
            DcamConstants.PropertyTypeMode => CameraPropertyType.Enumeration,
            _ => CameraPropertyType.FloatingPoint
        };
    }

    private static object FromNativeValue(CameraPropertyType type, double nativeValue, IReadOnlyList<CameraPropertyOption>? options)
    {
        return type switch
        {
            CameraPropertyType.Integer => (int)Math.Round(nativeValue),
            CameraPropertyType.Enumeration when options is { Count: > 0 } => options.FirstOrDefault(option => Math.Abs(Convert.ToDouble(option.Value, CultureInfo.InvariantCulture) - nativeValue) < 0.000_001)?.Value ?? nativeValue,
            CameraPropertyType.Enumeration => nativeValue,
            _ => nativeValue
        };
    }

    private static CameraPropertyAccess MapAccess(DcamPropertyAttribute attribute)
    {
        var access = CameraPropertyAccess.Read;
        if ((attribute.Attribute & DcamConstants.PropertyAttributeWritable) != 0)
        {
            access |= CameraPropertyAccess.Write;
        }

        var id = attribute.PropertyId;
        if (id is DcamConstants.PropertyExposureTime or DcamConstants.PropertyTriggerSource)
        {
            access |= CameraPropertyAccess.WriteWhileStreaming;
        }

        return access;
    }

    private static string MapCategory(string id, string displayName)
    {
        if (id.StartsWith("exposure.", StringComparison.OrdinalIgnoreCase) || displayName.Contains("exposure", StringComparison.OrdinalIgnoreCase))
        {
            return CameraPropertyCategories.Exposure;
        }

        if (id.StartsWith("image.", StringComparison.OrdinalIgnoreCase) || displayName.Contains("width", StringComparison.OrdinalIgnoreCase) || displayName.Contains("height", StringComparison.OrdinalIgnoreCase))
        {
            return CameraPropertyCategories.Image;
        }

        if (id.StartsWith("trigger.", StringComparison.OrdinalIgnoreCase) || displayName.Contains("trigger", StringComparison.OrdinalIgnoreCase))
        {
            return CameraPropertyCategories.Trigger;
        }

        if (displayName.Contains("temperature", StringComparison.OrdinalIgnoreCase) || displayName.Contains("gain", StringComparison.OrdinalIgnoreCase))
        {
            return CameraPropertyCategories.Sensor;
        }

        return CameraPropertyCategories.Advanced;
    }

    private static string? MapUnit(string id, string displayName)
    {
        if (id == "exposure.time")
        {
            return "ms";
        }

        if (displayName.Contains("temperature", StringComparison.OrdinalIgnoreCase))
        {
            return "C";
        }

        return null;
    }

    private static int MapDisplayOrder(string id, int nativePropertyId)
    {
        return id switch
        {
            "exposure.time" => 20,
            "image.width" => 50,
            "image.height" => 60,
            "image.pixelFormat" => 70,
            "trigger.mode" => 80,
            _ => 1000 + nativePropertyId
        };
    }
}
