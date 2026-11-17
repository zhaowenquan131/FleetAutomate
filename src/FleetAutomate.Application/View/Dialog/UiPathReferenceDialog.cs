using System.Windows;
using System.Windows.Controls;
using FleetAutomate.Model.Project;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace FleetAutomate.View.Dialog;

public sealed class UiPathReferenceDialog : Wpf.Ui.Controls.FluentWindow
{
    public UiPathReferenceDialog(UiPathReference? reference, Action<string, string> save, string? capturedXPath = null)
    {
        Title = reference == null ? "New UI Path Reference" : "Edit UI Path Reference";
        Width = 800;
        Height = 400;
        MinWidth = 600;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        var shell = new DockPanel();
        var titleBar = new Wpf.Ui.Controls.TitleBar { Title = Title, ShowMinimize = false, ShowMaximize = false };
        DockPanel.SetDock(titleBar, Dock.Top);
        shell.Children.Add(titleBar);
        var panel = new DockPanel { Margin = new Thickness(20) };
        var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var ok = new WpfButton { Content = "Save", MinWidth = 90, Margin = new Thickness(0, 0, 10, 0) };
        var cancel = new WpfButton { Content = "Cancel", MinWidth = 90, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        var fields = new StackPanel();
        fields.Children.Add(new TextBlock { Text = "Name", Margin = new Thickness(0, 0, 0, 5) });
        var name = new WpfTextBox { Text = reference?.Name ?? string.Empty, Margin = new Thickness(0, 0, 0, 16) };
        fields.Children.Add(name);
        fields.Children.Add(new TextBlock { Text = "XPath", Margin = new Thickness(0, 0, 0, 5) });
        var xpath = new WpfTextBox
        {
            Text = reference?.XPath ?? capturedXPath ?? string.Empty,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 60, MaxHeight = 100
        };
        fields.Children.Add(xpath);
        var capture = new WpfButton { Content = "Capture Element...", HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        capture.Click += (_, _) =>
        {
            var dialog = new ElementCaptureDialog { Owner = this, UseButtonText = "Use UI path" };
            if (dialog.ShowDialog() == true) xpath.Text = dialog.CapturedXPath;
        };
        fields.Children.Add(capture);
        panel.Children.Add(fields);
        shell.Children.Add(panel);
        Content = shell;
        ok.Click += (_, _) =>
        {
            try
            {
                save(name.Text, xpath.Text);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, "Invalid UI Path", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
    }
}
