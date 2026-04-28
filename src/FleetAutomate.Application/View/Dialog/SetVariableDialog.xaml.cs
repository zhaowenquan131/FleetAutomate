using System.Text.RegularExpressions;
using System.Windows;
using FleetAutomate.Expressions;
using FleetAutomate.Model.Actions.Logic;
using Wpf.Ui.Controls;

namespace FleetAutomate.View.Dialog
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

        public SetVariableValueMode ValueMode { get; private set; } = SetVariableValueMode.Literal;

        public SetVariableDialog()
        {
            InitializeComponent();
            VariableNameTextBox.Focus();
            VariableNameTextBox.TextChanged += VariableNameTextBox_TextChanged;
            VariableTypeComboBox.SelectionChanged += VariableTypeComboBox_SelectionChanged;
            ValueModeComboBox.SelectionChanged += ValueModeComboBox_SelectionChanged;
            InitializeExpressionTemplates();
            UpdateOkButtonState();
            UpdateHintText();
            OkButton.Content = "创建";
        }

        public SetVariableDialog(
            string variableName,
            string variableType,
            string variableValue,
            SetVariableValueMode valueMode = SetVariableValueMode.Literal)
        {
            InitializeComponent();
            VariableNameTextBox.Text = variableName;
            VariableValueTextBox.Text = variableValue;
            VariableTypeComboBox.SelectedIndex = GetIndexFromType(variableType);
            ValueModeComboBox.SelectedIndex = valueMode == SetVariableValueMode.Expression ? 1 : 0;
            VariableNameTextBox.Focus();
            VariableNameTextBox.TextChanged += VariableNameTextBox_TextChanged;
            VariableTypeComboBox.SelectionChanged += VariableTypeComboBox_SelectionChanged;
            ValueModeComboBox.SelectionChanged += ValueModeComboBox_SelectionChanged;
            InitializeExpressionTemplates();
            UpdateOkButtonState();
            UpdateHintText();
            OkButton.Content = "保存";
        }

        private void InitializeExpressionTemplates()
        {
            ExpressionTemplateComboBox.ItemsSource = ExpressionTemplateCatalog.GetTemplates();
            ExpressionTemplateComboBox.SelectedIndex = 0;
        }

        private void VariableNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void VariableTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateHintText();
        }

        private void ValueModeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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
            var isExpression = ValueModeComboBox?.SelectedIndex == 1;
            VariableTypeComboBox.Visibility = isExpression ? Visibility.Collapsed : Visibility.Visible;
            VariableTypeLabelTextBlock.Visibility = isExpression ? Visibility.Collapsed : Visibility.Visible;
            ValueLabelTextBlock.Text = isExpression ? "Expression" : "Value";
            ExpressionTemplatePanel.Visibility = isExpression ? Visibility.Visible : Visibility.Collapsed;
            VariableValueTextBox.ToolTip = isExpression
                ? "Enter an expression. The variable type is inferred from the expression result."
                : "Enter the initial value for the variable";

            HintTextBlock.Text = isExpression
                ? "Type is inferred automatically. Numbers currently infer as double."
                : selectedIndex switch
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
            var mode = ValueModeComboBox.SelectedIndex == 1 ? SetVariableValueMode.Expression : SetVariableValueMode.Literal;
            if (mode == SetVariableValueMode.Expression)
            {
                var validation = new SimpleExpressionEngine().Validate(variableValue, ExpressionContext.Empty);
                if (!validation.IsValid)
                {
                    await ShowErrorAsync("Invalid Expression", string.Join(global::System.Environment.NewLine, validation.Errors));
                    return;
                }
            }
            else if (!await ValidateValueForTypeAsync(variableValue, typeIndex))
            {
                return;  // Error message shown in validation method
            }

            // Set results
            VariableName = variableName;
            VariableValue = variableValue;
            VariableType = GetTypeFromIndex(typeIndex);
            ValueMode = mode;

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

        private void InsertTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ExpressionTemplateComboBox.SelectedValue is not string templateId)
            {
                return;
            }

            var result = ExpressionTemplateCatalog.InsertTemplate(
                VariableValueTextBox.Text ?? string.Empty,
                VariableValueTextBox.CaretIndex,
                templateId);
            VariableValueTextBox.Text = result.Text;
            VariableValueTextBox.CaretIndex = result.CaretIndex;
            VariableValueTextBox.Focus();
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

        private static int GetIndexFromType(string variableType)
        {
            return variableType switch
            {
                "int" => 0,
                "double" => 1,
                "string" => 2,
                "bool" => 3,
                _ => 2
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
