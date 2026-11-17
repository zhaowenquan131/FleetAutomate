using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using FleetAutomate.Application.ActionConfiguration;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Actions.MouseAndKeyboard;
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
    private readonly Dictionary<string, FrameworkElement> _fieldLabels = new();
    private readonly IReadOnlyList<Variable> _availableVariables;
    private readonly IReadOnlyList<FleetAutomate.Helpers.SearchScopeOption> _scopeOptions;

    public IReadOnlyList<ActionConfigurationValue> Values { get; private set; } = [];

    public ActionConfigurationDialog(IAction action, ActionConfigurationSchema schema,
        IReadOnlyList<Variable>? availableVariables = null,
        IEnumerable<FleetAutomate.Helpers.SearchScopeOption>? scopeOptions = null)
    {
        InitializeComponent();

        _action = action;
        _schema = schema;
        _availableVariables = availableVariables ?? [];
        _scopeOptions = scopeOptions?.ToList() ?? [];

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
            _fieldLabels[field.PropertyName] = label;

            var control = CreateFieldControl(field);
            if (field.Kind is ActionConfigurationFieldKind.Text or ActionConfigurationFieldKind.MultilineText
                or ActionConfigurationFieldKind.FilePath or ActionConfigurationFieldKind.DirectoryPath
                or ActionConfigurationFieldKind.KeyboardShortcut)
            {
                _action.TextVariableBindings.TryGetValue(field.PropertyName, out var variable);
                control = new TextParameterInput(control, _availableVariables, variable);
            }
            control.Margin = new Thickness(0, 0, 0, 14);
            FieldsPanel.Children.Add(control);
            _fieldControls[field.PropertyName] = control;
        }

        if (_action is SendKeysAction && _fieldControls.TryGetValue(nameof(SendKeysAction.InputMode), out var modeControl) &&
            modeControl is System.Windows.Controls.ComboBox mode)
        {
            mode.SelectionChanged += (_, _) => UpdateKeyboardFields();
            UpdateKeyboardFields();
        }
    }

    private void UpdateKeyboardFields()
    {
        var shortcut = ReadRawValue(_schema.Fields.First(field => field.PropertyName == nameof(SendKeysAction.InputMode)))?.ToString() == nameof(KeyboardInputMode.Shortcut);
        SetFieldVisibility(nameof(SendKeysAction.Text), !shortcut);
        SetFieldVisibility(nameof(SendKeysAction.Wait), !shortcut);
        SetFieldVisibility(nameof(SendKeysAction.KeyCombination), shortcut);
    }

    private void SetFieldVisibility(string propertyName, bool visible)
    {
        _fieldControls[propertyName].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _fieldLabels[propertyName].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private FrameworkElement CreateFieldControl(ActionConfigurationField field)
    {
        var value = GetActionValue(field.PropertyName);
        if (field.PropertyName == nameof(UiElementActionBase.SearchScope))
        {
            return new System.Windows.Controls.ComboBox
            {
                IsEditable = true, ItemsSource = _scopeOptions,
                DisplayMemberPath = nameof(FleetAutomate.Helpers.SearchScopeOption.DisplayText),
                SelectedValuePath = nameof(FleetAutomate.Helpers.SearchScopeOption.Key),
                SelectedValue = value, Text = Convert.ToString(value) ?? string.Empty,
                MinHeight = 32
            };
        }
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
            ActionConfigurationFieldKind.KeyboardShortcut => new KeyboardShortcutInput
            {
                Value = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
            },
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
        return ActionTextParameters.GetLiteralValue(_action, property);
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
        var bindings = new Dictionary<string, string>(_action.TextVariableBindings, StringComparer.Ordinal);
        foreach (var field in _schema.Fields)
        {
            var visible = _fieldControls[field.PropertyName].Visibility == Visibility.Visible;
            if (_fieldControls[field.PropertyName] is TextParameterInput input)
            {
                if (!visible)
                {
                    bindings.Remove(field.PropertyName);
                    continue;
                }
                if (input.UsesVariable)
                {
                    if (!_availableVariables.Any(v => v.Name == input.VariableName))
                        throw new InvalidOperationException($"Select an available variable for {field.Label}.");
                    bindings[field.PropertyName] = input.VariableName;
                    continue;
                }
                bindings.Remove(field.PropertyName);
            }
            var rawValue = ReadRawValue(field);
            if (visible && field.IsRequired && rawValue is string text && string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException($"{field.Label} is required.");
            }

            if (visible && field.Kind == ActionConfigurationFieldKind.KeyboardShortcut)
            {
                rawValue = KeyboardKeys.Format(KeyboardKeys.Parse(rawValue as string));
            }

            values.Add(new ActionConfigurationValue(field.PropertyName, ConvertFieldValue(field, rawValue)));
        }

        if (bindings.Count != _action.TextVariableBindings.Count || bindings.Any(pair =>
            !_action.TextVariableBindings.TryGetValue(pair.Key, out var name) || name != pair.Value))
            values.Add(new ActionConfigurationValue(nameof(IAction.TextVariableBindings), bindings));

        return values;
    }

    private object? ReadRawValue(ActionConfigurationField field)
    {
        var control = _fieldControls[field.PropertyName];
        if (control is TextParameterInput input) control = input.LiteralEditor;
        return control switch
        {
            WpfTextBox textBox => textBox.Text,
            XPathInput xpathInput => xpathInput.XPath,
            KeyboardShortcutInput keyboardInput => keyboardInput.Value,
            WpfCheckBox checkBox => checkBox.IsChecked == true,
            System.Windows.Controls.ComboBox comboBox => comboBox.IsEditable
                ? comboBox.SelectedItem is FleetAutomate.Helpers.SearchScopeOption option ? option.Key : comboBox.Text
                : comboBox.SelectedItem?.ToString(),
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
