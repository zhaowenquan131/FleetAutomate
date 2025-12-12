using FleetAutomate.Model.Flow;

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FleetAutomate.Converters
{
    /// <summary>
    /// Converts ActionState enum to appropriate foreground color.
    /// </summary>
    public class ActionStateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ActionState state)
            {
                return state switch
                {
                    ActionState.Ready => System.Windows.Media.Brushes.Gray,          // Gray for ready/idle
                    ActionState.Running => System.Windows.Media.Brushes.Green,       // Green for running
                    ActionState.Paused => System.Windows.Media.Brushes.Orange,       // Orange for paused
                    ActionState.Completed => System.Windows.Media.Brushes.Green,     // Green for success
                    ActionState.Failed => System.Windows.Media.Brushes.Red,          // Red for failed
                    _ => System.Windows.Media.Brushes.Gray
                };
            }

            return System.Windows.Media.Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
