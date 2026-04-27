using System.Windows;
using FleetAutomate.Model.Actions.System;
using Wpf.Ui.Controls;

namespace FleetAutomate.View.Dialog
{
    public partial class WaitDurationDialog : FluentWindow
    {
        public int Duration { get; private set; } = 1;

        public WaitDurationUnit Unit { get; private set; } = WaitDurationUnit.Seconds;

        public WaitDurationDialog()
        {
            InitializeComponent();
            DurationTextBox.Focus();
            DurationTextBox.SelectAll();
            OkButton.Content = "Create";
        }

        public WaitDurationDialog(int duration, WaitDurationUnit unit)
            : this()
        {
            DurationTextBox.Text = Math.Max(1, duration).ToString();
            UnitComboBox.SelectedIndex = unit == WaitDurationUnit.Minutes ? 1 : 0;
            OkButton.Content = "Save";
            Title = "Edit Wait";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(DurationTextBox.Text?.Trim(), out var duration) || duration < 1)
            {
                System.Windows.MessageBox.Show(
                    "Please enter a positive whole number for the duration.",
                    "Invalid Duration",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            Duration = duration;
            Unit = UnitComboBox.SelectedIndex == 1 ? WaitDurationUnit.Minutes : WaitDurationUnit.Seconds;
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
