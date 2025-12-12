using Canvas.TestRunner.Model.Project;
using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Canvas.TestRunner.Converters
{
    public class ProjectToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string projectPath && !string.IsNullOrEmpty(projectPath))
            {
                return $"Project: {Path.GetFileName(projectPath)} ({projectPath})";
            }

            return "No project loaded";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}