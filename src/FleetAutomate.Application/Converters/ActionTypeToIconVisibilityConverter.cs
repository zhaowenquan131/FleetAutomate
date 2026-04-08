using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.UIAutomation;

namespace FleetAutomate.Converters
{
    /// <summary>
    /// Toggles between text icons and fluent symbol icons for action rendering.
    /// </summary>
    public class ActionTypeToIconVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var showSymbol = GetActionType(value) == typeof(ClickElementAction);
            var mode = parameter as string;

            return string.Equals(mode, "Symbol", StringComparison.OrdinalIgnoreCase)
                ? (showSymbol ? Visibility.Visible : Visibility.Collapsed)
                : (showSymbol ? Visibility.Collapsed : Visibility.Visible);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static Type? GetActionType(object value)
        {
            return value switch
            {
                ActionTemplate template => template.ActionType,
                null => null,
                _ => value.GetType()
            };
        }
    }
}
