using FleetAutomate.Model.Flow;

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FleetAutomate.Converters
{
    /// <summary>
    /// Converts ActionState enum to appropriate background color.
    /// Highlights executing actions with a distinct background color.
    /// </summary>
    public class ActionStateToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ActionState state)
            {
                return state switch
                {
                    ActionState.Running => new SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 200, 150)),  // Highlight background for executing action
                    _ => System.Windows.Media.Brushes.Transparent  // Transparent for all other states
                };
            }

            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
