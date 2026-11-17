using System.Windows;
using System.Windows.Controls;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Project;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace FleetAutomate.View.Controls;

/// <summary>Keeps the existing literal editor, including capture/browse, alongside a variable source.</summary>
public sealed class TextParameterInput : StackPanel
{
    private readonly WpfComboBox _source;
    private readonly WpfComboBox _variables;
    private readonly WpfComboBox _paths;
    private readonly TextBlock _pathPreview;
    public FrameworkElement LiteralEditor { get; }
    public bool UsesVariable => _source.SelectedIndex == 1;
    public bool UsesUiPathReference => _source.SelectedIndex == 2;
    public string VariableName => _variables.SelectedItem as string ?? string.Empty;
    public UiPathReference? SelectedUiPathReference => _paths.SelectedItem as UiPathReference;
    public event EventHandler? SourceChanged;

    public TextParameterInput(FrameworkElement literalEditor, IReadOnlyList<Variable> variables, string? selectedVariable,
        IReadOnlyList<UiPathReference>? uiPaths = null)
    {
        LiteralEditor = literalEditor;
        var header = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(uiPaths == null ? 118 : 190) });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        _source = new WpfComboBox
        {
            ItemsSource = uiPaths == null ? new[] { "Text", "Variable" } : new[] { "Text", "Variable", "UI path reference" },
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
        _paths = new WpfComboBox
        {
            ItemsSource = uiPaths?.OrderBy(path => path.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            DisplayMemberPath = nameof(UiPathReference.Name),
            MinHeight = 30, Margin = new Thickness(8, 0, 0, 0),
            ToolTip = "Select a UI path from the current project"
        };
        _pathPreview = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
        _pathPreview.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var savedReference = literalEditor is XPathInput xpath && UiPathReferenceResolver.IsReference(xpath.XPath)
            ? xpath.XPath.Trim()[2..^1] : null;
        if (savedReference != null && uiPaths != null)
        {
            _paths.SelectedItem = uiPaths.FirstOrDefault(path => string.Equals(path.Name, savedReference, StringComparison.OrdinalIgnoreCase));
            if (_paths.SelectedItem == null)
            {
                _paths.IsEditable = true;
                _paths.IsReadOnly = true;
                _paths.Text = savedReference;
            }
        }
        _paths.SelectionChanged += (_, _) => UpdateSource();
        Grid.SetColumn(_variables, 1);
        Grid.SetColumn(_paths, 1);
        header.Children.Add(_source);
        header.Children.Add(_variables);
        header.Children.Add(_paths);
        Children.Add(header);
        Children.Add(_pathPreview);
        Children.Add(literalEditor);
        _source.SelectionChanged += (_, _) => UpdateSource();
        _source.SelectedIndex = !string.IsNullOrEmpty(selectedVariable) ? 1 : savedReference != null && uiPaths != null ? 2 : 0;
    }

    private void UpdateSource()
    {
        LiteralEditor.Visibility = UsesVariable || UsesUiPathReference ? Visibility.Collapsed : Visibility.Visible;
        _variables.Visibility = UsesVariable ? Visibility.Visible : Visibility.Collapsed;
        _paths.Visibility = UsesUiPathReference ? Visibility.Visible : Visibility.Collapsed;
        _pathPreview.Visibility = UsesUiPathReference ? Visibility.Visible : Visibility.Collapsed;
        _pathPreview.Text = SelectedUiPathReference?.XPath ?? (_paths.Items.Count == 0
            ? "No project UI paths. Add one under UI path references."
            : "Select an available project UI path reference.");
        SourceChanged?.Invoke(this, EventArgs.Empty);
    }
}
