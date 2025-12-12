using Canvas.TestRunner.Model.Project;
using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Canvas.TestRunner.Converters
{
    public class ProjectToTitleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var baseTitle = "Canvas Test Runner";
            
            if (value is not TestProject project)
                return baseTitle;

            var projectName = !string.IsNullOrWhiteSpace(project.Name) ? project.Name : "Untitled Project";
            return $"{baseTitle} - {projectName}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}