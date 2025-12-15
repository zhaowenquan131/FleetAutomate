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

        public SetTextDialog()
        {
            InitializeComponent();
            ElementIdentifierTextBox.Focus();
            ElementIdentifierTextBox.TextChanged += ElementIdentifierTextBox_TextChanged;
            TextToSetTextBox.TextChanged += TextToSetTextBox_TextChanged;
            UpdateOkButtonState();
        }

        /// <summary>
        /// Constructor for editing an existing SetTextAction with pre-populated values.
        /// </summary>
        public SetTextDialog(string elementIdentifier, string identifierType, string textToSet, bool clearExistingText)
        {
            InitializeComponent();

            // Pre-populate the form fields
            ElementIdentifierTextBox.Text = elementIdentifier;
            IdentifierTypeComboBox.SelectedIndex = GetIndexFromIdentifierType(identifierType);
            TextToSetTextBox.Text = textToSet;
            ClearExistingTextCheckBox.IsChecked = clearExistingText;

            ElementIdentifierTextBox.Focus();
            ElementIdentifierTextBox.TextChanged += ElementIdentifierTextBox_TextChanged;
            TextToSetTextBox.TextChanged += TextToSetTextBox_TextChanged;
            UpdateOkButtonState();
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

            // Set results
            ElementIdentifier = identifier;
            IdentifierType = GetIdentifierTypeFromIndex(IdentifierTypeComboBox.SelectedIndex);
            TextToSet = text;
            ClearExistingText = ClearExistingTextCheckBox.IsChecked ?? true;

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
