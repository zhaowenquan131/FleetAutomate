using System.Windows;
using FleetAutomate.Model;
using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Actions.System;

namespace FleetAutomate.Dialogs
{
    public partial class ActionPropertiesDialog : Wpf.Ui.Controls.FluentWindow
    {
        private readonly IAction _action;
        public string ActionName { get; set; }
        public bool DialogResultOk { get; private set; }

        public ActionPropertiesDialog(IAction action)
        {
            InitializeComponent();
            _action = action;
            ActionName = action.Name;
            DataContext = this;

            LoadActionProperties();
        }

        private void LoadActionProperties()
        {
            // Load common properties
            DescriptionTextBox.Text = _action.Description;
            IsEnabledCheckBox.IsChecked = _action.IsEnabled;

            // Load type-specific properties
            switch (_action)
            {
                case WaitForElementAction waitAction:
                    LoadWaitForElementProperties(waitAction);
                    break;

                case ClickElementAction clickAction:
                    LoadClickElementProperties(clickAction);
                    break;

                case LaunchApplicationAction launchAction:
                    LoadLaunchApplicationProperties(launchAction);
                    break;
            }
        }

        private void LoadWaitForElementProperties(WaitForElementAction action)
        {
            UIAutomationPanel.Visibility = Visibility.Visible;
            WaitForElementPanel.Visibility = Visibility.Visible;

            IdentifierTypeComboBox.SelectedItem = IdentifierTypeComboBox.Items
                .Cast<System.Windows.Controls.ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString() == action.IdentifierType);

            ElementIdentifierTextBox.Text = action.ElementIdentifier;
            TimeoutTextBox.Text = action.TimeoutMilliseconds.ToString();
            PollingIntervalTextBox.Text = action.PollingIntervalMilliseconds.ToString();
        }

        private void LoadClickElementProperties(ClickElementAction action)
        {
            UIAutomationPanel.Visibility = Visibility.Visible;
            ClickElementPanel.Visibility = Visibility.Visible;

            IdentifierTypeComboBox.SelectedItem = IdentifierTypeComboBox.Items
                .Cast<System.Windows.Controls.ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString() == action.IdentifierType);

            ElementIdentifierTextBox.Text = action.ElementIdentifier;
            IsDoubleClickCheckBox.IsChecked = action.IsDoubleClick;
        }

        private void LoadLaunchApplicationProperties(LaunchApplicationAction action)
        {
            LaunchAppPanel.Visibility = Visibility.Visible;

            ExecutablePathTextBox.Text = action.ExecutablePath;
            ArgumentsTextBox.Text = action.Arguments;
            WorkingDirectoryTextBox.Text = action.WorkingDirectory;
            WaitForCompletionCheckBox.IsChecked = action.WaitForCompletion;
            LaunchTimeoutTextBox.Text = action.TimeoutMilliseconds.ToString();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (SaveActionProperties())
            {
                DialogResultOk = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResultOk = false;
            Close();
        }

        private bool SaveActionProperties()
        {
            try
            {
                // Save type-specific properties (including Description)
                switch (_action)
                {
                    case WaitForElementAction waitAction:
                        return SaveWaitForElementProperties(waitAction);

                    case ClickElementAction clickAction:
                        return SaveClickElementProperties(clickAction);

                    case LaunchApplicationAction launchAction:
                        return SaveLaunchApplicationProperties(launchAction);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving properties: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool SaveWaitForElementProperties(WaitForElementAction action)
        {
            // Save description
            action.Description = DescriptionTextBox.Text;

            var selectedItem = IdentifierTypeComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (selectedItem != null)
            {
                action.IdentifierType = selectedItem.Content.ToString() ?? "XPath";
            }

            action.ElementIdentifier = ElementIdentifierTextBox.Text;

            if (int.TryParse(TimeoutTextBox.Text, out int timeout))
            {
                action.TimeoutMilliseconds = timeout;
            }
            else
            {
                System.Windows.MessageBox.Show("Invalid timeout value", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (int.TryParse(PollingIntervalTextBox.Text, out int interval))
            {
                action.PollingIntervalMilliseconds = interval;
            }
            else
            {
                System.Windows.MessageBox.Show("Invalid polling interval value", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool SaveClickElementProperties(ClickElementAction action)
        {
            // Save description
            action.Description = DescriptionTextBox.Text;

            var selectedItem = IdentifierTypeComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (selectedItem != null)
            {
                action.IdentifierType = selectedItem.Content.ToString() ?? "XPath";
            }

            action.ElementIdentifier = ElementIdentifierTextBox.Text;
            action.IsDoubleClick = IsDoubleClickCheckBox.IsChecked ?? false;

            return true;
        }

        private bool SaveLaunchApplicationProperties(LaunchApplicationAction action)
        {
            // Save description
            action.Description = DescriptionTextBox.Text;

            action.ExecutablePath = ExecutablePathTextBox.Text;
            action.Arguments = ArgumentsTextBox.Text;
            action.WorkingDirectory = WorkingDirectoryTextBox.Text;
            action.WaitForCompletion = WaitForCompletionCheckBox.IsChecked ?? false;

            if (int.TryParse(LaunchTimeoutTextBox.Text, out int timeout))
            {
                action.TimeoutMilliseconds = timeout;
            }
            else
            {
                System.Windows.MessageBox.Show("Invalid timeout value", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}
