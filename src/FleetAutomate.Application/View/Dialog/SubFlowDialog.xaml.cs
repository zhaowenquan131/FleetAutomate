using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Wpf.Ui.Controls;

namespace FleetAutomate.View.Dialog
{
    /// <summary>
    /// Interaction logic for SubFlowDialog.xaml
    /// </summary>
    public partial class SubFlowDialog : FluentWindow
    {
        /// <summary>
        /// Gets the selected flow name.
        /// </summary>
        public string? SelectedFlowName { get; private set; }

        public SubFlowDialog(IEnumerable<string> availableFlows)
        {
            InitializeComponent();

            // Populate ComboBox with available flows
            var flowsList = availableFlows.ToList();
            FlowComboBox.ItemsSource = flowsList;

            // Select first item by default if available
            if (flowsList.Count > 0)
            {
                FlowComboBox.SelectedIndex = 0;
            }

            // Update OK button state
            FlowComboBox.SelectionChanged += (s, e) => UpdateOkButtonState();
            UpdateOkButtonState();

            // Focus on ComboBox
            FlowComboBox.Focus();
            OkButton.Content = "创建";
        }

        public SubFlowDialog(IEnumerable<string> availableFlows, string? selectedFlowName)
            : this(availableFlows)
        {
            if (!string.IsNullOrWhiteSpace(selectedFlowName) && FlowComboBox.Items.Contains(selectedFlowName))
            {
                FlowComboBox.SelectedItem = selectedFlowName;
            }

            OkButton.Content = "保存";
        }

        private void UpdateOkButtonState()
        {
            OkButton.IsEnabled = FlowComboBox.SelectedItem != null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedFlowName = FlowComboBox.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(SelectedFlowName))
            {
                _ = ShowErrorAsync("No Selection", "Please select a flow to execute.");
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
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
    }
}
