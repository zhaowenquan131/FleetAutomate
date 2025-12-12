using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace FleetAutomate.Dialogs
{
    /// <summary>
    /// Interaction logic for IfActionDialog.xaml
    /// </summary>
    public partial class IfActionDialog : FluentWindow
    {
        /// <summary>
        /// Gets the condition expression entered by the user.
        /// </summary>
        public string ConditionExpression { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the condition type selected by the user.
        /// </summary>
        public string ConditionType { get; private set; } = "Expression";

        /// <summary>
        /// Gets the element identifier (for UIElement Exists condition).
        /// </summary>
        public string ElementIdentifier { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the identifier type (for UIElement Exists condition).
        /// </summary>
        public string IdentifierType { get; private set; } = "XPath";

        /// <summary>
        /// Gets the retry times (for UIElement Exists condition).
        /// </summary>
        public int RetryTimes { get; private set; } = 1;

        public IfActionDialog()
        {
            InitializeComponent();
            ConditionTextBox.Focus();
            ConditionTextBox.TextChanged += ConditionTextBox_TextChanged;
            ElementIdentifierTextBox.TextChanged += ElementIdentifierTextBox_TextChanged;
            UpdateOkButtonState();
        }

        /// <summary>
        /// Constructor for editing an existing IfAction.
        /// </summary>
        public IfActionDialog(string conditionType, string conditionExpression, string elementIdentifier, string identifierType, int retryTimes)
        {
            InitializeComponent();

            // Set the condition type
            if (conditionType == "UIElementExists")
            {
                // Select UIElementExists type
                ConditionTypeComboBox.SelectedIndex = 1;

                // Pre-populate values
                ElementIdentifierTextBox.Text = elementIdentifier;
                RetryTimesTextBox.Text = retryTimes.ToString();

                // Select identifier type
                for (int i = 0; i < IdentifierTypeComboBox.Items.Count; i++)
                {
                    if (IdentifierTypeComboBox.Items[i] is ComboBoxItem item &&
                        item.Content?.ToString() == identifierType)
                    {
                        IdentifierTypeComboBox.SelectedIndex = i;
                        break;
                    }
                }

                ElementIdentifierTextBox.Focus();
            }
            else
            {
                // Select Expression type (default)
                ConditionTypeComboBox.SelectedIndex = 0;
                ConditionTextBox.Text = conditionExpression;
                ConditionTextBox.Focus();
            }

            ConditionTextBox.TextChanged += ConditionTextBox_TextChanged;
            ElementIdentifierTextBox.TextChanged += ElementIdentifierTextBox_TextChanged;
            UpdateOkButtonState();
        }

        private void ConditionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConditionTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var conditionType = selectedItem.Tag?.ToString() ?? "Expression";

                // Check if panels are initialized (they may be null during InitializeComponent)
                if (ExpressionPanel == null || UIElementPanel == null)
                {
                    return;
                }

                // Show/hide appropriate panel
                if (conditionType == "Expression")
                {
                    ExpressionPanel.Visibility = Visibility.Visible;
                    UIElementPanel.Visibility = Visibility.Collapsed;
                    ConditionTextBox?.Focus();
                }
                else if (conditionType == "UIElementExists")
                {
                    ExpressionPanel.Visibility = Visibility.Collapsed;
                    UIElementPanel.Visibility = Visibility.Visible;
                    ElementIdentifierTextBox?.Focus();
                }

                UpdateOkButtonState();
            }
        }

        private void ConditionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void ElementIdentifierTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void UpdateOkButtonState()
        {
            if (ConditionTypeComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                var conditionType = selectedItem.Tag?.ToString() ?? "Expression";

                if (conditionType == "Expression")
                {
                    var condition = ConditionTextBox?.Text?.Trim();
                    OkButton.IsEnabled = !string.IsNullOrWhiteSpace(condition);
                }
                else if (conditionType == "UIElementExists")
                {
                    var identifier = ElementIdentifierTextBox?.Text?.Trim();
                    OkButton.IsEnabled = !string.IsNullOrWhiteSpace(identifier);
                }
            }
        }

        private async void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConditionTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var conditionType = selectedItem.Tag?.ToString() ?? "Expression";
                ConditionType = conditionType;

                if (conditionType == "Expression")
                {
                    if (!await ValidateAndSetExpressionCondition())
                    {
                        return;
                    }
                }
                else if (conditionType == "UIElementExists")
                {
                    if (!ValidateAndSetUIElementCondition())
                    {
                        return;
                    }
                }
            }

            // Close with success
            DialogResult = true;
            Close();
        }

        private async Task<bool> ValidateAndSetExpressionCondition()
        {
            var condition = ConditionTextBox.Text?.Trim();

            // Validate condition
            if (string.IsNullOrWhiteSpace(condition))
            {
                await ShowErrorAsync("Invalid Input", "Please enter a condition.");
                return false;
            }

            // Try to parse the condition
            var parsedCondition = Model.Actions.Logic.Expression.BooleanExpressionParser.Parse(condition);
            if (parsedCondition == null)
            {
                ErrorTextBlock.Text = "Invalid boolean expression. Use comparison operators (>, <, >=, <=) or literal values (true, false).";
                ErrorTextBlock.Visibility = Visibility.Visible;
                return false;
            }

            // Clear error
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            // Set results
            ConditionExpression = condition;
            return true;
        }

        private bool ValidateAndSetUIElementCondition()
        {
            var identifier = ElementIdentifierTextBox.Text?.Trim();

            // Validate identifier
            if (string.IsNullOrWhiteSpace(identifier))
            {
                ErrorTextBlock.Text = "Please enter an element identifier.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                return false;
            }

            // Validate and parse retry times
            var retryTimesText = RetryTimesTextBox.Text?.Trim();
            if (!int.TryParse(retryTimesText, out int retryTimes) || retryTimes < 1)
            {
                ErrorTextBlock.Text = "Retry times must be a positive integer (minimum 1).";
                ErrorTextBlock.Visibility = Visibility.Visible;
                return false;
            }

            // Clear error
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            // Set results
            ElementIdentifier = identifier;
            RetryTimes = retryTimes;

            if (IdentifierTypeComboBox.SelectedItem is ComboBoxItem identifierTypeItem)
            {
                IdentifierType = identifierTypeItem.Content?.ToString() ?? "XPath";
            }

            return true;
        }

        private async Task ShowErrorAsync(string title, string message)
        {
            var messageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            await messageBox.ShowDialogAsync();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
