using System.Windows;
using System.Windows.Controls;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace FleetAutomate.View.Controls;

/// <summary>Keeps the existing literal editor, including capture/browse, alongside a variable source.</summary>
public sealed class TextParameterInput : StackPanel
{
    private readonly WpfComboBox _source;
    private readonly WpfComboBox _variables;
    public FrameworkElement LiteralEditor { get; }
    public bool UsesVariable => _source.SelectedIndex == 1;
    public string VariableName => _variables.SelectedItem as string ?? string.Empty;

    public TextParameterInput(FrameworkElement literalEditor, IReadOnlyList<Variable> variables, string? selectedVariable)
    {
        LiteralEditor = literalEditor;
        var header = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        _source = new WpfComboBox
        {
            ItemsSource = new[] { "Text", "Variable" },
            MinHeight = 30
        };
        _variables = new WpfComboBox
        {
            ItemsSource = variables.Select(v => v.Name).Distinct(StringComparer.Ordinal).ToList(),
            MinHeight = 30, Margin = new Thickness(8, 0, 0, 0),
            ToolTip = "Variables available before this action"
        };
        // Preserve a removed/renamed reference for correction, without offering it as an available variable.
        if (!string.IsNullOrEmpty(selectedVariable) && !variables.Any(v => v.Name == selectedVariable))
        {
            _variables.IsEditable = true;
            _variables.IsReadOnly = true;
            _variables.Text = selectedVariable;
            _variables.ToolTip = $"Variable '{selectedVariable}' is no longer available here. Select another variable.";
        }
        else _variables.SelectedItem = selectedVariable;
        Grid.SetColumn(_variables, 1);
        header.Children.Add(_source);
        header.Children.Add(_variables);
        Children.Add(header);
        Children.Add(literalEditor);
        _source.SelectionChanged += (_, _) => UpdateSource();
        _source.SelectedIndex = string.IsNullOrEmpty(selectedVariable) ? 0 : 1;
    }

    private void UpdateSource()
    {
        LiteralEditor.Visibility = UsesVariable ? Visibility.Collapsed : Visibility.Visible;
        _variables.Visibility = UsesVariable ? Visibility.Visible : Visibility.Collapsed;
    }
}
