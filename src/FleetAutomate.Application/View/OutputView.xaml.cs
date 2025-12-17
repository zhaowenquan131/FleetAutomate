using FleetAutomate.Services;
using System.Collections.Specialized;
using System.Text;
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

            // Subscribe to collection changes to update text
            UILogTarget.LogEntries.CollectionChanged += LogEntries_CollectionChanged;

            // Initialize with existing log entries
            RefreshLogText();
        }

        private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
                {
                    // Append new log entries
                    foreach (LogEntry entry in e.NewItems)
                    {
                        if (LogTextBox.Text.Length > 0)
                        {
                            LogTextBox.AppendText(System.Environment.NewLine);
                        }
                        LogTextBox.AppendText(entry.ToString());
                    }

                    // Auto-scroll to bottom
                    if (AutoScrollCheckBox.IsChecked == true)
                    {
                        LogTextBox.ScrollToEnd();
                    }
                }
                else
                {
                    // For other actions (Clear, Reset, etc.), rebuild entire text
                    RefreshLogText();
                }
            });
        }

        private void RefreshLogText()
        {
            var sb = new StringBuilder();
            foreach (var entry in UILogTarget.LogEntries)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.Append(entry.ToString());
            }
            LogTextBox.Text = sb.ToString();

            // Auto-scroll to bottom
            if (AutoScrollCheckBox.IsChecked == true)
            {
                LogTextBox.ScrollToEnd();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            UILogTarget.Clear();
        }
    }
}
