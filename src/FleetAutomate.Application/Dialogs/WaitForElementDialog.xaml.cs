using System.Windows;
using Wpf.Ui.Controls;

namespace FleetAutomate.Dialogs
{
    /// <summary>
    /// Interaction logic for WaitForElementDialog.xaml
    /// </summary>
    public partial class WaitForElementDialog : FluentWindow
    {
        /// <summary>
        /// Gets the element identifier entered by the user.
        /// </summary>
        public string ElementIdentifier { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the identifier type selected by the user.
        /// Valid values: "XPath", "AutomationId", "Name", "ClassName"
        /// </summary>
        public string IdentifierType { get; private set; } = "XPath";

        /// <summary>
        /// Gets the timeout in milliseconds.
        /// </summary>
        public int TimeoutMilliseconds { get; private set; } = 30000;

        /// <summary>
        /// Gets the polling interval in milliseconds.
        /// </summary>
        public int PollingIntervalMilliseconds { get; private set; } = 100;

        public WaitForElementDialog()
        {
            InitializeComponent();
            ElementIdentifierTextBox.Focus();
            ElementIdentifierTextBox.TextChanged += ElementIdentifierTextBox_TextChanged;
            IdentifierTypeComboBox.SelectionChanged += IdentifierTypeComboBox_SelectionChanged;
            UpdateOkButtonState();
            UpdateHintText();
        }

        private void ElementIdentifierTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void IdentifierTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateHintText();
        }

        private void UpdateOkButtonState()
        {
            var identifier = ElementIdentifierTextBox.Text?.Trim();
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(identifier);
        }

        private void UpdateHintText()
        {
            var selectedIndex = IdentifierTypeComboBox.SelectedIndex;
            // Hint text is static, but could be dynamic based on selection
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var identifier = ElementIdentifierTextBox.Text?.Trim();

            // Validate element identifier
            if (string.IsNullOrWhiteSpace(identifier))
            {
                System.Windows.MessageBox.Show("Please enter an element identifier.", "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Parse and validate timeout
            if (!int.TryParse(TimeoutTextBox.Text, out var timeoutMs) || timeoutMs < 1000 || timeoutMs > 300000)
            {
                System.Windows.MessageBox.Show("Timeout must be between 1000 and 300000 milliseconds.", "Invalid Timeout", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Parse and validate polling interval
            if (!int.TryParse(PollingIntervalTextBox.Text, out var pollingInterval) || pollingInterval < 50 || pollingInterval > 1000)
            {
                System.Windows.MessageBox.Show("Polling interval must be between 50 and 1000 milliseconds.", "Invalid Interval", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Set results
            ElementIdentifier = identifier;
            IdentifierType = GetIdentifierTypeFromIndex(IdentifierTypeComboBox.SelectedIndex);
            TimeoutMilliseconds = timeoutMs;
            PollingIntervalMilliseconds = pollingInterval;

            // Close with success
            DialogResult = true;
            Close();
        }

        private static string GetIdentifierTypeFromIndex(int index)
        {
            return index switch
            {
                0 => "XPath",
                1 => "AutomationId",
                2 => "Name",
                3 => "ClassName",
                _ => "XPath"
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
