using FleetAutomate.Services;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace FleetAutomate.View
{
    /// <summary>
    /// Interaction logic for OutputView.xaml
    /// </summary>
    public partial class OutputView : System.Windows.Controls.UserControl
    {
        public OutputView()
        {
            InitializeComponent();

            // Bind to the UILogTarget's log entries
            DataContext = UILogTarget.LogEntries;

            // Subscribe to collection changes for auto-scroll
            UILogTarget.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
        }

        private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Auto-scroll to bottom when new items are added
            if (AutoScrollCheckBox.IsChecked == true && e.Action == NotifyCollectionChangedAction.Add)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                    }
                });
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            UILogTarget.Clear();
        }
    }
}
