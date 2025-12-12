using System.Windows;
using Wpf.Ui.Controls;

namespace FleetAutomate.View.Dialog
{
    /// <summary>
    /// Interaction logic for LaunchApplicationDialog.xaml
    /// </summary>
    public partial class LaunchApplicationDialog : FluentWindow
    {
        /// <summary>
        /// Gets the executable path or command entered by the user.
        /// </summary>
        public string ExecutablePath { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the command-line arguments entered by the user.
        /// </summary>
        public string Arguments { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the working directory entered by the user.
        /// </summary>
        public string WorkingDirectory { get; private set; } = string.Empty;

        /// <summary>
        /// Gets whether the action should wait for the application to exit.
        /// </summary>
        public bool WaitForCompletion { get; private set; } = false;

        /// <summary>
        /// Gets the timeout in milliseconds.
        /// </summary>
        public int TimeoutMilliseconds { get; private set; } = 30000;

        public LaunchApplicationDialog()
        {
            InitializeComponent();
            ExecutablePathTextBox.Focus();
            ExecutablePathTextBox.TextChanged += ExecutablePathTextBox_TextChanged;
            WaitForCompletionCheckBox.Checked += WaitForCompletionCheckBox_Changed;
            WaitForCompletionCheckBox.Unchecked += WaitForCompletionCheckBox_Changed;
            UpdateOkButtonState();
        }

        private void ExecutablePathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void WaitForCompletionCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Enable/disable timeout controls based on wait for completion
            TimeoutTextBox.IsEnabled = WaitForCompletionCheckBox.IsChecked == true;
        }

        private void UpdateOkButtonState()
        {
            var executablePath = ExecutablePathTextBox.Text?.Trim();
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(executablePath);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var executablePath = ExecutablePathTextBox.Text?.Trim();

            // Validate executable path
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                System.Windows.MessageBox.Show("Please enter an executable path or command.", "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Set results
            ExecutablePath = executablePath;
            Arguments = ArgumentsTextBox.Text?.Trim() ?? string.Empty;
            WorkingDirectory = WorkingDirectoryTextBox.Text?.Trim() ?? string.Empty;
            WaitForCompletion = WaitForCompletionCheckBox.IsChecked == true;

            // Parse timeout value
            if (!int.TryParse(TimeoutTextBox.Text, out var timeoutMs) || timeoutMs < 1000 || timeoutMs > 300000)
            {
                System.Windows.MessageBox.Show("Timeout must be between 1000 and 300000 milliseconds.", "Invalid Timeout", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            TimeoutMilliseconds = timeoutMs;

            // Close with success
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                FilterIndex = 1,
                Multiselect = false,
                Title = "Select an executable file"
            };

            if (openFileDialog.ShowDialog(this) == true)
            {
                ExecutablePathTextBox.Text = openFileDialog.FileName;
            }
        }
    }
}
