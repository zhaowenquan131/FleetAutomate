using System;
using System.Globalization;
using System.Windows.Data;
using Canvas.TestRunner.Model;
using Canvas.TestRunner.Model.Actions.UIAutomation;
using Canvas.TestRunner.Model.Actions.System;
using Canvas.TestRunner.Model.Actions.Logic;
using Canvas.TestRunner.Model.Actions.Logic.Loops;
using Canvas.TestRunner.Model.Flow;

namespace Canvas.TestRunner.Converters
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
                ClickElementAction => "👆",

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
