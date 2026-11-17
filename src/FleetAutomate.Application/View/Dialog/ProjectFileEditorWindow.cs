using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using YamlDotNet.RepresentationModel;
using WpfButton = System.Windows.Controls.Button;

namespace FleetAutomate.View.Dialog;

public sealed class ProjectFileEditorWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly string _path;
    private readonly bool _config;
    private readonly TextEditor _editor;
    private readonly TextBlock _status;
    private readonly Encoding _encoding;
    private string _savedText;

    public ProjectFileEditorWindow(string path, bool config)
    {
        _path = path;
        _config = config;
        using (var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            _savedText = reader.ReadToEnd();
            _encoding = reader.CurrentEncoding;
        }
        Title = Path.GetFileName(path);
        Width = 960;
        Height = 700;
        MinWidth = 560;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        var shell = new DockPanel();
        var titleBar = new Wpf.Ui.Controls.TitleBar();
        titleBar.SetBinding(Wpf.Ui.Controls.TitleBar.TitleProperty, new System.Windows.Data.Binding(nameof(Title)) { Source = this });
        DockPanel.SetDock(titleBar, Dock.Top);
        shell.Children.Add(titleBar);
        var panel = new DockPanel { Margin = new Thickness(16) };
        var close = new WpfButton { Content = "Close", MinWidth = 90, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Bottom);
        panel.Children.Add(close);
        var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var save = new WpfButton { Content = "Save (Ctrl+S)", MinWidth = 110 };
        save.Click += (_, _) => SaveFile();
        DockPanel.SetDock(save, Dock.Right);
        toolbar.Children.Add(save);
        toolbar.Children.Add(new TextBlock { Text = path, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        DockPanel.SetDock(toolbar, Dock.Top);
        panel.Children.Add(toolbar);
        _status = new TextBlock { Margin = new Thickness(0, 10, 0, 0), Text = "Ready" };
        DockPanel.SetDock(_status, Dock.Bottom);
        panel.Children.Add(_status);
        _editor = new TextEditor
        {
            Text = _savedText,
            ShowLineNumbers = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 14,
            Background = System.Windows.Media.Brushes.White,
            Foreground = System.Windows.Media.Brushes.Black,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            SyntaxHighlighting = CreateHighlighting(Path.GetExtension(path)),
            Padding = new Thickness(8)
        };
        _editor.Options.ConvertTabsToSpaces = true;
        _editor.Options.IndentationSize = 2;
        _editor.IsModified = false;
        _editor.TextChanged += (_, _) =>
        {
            Title = Path.GetFileName(_path) + (_editor.IsModified ? " *" : string.Empty);
            _status.Text = _editor.IsModified ? "Unsaved changes" : "Saved";
        };
        _editor.TextArea.Caret.PositionChanged += (_, _) => _status.Text = $"Line {_editor.TextArea.Caret.Line}, column {_editor.TextArea.Caret.Column}";
        panel.Children.Add(_editor);
        shell.Children.Add(panel);
        Content = shell;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                SaveFile();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        };
        Closing += ConfirmClose;
    }

    private bool SaveFile()
    {
        try
        {
            if (_config) ValidateConfig(_path, _editor.Text);
            if (File.Exists(_path) && File.ReadAllText(_path) != _savedText &&
                System.Windows.MessageBox.Show(this, "The file changed outside the editor. Overwrite it?", "File Changed", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return false;
            File.WriteAllText(_path, _editor.Text, _encoding);
            _savedText = _editor.Text;
            _editor.IsModified = false;
            Title = Path.GetFileName(_path);
            _status.Text = "Saved";
            return true;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            System.Windows.MessageBox.Show(this, ex.Message, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void ConfirmClose(object? sender, CancelEventArgs e)
    {
        if (!_editor.IsModified) return;
        var response = System.Windows.MessageBox.Show(this, $"Save changes to {Path.GetFileName(_path)}?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        e.Cancel = response == MessageBoxResult.Cancel || (response == MessageBoxResult.Yes && !SaveFile());
    }

    public static void ValidateConfig(string path, string text)
    {
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(text);
        }
        else
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));
        }
    }

    private static IHighlightingDefinition? CreateHighlighting(string extension)
    {
        var format = extension.ToLowerInvariant();
        if (format is not ".json" and not ".yaml" and not ".yml")
            return HighlightingManager.Instance.GetDefinitionByExtension(format);
        var rules = format == ".json" ? """
            <Rule color="String">"(\\.|[^"\\])*"</Rule>
            <Keywords color="Keyword"><Word>true</Word><Word>false</Word><Word>null</Word></Keywords>
            <Rule color="Number">\b-?[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?\b</Rule>
            """ : """
            <Rule color="String">"(\\.|[^"\\])*"|'([^']|'')*'</Rule>
            <Span color="Comment" begin="#" />
            <Rule color="Keyword">^\s*[^#\s][^:]*?(?=\s*:)</Rule>
            <Keywords color="Keyword"><Word>true</Word><Word>false</Word><Word>null</Word></Keywords>
            <Rule color="Number">\b-?[0-9]+(\.[0-9]+)?\b</Rule>
            """;
        var definition = $$"""
            <SyntaxDefinition name="{{format}}" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="String" foreground="#A31515" />
              <Color name="Keyword" foreground="#0000AA" />
              <Color name="Number" foreground="#098658" />
              <Color name="Comment" foreground="#608B4E" />
              <RuleSet>{{rules}}</RuleSet>
            </SyntaxDefinition>
            """;
        using var reader = XmlReader.Create(new StringReader(definition));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
