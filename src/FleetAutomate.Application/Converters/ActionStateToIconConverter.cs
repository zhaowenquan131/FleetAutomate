using FleetAutomate.Model;
using FleetAutomate.Model.Flow;

using System;
using System.Globalization;
using System.Windows.Data;

namespace FleetAutomate.Converters
{
    /// <summary>
    /// Converts ActionState enum to appropriate icon emoji.
    /// </summary>
    public class ActionStateToIconConverter : IValueConverter, IMultiValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IAction action)
            {
                return GetIcon(action.State, action);
            }

            if (value is ActionState state)
            {
                return GetIcon(state, null);
            }

            return "●";  // Default to circle
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var action = values.Length > 0 ? values[0] as IAction : null;
            var state = values.Length > 1 && values[1] is ActionState actionState
                ? actionState
                : action?.State ?? ActionState.Ready;

            return GetIcon(state, action);
        }

        private static string GetIcon(ActionState state, IAction? action)
        {
            var isPauseableRunning = state == ActionState.Running &&
                                     action is IPauseAwareAction pauseAwareAction &&
                                     pauseAwareAction.PauseBehavior != ActionPauseBehavior.None;

            if (isPauseableRunning)
            {
                return "⏸";
            }

            return state switch
            {
                ActionState.Ready => "●",          // Circle for ready/idle (gray)
                ActionState.Running => "○",        // Hollow circle for atomic running actions
                ActionState.Paused => "⏸",         // Pause for paused (orange)
                ActionState.Completed => "✓",      // Checkmark for success (green)
                ActionState.Failed => "✗",         // X mark for failed (red)
                _ => "●"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
