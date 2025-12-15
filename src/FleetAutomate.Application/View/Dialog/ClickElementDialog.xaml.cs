using System.Windows;
using Wpf.Ui.Controls;

namespace FleetAutomate.View.Dialog
{
    /// <summary>
    /// Interaction logic for ClickElementDialog.xaml
    /// </summary>
    public partial class ClickElementDialog : FluentWindow
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
        /// Gets whether to perform a double-click instead of single-click.
        /// </summary>
        public bool IsDoubleClick { get; private set; } = false;

        /// <summary>
        /// Gets whether to use Invoke pattern instead of actual mouse click.
        /// </summary>
        public bool UseInvoke { get; private set; } = false;

        /// <summary>
        /// Gets the number of times to retry if the action fails.
        /// </summary>
        public int RetryTimes { get; private set; } = 3;

        /// <summary>
        /// Gets the delay in milliseconds between retry attempts.
        /// </summary>
        public int RetryDelayMilliseconds { get; private set; } = 500;

        public ClickElementDialog()
        {
            InitializeComponent();
            ElementIdentifierTextBox.Focus();
            ElementIdentifierTextBox.TextChanged += ElementIdentifierTextBox_TextChanged;
            UpdateOkButtonState();
        }

        /// <summary>
        /// Constructor for editing an existing ClickElementAction with pre-populated values.
        /// </summary>
        public ClickElementDialog(string elementIdentifier, string identifierType, bool isDoubleClick, bool useInvoke, int retryTimes, int retryDelayMilliseconds)
        {
            InitializeComponent();

            // Pre-populate the form fields
            ElementIdentifierTextBox.Text = elementIdentifier;
            IdentifierTypeComboBox.SelectedIndex = GetIndexFromIdentifierType(identifierType);
            DoubleClickCheckBox.IsChecked = isDoubleClick;
            UseInvokeCheckBox.IsChecked = useInvoke;
            RetryTimesTextBox.Text = retryTimes.ToString();
            RetryDelayTextBox.Text = retryDelayMilliseconds.ToString();

            ElementIdentifierTextBox.Focus();
            ElementIdentifierTextBox.TextChanged += ElementIdentifierTextBox_TextChanged;
            UpdateOkButtonState();
        }

        private void ElementIdentifierTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void UpdateOkButtonState()
        {
            var identifier = ElementIdentifierTextBox.Text?.Trim();
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(identifier);
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

            // Validate retry times
            if (!int.TryParse(RetryTimesTextBox.Text?.Trim(), out int retryTimes) || retryTimes < 0)
            {
                System.Windows.MessageBox.Show("Retry times must be a non-negative integer (0 or greater).", "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                RetryTimesTextBox.Focus();
                return;
            }

            // Validate retry delay
            if (!int.TryParse(RetryDelayTextBox.Text?.Trim(), out int retryDelay) || retryDelay < 0)
            {
                System.Windows.MessageBox.Show("Retry delay must be a non-negative integer (0 or greater).", "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                RetryDelayTextBox.Focus();
                return;
            }

            // Set results
            ElementIdentifier = identifier;
            IdentifierType = GetIdentifierTypeFromIndex(IdentifierTypeComboBox.SelectedIndex);
            IsDoubleClick = DoubleClickCheckBox.IsChecked ?? false;
            UseInvoke = UseInvokeCheckBox.IsChecked ?? false;
            RetryTimes = retryTimes;
            RetryDelayMilliseconds = retryDelay;

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

        private static int GetIndexFromIdentifierType(string identifierType)
        {
            return identifierType switch
            {
                "XPath" => 0,
                "AutomationId" => 1,
                "Name" => 2,
                "ClassName" => 3,
                _ => 0
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
