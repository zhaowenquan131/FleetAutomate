using System.Windows;

namespace FleetAutomate.Dialogs
{
    public partial class TestFlowNameDialog : Window
    {
        public string TestFlowName { get; private set; } = string.Empty;
        public string TestFlowDescription { get; private set; } = string.Empty;

        public TestFlowNameDialog()
        {
            InitializeComponent();
            TestFlowNameTextBox.Focus();
            TestFlowNameTextBox.TextChanged += TestFlowNameTextBox_TextChanged;
            UpdateOkButtonState();
        }

        private void TestFlowNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateOkButtonState();
        }

        private void UpdateOkButtonState()
        {
            var testFlowName = TestFlowNameTextBox.Text?.Trim();
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(testFlowName);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var testFlowName = TestFlowNameTextBox.Text?.Trim();
            
            if (string.IsNullOrWhiteSpace(testFlowName))
            {
                System.Windows.MessageBox.Show("Please enter a TestFlow name.", "Invalid Input", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TestFlowName = testFlowName;
            TestFlowDescription = DescriptionTextBox.Text?.Trim() ?? string.Empty;
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}