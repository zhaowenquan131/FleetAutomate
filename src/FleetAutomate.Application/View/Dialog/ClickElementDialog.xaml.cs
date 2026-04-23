using System.Windows;
using System.Windows.Controls;
using FleetAutomate.Helpers;
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
        /// Gets whether Invoke should be dispatched without waiting for completion.
        /// </summary>
        public bool InvokeWithoutWaiting { get; private set; } = false;

        /// <summary>
        /// Gets the number of times to retry if the action fails.
        /// </summary>
        public int RetryTimes { get; private set; } = 3;

        /// <summary>
        /// Gets the delay in milliseconds between retry attempts.
        /// </summary>
        public int RetryDelayMilliseconds { get; private set; } = 500;

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

        public ClickElementDialog(IEnumerable<SearchScopeOption>? availableScopeKeys = null)
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

            UseInvokeCheckBox.Checked += UseInvokeCheckBox_CheckedChanged;
            UseInvokeCheckBox.Unchecked += UseInvokeCheckBox_CheckedChanged;
            UpdateInvokeOptions();
            UpdateOkButtonState();
            // Set button text for creating
            OkButton.Content = "Create";
        }

        /// <summary>
        /// Constructor for editing an existing ClickElementAction with pre-populated values.
        /// </summary>
        public ClickElementDialog(
            string elementIdentifier,
            string identifierType,
            bool isDoubleClick,
            bool useInvoke,
            bool invokeWithoutWaiting,
            int retryTimes,
            int retryDelayMilliseconds,
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
            DoubleClickCheckBox.IsChecked = isDoubleClick;
            UseInvokeCheckBox.IsChecked = useInvoke;
            InvokeWithoutWaitingCheckBox.IsChecked = invokeWithoutWaiting;
            RetryTimesTextBox.Text = retryTimes.ToString();
            RetryDelayTextBox.Text = retryDelayMilliseconds.ToString();
            AddElementToGlobalDictionary.IsChecked = addToGlobalDictionary;

            // Set search scope selection
            SelectSearchScope(searchScope);

            // Subscribe to property change to enable/disable OK button
            var xpathDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                View.Controls.XPathInput.XPathProperty,
                typeof(View.Controls.XPathInput));
            xpathDescriptor?.AddValueChanged(ElementIdentifierInput, (s, e) => UpdateOkButtonState());

            UseInvokeCheckBox.Checked += UseInvokeCheckBox_CheckedChanged;
            UseInvokeCheckBox.Unchecked += UseInvokeCheckBox_CheckedChanged;
            UpdateInvokeOptions();
            UpdateOkButtonState();
            // Set button text for editing
            OkButton.Content = "Save";
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

        private void UpdateOkButtonState()
        {
            var identifier = ElementIdentifierInput.XPath?.Trim();
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(identifier);
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
            InvokeWithoutWaiting = UseInvoke && (InvokeWithoutWaitingCheckBox.IsChecked ?? false);
            RetryTimes = retryTimes;
            RetryDelayMilliseconds = retryDelay;
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

        private void UseInvokeCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateInvokeOptions();
        }

        private void UpdateInvokeOptions()
        {
            var useInvoke = UseInvokeCheckBox.IsChecked ?? false;
            InvokeWithoutWaitingCheckBox.IsEnabled = useInvoke;
            if (!useInvoke)
            {
                InvokeWithoutWaitingCheckBox.IsChecked = false;
            }
        }
    }
}
