using System.Windows;
using FleetAutomate.Expressions;
using FleetAutomate.Model.Actions.System;
using Wpf.Ui.Controls;
using LogLevel = FleetAutomate.Model.Actions.System.LogLevel;

namespace FleetAutomate.View.Dialog
{
    /// <summary>
    /// Interaction logic for LogActionDialog.xaml
    /// </summary>
    public partial class LogActionDialog : FluentWindow
    {
        /// <summary>
        /// Gets the log level selected by the user.
        /// </summary>
        public LogLevel LogLevel { get; private set; } = LogLevel.Info;

        /// <summary>
        /// Gets the message entered by the user.
        /// </summary>
        public string Message { get; private set; } = string.Empty;

        public LogMessageMode MessageMode { get; private set; } = LogMessageMode.Literal;

        public LogActionDialog()
        {
            InitializeComponent();
            MessageModeComboBox.SelectionChanged += MessageModeComboBox_SelectionChanged;
            InitializeExpressionTemplates();
            MessageTextBox.Focus();
            MessageTextBox.TextChanged += MessageTextBox_TextChanged;
            UpdateOkButtonState();
            UpdateModeUi();
            // Set button text for creating
            OkButton.Content = "Create";
        }

        /// <summary>
        /// Constructor for editing an existing LogAction.
        /// </summary>
        /// <param name="logLevel">Current log level</param>
        /// <param name="message">Current message</param>
        public LogActionDialog(LogLevel logLevel, string message, LogMessageMode messageMode = LogMessageMode.Literal)
            : this()
        {
            // Pre-populate fields with existing values
            LogLevelComboBox.SelectedIndex = (int)logLevel;
            MessageTextBox.Text = message ?? string.Empty;
            MessageModeComboBox.SelectedIndex = messageMode == LogMessageMode.Expression ? 1 : 0;
            UpdateModeUi();

            // Update button text for editing
            OkButton.Content = "Save";

            // Update window title for editing
            Title = "Edit Log Message";
        }

        private void MessageTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void InitializeExpressionTemplates()
        {
            ExpressionTemplateComboBox.ItemsSource = ExpressionTemplateCatalog.GetTemplates();
            ExpressionTemplateComboBox.SelectedIndex = 0;
        }

        private void MessageModeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateModeUi();
        }

        private void UpdateModeUi()
        {
            var isExpression = MessageModeComboBox?.SelectedIndex == 1;
            ExpressionTemplatePanel.Visibility = isExpression ? Visibility.Visible : Visibility.Collapsed;
            MessageLabelTextBlock.Text = isExpression ? "Expression" : "Message";
            MessageTextBox.ToolTip = isExpression
                ? "Expression to evaluate and log. Use templates or type manually."
                : "Message to log. Use {variableName} to insert variable values.";
            HintTextBlock.Text = isExpression
                ? "Expression mode supports variables, UI/date/text functions, parentheses, and chained text methods."
                : "Tip: Use {variableName} to insert variable values in the message.";
        }

        private void UpdateOkButtonState()
        {
            var message = MessageTextBox.Text?.Trim();
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(message);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var message = MessageTextBox.Text?.Trim();

            // Validate message
            if (string.IsNullOrWhiteSpace(message))
            {
                System.Windows.MessageBox.Show("Please enter a message to log.", "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Set results
            LogLevel = (LogLevel)LogLevelComboBox.SelectedIndex;
            Message = message;
            MessageMode = MessageModeComboBox.SelectedIndex == 1 ? LogMessageMode.Expression : LogMessageMode.Literal;

            // Close with success
            DialogResult = true;
            Close();
        }

        private void InsertTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ExpressionTemplateComboBox.SelectedValue is not string templateId)
            {
                return;
            }

            var originalText = MessageTextBox.Text ?? string.Empty;
            var template = ExpressionTemplateCatalog.GetTemplates()
                .First(t => string.Equals(t.Id, templateId, StringComparison.Ordinal));
            var insertAt = Math.Clamp(MessageTextBox.CaretIndex, 0, originalText.Length);
            var interpolatedTemplate = "{" + template.TemplateText + "}";
            MessageTextBox.Text = originalText.Insert(insertAt, interpolatedTemplate);
            MessageTextBox.CaretIndex = insertAt + interpolatedTemplate.Length;
            MessageTextBox.Focus();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
