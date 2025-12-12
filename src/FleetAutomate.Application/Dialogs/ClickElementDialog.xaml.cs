using System.Windows;
using Wpf.Ui.Controls;

namespace FleetAutomate.Dialogs
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
        public ClickElementDialog(string elementIdentifier, string identifierType, bool isDoubleClick, bool useInvoke)
        {
            InitializeComponent();

            // Pre-populate the form fields
            ElementIdentifierTextBox.Text = elementIdentifier;
            IdentifierTypeComboBox.SelectedIndex = GetIndexFromIdentifierType(identifierType);
            DoubleClickCheckBox.IsChecked = isDoubleClick;
            UseInvokeCheckBox.IsChecked = useInvoke;

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

            // Set results
            ElementIdentifier = identifier;
            IdentifierType = GetIdentifierTypeFromIndex(IdentifierTypeComboBox.SelectedIndex);
            IsDoubleClick = DoubleClickCheckBox.IsChecked ?? false;
            UseInvoke = UseInvokeCheckBox.IsChecked ?? false;

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
