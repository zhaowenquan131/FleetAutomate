using System.Windows;
using Wpf.Ui.Controls;
using LogLevel = FleetAutomate.Model.Actions.System.LogLevel;

namespace FleetAutomate.View.Dialog
{
    /// <summary>
    /// Interaction logic for LogActionDialog.xaml
    /// </summary>
    public partial class LogActionDialog : FluentWindow
    {
        /// <summary>
        /// Gets the log level selected by the user.
        /// </summary>
        public LogLevel LogLevel { get; private set; } = LogLevel.Info;

        /// <summary>
        /// Gets the message entered by the user.
        /// </summary>
        public string Message { get; private set; } = string.Empty;

        public LogActionDialog()
        {
            InitializeComponent();
            MessageTextBox.Focus();
            MessageTextBox.TextChanged += MessageTextBox_TextChanged;
            UpdateOkButtonState();
        }

        /// <summary>
        /// Constructor for editing an existing LogAction.
        /// </summary>
        /// <param name="logLevel">Current log level</param>
        /// <param name="message">Current message</param>
        public LogActionDialog(LogLevel logLevel, string message)
            : this()
        {
            // Pre-populate fields with existing values
            LogLevelComboBox.SelectedIndex = (int)logLevel;
            MessageTextBox.Text = message ?? string.Empty;

            // Update button text for editing
            OkButton.Content = "Save";

            // Update window title for editing
            Title = "Edit Log Message";
        }

        private void MessageTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void UpdateOkButtonState()
        {
            var message = MessageTextBox.Text?.Trim();
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(message);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var message = MessageTextBox.Text?.Trim();

            // Validate message
            if (string.IsNullOrWhiteSpace(message))
            {
                System.Windows.MessageBox.Show("Please enter a message to log.", "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Set results
            LogLevel = (LogLevel)LogLevelComboBox.SelectedIndex;
            Message = message;

            // Close with success
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
