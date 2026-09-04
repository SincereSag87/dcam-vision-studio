using System.Globalization;
using System.Windows.Data;
using DcamVision.Core;

namespace DcamVision.App.Converters;

public sealed class ExposureUnitLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ExposureUnit unit ? ExposureUnitConverter.GetUnitLabel(unit) : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
