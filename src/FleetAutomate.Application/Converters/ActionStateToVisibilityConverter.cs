using FleetAutomate.Model.Flow;

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FleetAutomate.Converters
{
    /// <summary>
    /// Converts ActionState enum to appropriate Wpf.Ui SymbolIcon type.
    /// Used to display the correct status icon based on the current state.
    /// </summary>
    public class ActionStateToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ActionState state)
            {
                return state switch
                {
                    ActionState.Ready => "Settings20",           // Gear for ready/idle
                    ActionState.Running => "Play20",             // Play for running/executing
                    ActionState.Paused => "PauseRegular20",      // Pause for paused
                    ActionState.Completed => "CheckmarkCircle20",// Checkmark circle for success
                    ActionState.Failed => "ErrorCircle20",       // Error circle for failed
                    _ => "Settings20"
                };
            }

            return "Settings20";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

