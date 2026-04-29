using System;
using System.Globalization;
using System.Windows.Data;
using FleetAutomate.Icons;

namespace FleetAutomate.Converters;

public sealed class ActionIconImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ActionIconRegistry.GetImage(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
