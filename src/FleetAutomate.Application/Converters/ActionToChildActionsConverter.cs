using System;
using System.Collections.ObjectModel;
using System.Windows.Data;
using FleetAutomate.Model;

namespace FleetAutomate.Converters
{
    /// <summary>
    /// Converts an action to its child actions if it implements ICompositeAction.
    /// Returns an empty collection if the action is not composite.
    /// </summary>
    public class ActionToChildActionsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ICompositeAction compositeAction)
            {
                return compositeAction.GetChildActions();
            }

            // Return an empty collection for non-composite actions
            return new ObservableCollection<IAction>();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
