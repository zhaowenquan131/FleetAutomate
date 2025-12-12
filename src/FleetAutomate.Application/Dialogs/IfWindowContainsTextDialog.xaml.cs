using System.Windows;
using Wpf.Ui.Controls;

namespace FleetAutomate.Dialogs
{
    /// <summary>
    /// Interaction logic for IfWindowContainsTextDialog.xaml
    /// </summary>
    public partial class IfWindowContainsTextDialog : FluentWindow
    {
        /// <summary>
        /// Gets the window identifier entered by the user.
        /// </summary>
        public string WindowIdentifier { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the identifier type selected by the user.
        /// Valid values: "Name", "AutomationId"
        /// </summary>
        public string IdentifierType { get; private set; } = "Name";

        /// <summary>
        /// Gets the search text entered by the user.
        /// </summary>
        public string SearchText { get; private set; } = string.Empty;

        /// <summary>
        /// Gets whether the search should be case sensitive.
        /// </summary>
        public bool CaseSensitive { get; private set; } = false;

        /// <summary>
        /// Gets whether to perform a deep search in all child elements.
        /// </summary>
        public bool DeepSearch { get; private set; } = true;

        public IfWindowContainsTextDialog()
        {
            InitializeComponent();
            WindowIdentifierTextBox.Focus();
            WindowIdentifierTextBox.TextChanged += InputTextBox_TextChanged;
            SearchTextTextBox.TextChanged += InputTextBox_TextChanged;
            UpdateOkButtonState();
        }

        /// <summary>
        /// Constructor for editing an existing action with pre-populated values.
        /// </summary>
        public IfWindowContainsTextDialog(string windowIdentifier, string identifierType, string searchText, bool caseSensitive, bool deepSearch)
        {
            InitializeComponent();

            // Pre-populate the form fields
            WindowIdentifierTextBox.Text = windowIdentifier;
            IdentifierTypeComboBox.SelectedIndex = GetIndexFromIdentifierType(identifierType);
            SearchTextTextBox.Text = searchText;
            CaseSensitiveCheckBox.IsChecked = caseSensitive;
            DeepSearchCheckBox.IsChecked = deepSearch;

            WindowIdentifierTextBox.Focus();
            WindowIdentifierTextBox.TextChanged += InputTextBox_TextChanged;
            SearchTextTextBox.TextChanged += InputTextBox_TextChanged;
            UpdateOkButtonState();
        }

        private void InputTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void UpdateOkButtonState()
        {
            var windowId = WindowIdentifierTextBox.Text?.Trim();
            var searchText = SearchTextTextBox.Text?.Trim();
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(windowId) && !string.IsNullOrWhiteSpace(searchText);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var windowId = WindowIdentifierTextBox.Text?.Trim();
            var searchText = SearchTextTextBox.Text?.Trim();

            // Validate inputs
            if (string.IsNullOrWhiteSpace(windowId))
            {
                System.Windows.MessageBox.Show("Please enter a window identifier.", "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                System.Windows.MessageBox.Show("Please enter search text.", "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Set results
            WindowIdentifier = windowId;
            IdentifierType = GetIdentifierTypeFromIndex(IdentifierTypeComboBox.SelectedIndex);
            SearchText = searchText;
            CaseSensitive = CaseSensitiveCheckBox.IsChecked ?? false;
            DeepSearch = DeepSearchCheckBox.IsChecked ?? true;

            // Close with success
            DialogResult = true;
            Close();
        }

        private static string GetIdentifierTypeFromIndex(int index)
        {
            return index switch
            {
                0 => "Name",
                1 => "AutomationId",
                _ => "Name"
            };
        }

        private static int GetIndexFromIdentifierType(string identifierType)
        {
            return identifierType switch
            {
                "Name" => 0,
                "AutomationId" => 1,
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
