using System;
using System.Globalization;
using System.Windows.Data;
using FleetAutomate.Icons;
using Wpf.Ui.Controls;

namespace FleetAutomate.Converters
{
    /// <summary>
    /// Resolves action and category types to Fluent UI system symbols.
    /// </summary>
    public class ActionTypeToSymbolConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ActionSymbolRegistry.GetSymbol(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
