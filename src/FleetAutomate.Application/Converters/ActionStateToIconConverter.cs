using FleetAutomate.Model.Flow;

using System;
using System.Globalization;
using System.Windows.Data;

namespace FleetAutomate.Converters
{
    /// <summary>
    /// Converts ActionState enum to appropriate icon emoji.
    /// </summary>
    public class ActionStateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ActionState state)
            {
                return state switch
                {
                    ActionState.Ready => "●",          // Circle for ready/idle (gray)
                    ActionState.Running => "▶",         // Play arrow for running/executing (green)
                    ActionState.Paused => "⏸",          // Pause for paused (orange)
                    ActionState.Completed => "✓",       // Checkmark for success (green)
                    ActionState.Failed => "✗",          // X mark for failed (red)
                    _ => "●"
                };
            }

            return "●";  // Default to circle
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
