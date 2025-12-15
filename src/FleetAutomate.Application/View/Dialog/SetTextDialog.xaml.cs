using System.Windows;
using Wpf.Ui.Controls;

namespace FleetAutomate.View.Dialog
{
    /// <summary>
    /// Interaction logic for SetTextDialog.xaml
    /// </summary>
    public partial class SetTextDialog : FluentWindow
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
        /// Gets the text to set in the input element.
        /// </summary>
        public string TextToSet { get; private set; } = string.Empty;

        /// <summary>
        /// Gets whether to clear existing text before setting new text.
        /// </summary>
        public bool ClearExistingText { get; private set; } = true;

        /// <summary>
        /// Gets the number of times to retry if the action fails.
        /// </summary>
        public int RetryTimes { get; private set; } = 3;

        /// <summary>
        /// Gets the delay in milliseconds between retry attempts.
        /// </summary>
        public int RetryDelayMilliseconds { get; private set; } = 500;

        public SetTextDialog()
        {
            InitializeComponent();
            ElementIdentifierTextBox.Focus();
            ElementIdentifierTextBox.TextChanged += ElementIdentifierTextBox_TextChanged;
            TextToSetTextBox.TextChanged += TextToSetTextBox_TextChanged;
            UpdateOkButtonState();
            // Set button text for creating
            OkButton.Content = "Create";
        }

        /// <summary>
        /// Constructor for editing an existing SetTextAction with pre-populated values.
        /// </summary>
        public SetTextDialog(string elementIdentifier, string identifierType, string textToSet, bool clearExistingText, int retryTimes, int retryDelayMilliseconds)
        {
            InitializeComponent();

            // Pre-populate the form fields
            ElementIdentifierTextBox.Text = elementIdentifier;
            IdentifierTypeComboBox.SelectedIndex = GetIndexFromIdentifierType(identifierType);
            TextToSetTextBox.Text = textToSet;
            ClearExistingTextCheckBox.IsChecked = clearExistingText;
            RetryTimesTextBox.Text = retryTimes.ToString();
            RetryDelayTextBox.Text = retryDelayMilliseconds.ToString();

            ElementIdentifierTextBox.Focus();
            ElementIdentifierTextBox.TextChanged += ElementIdentifierTextBox_TextChanged;
            TextToSetTextBox.TextChanged += TextToSetTextBox_TextChanged;
            UpdateOkButtonState();
            // Set button text for editing
            OkButton.Content = "OK";
        }

        private void ElementIdentifierTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void TextToSetTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void UpdateOkButtonState()
        {
            var identifier = ElementIdentifierTextBox.Text?.Trim();
            var text = TextToSetTextBox.Text?.Trim();
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(identifier) && !string.IsNullOrWhiteSpace(text);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var identifier = ElementIdentifierTextBox.Text?.Trim();
            var text = TextToSetTextBox.Text?.Trim();

            // Validate element identifier
            if (string.IsNullOrWhiteSpace(identifier))
            {
                System.Windows.MessageBox.Show("Please enter an element identifier.", "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Validate text to set
            if (string.IsNullOrWhiteSpace(text))
            {
                System.Windows.MessageBox.Show("Please enter the text to set.", "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
            TextToSet = text;
            ClearExistingText = ClearExistingTextCheckBox.IsChecked ?? true;
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
