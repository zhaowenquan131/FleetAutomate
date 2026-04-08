using System;
using System.Globalization;
using System.Windows.Data;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.Model;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Converters
{
    /// <summary>
    /// Converts an action type to its corresponding icon.
    /// </summary>
    public class ActionTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "❓";

            return value switch
            {
                // UI Automation Actions
                WaitForElementAction => "⏱️",
                ClickElementAction => string.Empty,

                // System Actions
                LaunchApplicationAction => "🚀",

                // Logic Actions
                IfAction => "🔀",
                SetVariableAction<object> => "📝",

                // Loop Actions
                WhileLoopAction => "🔄",
                ForLoopAction => "🔁",

                // Special Actions
                ActionBlock => "📦",
                TestFlow => "📋",

                // Default
                _ => "⚙️"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
