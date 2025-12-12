using System.Text.RegularExpressions;
using System.Windows;
using Wpf.Ui.Controls;

namespace FleetAutomate.Dialogs
{
    /// <summary>
    /// Interaction logic for SetVariableDialog.xaml
    /// </summary>
    public partial class SetVariableDialog : FluentWindow
    {
        /// <summary>
        /// Gets the variable name entered by the user.
        /// </summary>
        public string VariableName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the variable type selected by the user.
        /// Valid values: "int", "double", "string", "bool"
        /// </summary>
        public string VariableType { get; private set; } = "int";

        /// <summary>
        /// Gets the variable value entered by the user.
        /// </summary>
        public string VariableValue { get; private set; } = string.Empty;

        public SetVariableDialog()
        {
            InitializeComponent();
            VariableNameTextBox.Focus();
            VariableNameTextBox.TextChanged += VariableNameTextBox_TextChanged;
            VariableTypeComboBox.SelectionChanged += VariableTypeComboBox_SelectionChanged;
            UpdateOkButtonState();
            UpdateHintText();
        }

        private void VariableNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void VariableTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateHintText();
        }

        private void UpdateOkButtonState()
        {
            var variableName = VariableNameTextBox.Text?.Trim();
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(variableName) && IsValidVariableName(variableName);
        }

        private void UpdateHintText()
        {
            var selectedIndex = VariableTypeComboBox.SelectedIndex;
            HintTextBlock.Text = selectedIndex switch
            {
                0 => "Enter an integer value (e.g., 42)",
                1 => "Enter a decimal value (e.g., 3.14)",
                2 => "Enter any text value",
                3 => "Enter true or false",
                _ => "Enter a value"
            };
        }

        private static bool IsValidVariableName(string name)
        {
            // Variable name must start with letter or underscore, followed by alphanumeric or underscore
            return Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
        }

        private async void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var variableName = VariableNameTextBox.Text?.Trim();
            var variableValue = VariableValueTextBox.Text?.Trim() ?? string.Empty;

            // Validate variable name
            if (string.IsNullOrWhiteSpace(variableName))
            {
                await ShowErrorAsync("Invalid Input", "Please enter a variable name.");
                return;
            }

            if (!IsValidVariableName(variableName))
            {
                await ShowErrorAsync("Invalid Name", "Variable name must start with a letter or underscore, followed by alphanumeric characters or underscores.");
                return;
            }

            // Validate value based on type
            var typeIndex = VariableTypeComboBox.SelectedIndex;
            if (!await ValidateValueForTypeAsync(variableValue, typeIndex))
            {
                return;  // Error message shown in validation method
            }

            // Set results
            VariableName = variableName;
            VariableValue = variableValue;
            VariableType = GetTypeFromIndex(typeIndex);

            // Close with success
            DialogResult = true;
            Close();
        }

        private async Task<bool> ValidateValueForTypeAsync(string value, int typeIndex)
        {
            // Allow empty values (will use default)
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return typeIndex switch
            {
                0 => await ValidateIntegerAsync(value),
                1 => await ValidateDoubleAsync(value),
                2 => true,  // Text accepts anything
                3 => await ValidateBooleanAsync(value),
                _ => false
            };
        }

        private async Task<bool> ValidateIntegerAsync(string value)
        {
            if (int.TryParse(value, out _))
            {
                return true;
            }

            await ShowErrorAsync("Invalid Value", "Please enter a valid integer value.");
            return false;
        }

        private async Task<bool> ValidateDoubleAsync(string value)
        {
            if (double.TryParse(value, out _))
            {
                return true;
            }

            await ShowErrorAsync("Invalid Value", "Please enter a valid decimal value.");
            return false;
        }

        private async Task<bool> ValidateBooleanAsync(string value)
        {
            if (value.Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
                value.Equals("false", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            await ShowErrorAsync("Invalid Value", "Please enter 'true' or 'false'.");
            return false;
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

        private static string GetTypeFromIndex(int index)
        {
            return index switch
            {
                0 => "int",
                1 => "double",
                2 => "string",
                3 => "bool",
                _ => "string"
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
