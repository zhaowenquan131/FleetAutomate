using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using FleetAutomate.Application.ActionConfiguration;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.View.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace FleetAutomate.View.Dialog;

public partial class ActionConfigurationDialog : Wpf.Ui.Controls.FluentWindow
{
    private readonly IAction _action;
    private readonly ActionConfigurationSchema _schema;
    private readonly Dictionary<string, FrameworkElement> _fieldControls = new();

    public IReadOnlyList<ActionConfigurationValue> Values { get; private set; } = [];

    public ActionConfigurationDialog(IAction action, ActionConfigurationSchema schema)
    {
        InitializeComponent();

        _action = action;
        _schema = schema;

        Title = schema.Title;
        HeaderTextBlock.Text = schema.Title;
        SubHeaderTextBlock.Text = action.Name;

        BuildFields();
    }

    private void BuildFields()
    {
        foreach (var field in _schema.Fields)
        {
            var label = new TextBlock
            {
                Text = field.IsRequired ? $"{field.Label} *" : field.Label,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            FieldsPanel.Children.Add(label);

            var control = CreateFieldControl(field);
            control.Margin = new Thickness(0, 0, 0, 14);
            FieldsPanel.Children.Add(control);
            _fieldControls[field.PropertyName] = control;
        }
    }

    private FrameworkElement CreateFieldControl(ActionConfigurationField field)
    {
        var value = GetActionValue(field.PropertyName);
        if (field.PropertyName == nameof(UiElementActionBase.ElementIdentifier))
        {
            return new XPathInput
            {
                XPath = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty,
                MinHeight = 72
            };
        }

        return field.Kind switch
        {
            ActionConfigurationFieldKind.Boolean => new WpfCheckBox
            {
                IsChecked = value as bool? ?? false,
                Content = field.Label
            },
            ActionConfigurationFieldKind.Enum => CreateComboBox(field, value),
            ActionConfigurationFieldKind.MultilineText => new WpfTextBox
            {
                Text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 96
            },
            ActionConfigurationFieldKind.FilePath => CreatePathInput(field, value, isDirectory: false),
            ActionConfigurationFieldKind.DirectoryPath => CreatePathInput(field, value, isDirectory: true),
            ActionConfigurationFieldKind.DateTimeOffset => new WpfTextBox
            {
                Text = value is DateTimeOffset dateTimeOffset && dateTimeOffset != default
                    ? dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)
                    : string.Empty
            },
            _ => new WpfTextBox
            {
                Text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
            }
        };
    }

    private System.Windows.Controls.ComboBox CreateComboBox(ActionConfigurationField field, object? value)
    {
        var comboBox = new System.Windows.Controls.ComboBox
        {
            IsEditable = false,
            MinHeight = 32
        };

        var options = field.Options ?? (field.ValueType?.IsEnum == true ? Enum.GetNames(field.ValueType) : []);
        foreach (var option in options)
        {
            comboBox.Items.Add(option);
        }

        var current = Convert.ToString(value, CultureInfo.InvariantCulture);
        comboBox.SelectedItem = !string.IsNullOrWhiteSpace(current) && comboBox.Items.Contains(current)
            ? current
            : comboBox.Items.Count > 0 ? comboBox.Items[0] : null;
        return comboBox;
    }

    private FrameworkElement CreatePathInput(ActionConfigurationField field, object? value, bool isDirectory)
    {
        var panel = new DockPanel();
        var button = new WpfButton
        {
            Content = "Browse...",
            MinWidth = 86,
            Margin = new Thickness(8, 0, 0, 0)
        };
        DockPanel.SetDock(button, Dock.Right);

        var textBox = new WpfTextBox
        {
            Text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
        };

        button.Click += (_, _) =>
        {
            if (isDirectory)
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    textBox.Text = dialog.SelectedPath;
                }
                return;
            }

            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = field.Label,
                CheckFileExists = false
            };
            if (fileDialog.ShowDialog(this) == true)
            {
                textBox.Text = fileDialog.FileName;
            }
        };

        panel.Children.Add(button);
        panel.Children.Add(textBox);
        return panel;
    }

    private object? GetActionValue(string propertyName)
    {
        var property = GetProperty(propertyName);
        return property.GetValue(_action);
    }

    private PropertyInfo GetProperty(string propertyName)
    {
        return _action.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Action property '{propertyName}' was not found.");
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Values = CollectValues();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Invalid Action Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private IReadOnlyList<ActionConfigurationValue> CollectValues()
    {
        var values = new List<ActionConfigurationValue>();
        foreach (var field in _schema.Fields)
        {
            var rawValue = ReadRawValue(field);
            if (field.IsRequired && rawValue is string text && string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException($"{field.Label} is required.");
            }

            values.Add(new ActionConfigurationValue(field.PropertyName, ConvertFieldValue(field, rawValue)));
        }

        return values;
    }

    private object? ReadRawValue(ActionConfigurationField field)
    {
        var control = _fieldControls[field.PropertyName];
        return control switch
        {
            WpfTextBox textBox => textBox.Text,
            XPathInput xpathInput => xpathInput.XPath,
            WpfCheckBox checkBox => checkBox.IsChecked == true,
            System.Windows.Controls.ComboBox comboBox => comboBox.SelectedItem?.ToString(),
            DockPanel dockPanel => dockPanel.Children.OfType<WpfTextBox>().FirstOrDefault()?.Text ?? string.Empty,
            _ => null
        };
    }

    private static object? ConvertFieldValue(ActionConfigurationField field, object? rawValue)
    {
        if (rawValue == null)
        {
            return null;
        }

        var text = rawValue as string;
        return field.Kind switch
        {
            ActionConfigurationFieldKind.Integer => int.Parse(text ?? string.Empty, CultureInfo.CurrentCulture),
            ActionConfigurationFieldKind.Double => double.Parse(text ?? string.Empty, CultureInfo.CurrentCulture),
            ActionConfigurationFieldKind.Boolean => rawValue,
            ActionConfigurationFieldKind.Enum when field.ValueType == typeof(string) => text,
            ActionConfigurationFieldKind.Enum when field.ValueType?.IsEnum == true => Enum.Parse(field.ValueType, text ?? string.Empty),
            ActionConfigurationFieldKind.DateTimeOffset => string.IsNullOrWhiteSpace(text)
                ? default(DateTimeOffset)
                : DateTimeOffset.Parse(text, CultureInfo.CurrentCulture),
            _ => text ?? Convert.ToString(rawValue, CultureInfo.CurrentCulture)
        };
    }
}
