using System.Windows;
using System.Windows.Controls;
using FleetAutomate.Helpers;
using Wpf.Ui.Controls;

namespace FleetAutomate.View.Dialog
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

        /// <summary>
        /// Gets the search scope key (null or empty for desktop/full search).
        /// </summary>
        public string? SearchScope { get; private set; }

        /// <summary>
        /// Gets whether to add the found element to the global dictionary.
        /// </summary>
        public bool AddToGlobalDictionary { get; private set; } = false;

        /// <summary>
        /// Available scope keys from the global element dictionary.
        /// </summary>
        private readonly IEnumerable<SearchScopeOption> _availableScopeKeys;

        public WaitForElementDialog(IEnumerable<SearchScopeOption>? availableScopeKeys = null)
        {
            InitializeComponent();
            _availableScopeKeys = availableScopeKeys ?? [];
            ElementIdentifierInput.XPath = string.Empty;

            PopulateScopeComboBox();

            // Subscribe to property change to enable/disable OK button
            var xpathDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                View.Controls.XPathInput.XPathProperty,
                typeof(View.Controls.XPathInput));
            xpathDescriptor?.AddValueChanged(ElementIdentifierInput, (s, e) => UpdateOkButtonState());

            IdentifierTypeComboBox.SelectionChanged += IdentifierTypeComboBox_SelectionChanged;
            UpdateOkButtonState();
            UpdateHintText();
        }

        /// <summary>
        /// Constructor for editing an existing WaitForElementAction with pre-populated values.
        /// </summary>
        public WaitForElementDialog(
            string elementIdentifier,
            string identifierType,
            int timeoutMilliseconds,
            int pollingIntervalMilliseconds,
            string? searchScope = null,
            bool addToGlobalDictionary = false,
            IEnumerable<SearchScopeOption>? availableScopeKeys = null)
        {
            InitializeComponent();
            _availableScopeKeys = availableScopeKeys ?? [];

            PopulateScopeComboBox();

            // Pre-populate the form fields
            ElementIdentifierInput.XPath = elementIdentifier;
            IdentifierTypeComboBox.SelectedIndex = GetIndexFromIdentifierType(identifierType);
            TimeoutTextBox.Text = timeoutMilliseconds.ToString();
            PollingIntervalTextBox.Text = pollingIntervalMilliseconds.ToString();
            AddElementToGlobalDictionary.IsChecked = addToGlobalDictionary;

            // Set search scope selection
            SelectSearchScope(searchScope);

            // Subscribe to property change to enable/disable OK button
            var xpathDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                View.Controls.XPathInput.XPathProperty,
                typeof(View.Controls.XPathInput));
            xpathDescriptor?.AddValueChanged(ElementIdentifierInput, (s, e) => UpdateOkButtonState());

            IdentifierTypeComboBox.SelectionChanged += IdentifierTypeComboBox_SelectionChanged;
            UpdateOkButtonState();
            UpdateHintText();
        }

        private void PopulateScopeComboBox()
        {
            // Add available scope keys after the default "(Desktop - Full Search)" item
            foreach (var scopeOption in _availableScopeKeys)
            {
                SearchScopeComboBox.Items.Add(new ComboBoxItem { Content = scopeOption.DisplayText, Tag = scopeOption.Key });
            }
        }

        private void SelectSearchScope(string? searchScope)
        {
            if (string.IsNullOrEmpty(searchScope))
            {
                SearchScopeComboBox.SelectedIndex = 0; // Desktop
                return;
            }

            // Find and select the matching item
            for (int i = 1; i < SearchScopeComboBox.Items.Count; i++)
            {
                if (SearchScopeComboBox.Items[i] is ComboBoxItem item && item.Tag as string == searchScope)
                {
                    SearchScopeComboBox.SelectedIndex = i;
                    return;
                }
            }

            // If not found, select desktop
            SearchScopeComboBox.SelectedIndex = 0;
        }

        private string? GetSelectedSearchScope()
        {
            if (SearchScopeComboBox.SelectedIndex <= 0)
                return null; // Desktop / Full Search

            if (SearchScopeComboBox.SelectedItem is ComboBoxItem item)
                return item.Tag as string;

            return null;
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

        private void IdentifierTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateHintText();
        }

        private void UpdateOkButtonState()
        {
            var identifier = ElementIdentifierInput.XPath?.Trim();
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(identifier);
        }

        private void UpdateHintText()
        {
            var selectedIndex = IdentifierTypeComboBox.SelectedIndex;
            // Hint text is static, but could be dynamic based on selection
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var identifier = ElementIdentifierInput.XPath?.Trim();

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
            SearchScope = GetSelectedSearchScope();
            AddToGlobalDictionary = AddElementToGlobalDictionary.IsChecked ?? false;

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
