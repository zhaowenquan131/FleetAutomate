using System;
using System.Globalization;
using System.Windows.Data;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.UIAutomation;
using Wpf.Ui.Controls;

namespace FleetAutomate.Converters
{
    /// <summary>
    /// Resolves action types that should use a fluent symbol instead of a text icon.
    /// </summary>
    public class ActionTypeToSymbolConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return GetActionType(value) == typeof(ClickElementAction)
                ? SymbolRegular.CursorClick20
                : null;
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
