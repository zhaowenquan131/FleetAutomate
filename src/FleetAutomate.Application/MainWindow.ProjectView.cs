using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FleetAutomate.Model.Project;
using FleetAutomate.Services;
using FleetAutomate.View.Dialog;
using FleetAutomate.ViewModel;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfTreeViewItem = System.Windows.Controls.TreeViewItem;

namespace FleetAutomate;

public partial class MainWindow
{
    private void ProjectTreeView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            FindAncestor<System.Windows.Controls.Primitives.ToggleButton>(e.OriginalSource as DependencyObject) != null)
            return;
        ViewModel.SelectedAction = null;
        if (e.ClickCount == 2 && FindAncestor<WpfTreeViewItem>(e.OriginalSource as DependencyObject)?.DataContext is ProjectTreeNode node)
        {
            e.Handled = true;
            OpenProjectNode(node);
        }
    }

    private void ProjectTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<WpfTreeViewItem>(e.OriginalSource as DependencyObject);
        if (item == null) return;
        ViewModel.SelectedAction = null;
        item.IsSelected = true;
        item.Focus();
        e.Handled = true;
    }

    private void ProjectTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        ViewModel.SelectedAction = null;
        if (e.NewValue is ProjectTreeNode { Value: ObservableFlow flow }) ViewModel.ActiveTestFlow = flow;
    }

    private void ProjectTreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ProjectTreeView.SelectedItem is ProjectTreeNode node)
        {
            e.Handled = true;
            OpenProjectNode(node);
        }
    }

    private void ProjectTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (ProjectTreeView.SelectedItem is not ProjectTreeNode node || ViewModel.ActiveProject == null)
        {
            e.Handled = true;
            return;
        }
        var menu = ProjectTreeView.ContextMenu;
        menu.Items.Clear();
        void Add(string title, Action action, bool enabled = true)
        {
            var item = new WpfMenuItem { Header = title, IsEnabled = enabled };
            item.Click += (_, _) => ProjectOperation(action);
            menu.Items.Add(item);
        }
        var editable = !ViewModel.IsTestFlowRunning;
        if (node.Kind is ProjectNodeKind.Project or ProjectNodeKind.TestFlows)
        {
            Add("New test flow...", () => AddTestFlowMenuItem_Click(this, new RoutedEventArgs()), editable);
            Add("Add existing test flow...", () => ViewModel.AddExistingTestFlowCommand.Execute(null), editable);
        }
        if (node.Kind is ProjectNodeKind.Project or ProjectNodeKind.Configs)
        {
            Add("New JSON config...", () => CreateProjectConfig(yaml: false));
            Add("New YAML config...", () => CreateProjectConfig(yaml: true));
            Add("Add existing configs...", () => AddProjectFiles(config: true));
        }
        if (node.Kind is ProjectNodeKind.Project or ProjectNodeKind.UiPaths)
            Add("New UI path reference...", () => EditProjectUiPath(null), editable);
        if (node.Kind is ProjectNodeKind.Project or ProjectNodeKind.Resources)
            Add("Add resources...", () => AddProjectFiles(config: false));
        if (node.Value is ObservableFlow flow)
        {
            Add("Open", () => ViewModel.OpenTestFlowInTabCommand.Execute(flow));
            Add("Run test flow", () =>
            {
                ViewModel.ActiveTestFlow = flow;
                ViewModel.RunTestFlowCommand.Execute(null);
            }, editable && flow.IsEnabled);
            Add(flow.IsEnabled ? "Disable" : "Enable", () => flow.IsEnabled = !flow.IsEnabled, editable);
            Add("Remove from project", () =>
            {
                ViewModel.CloseTestFlowTabCommand.Execute(flow);
                ViewModel.ActiveProject?.TestFlows.Remove(flow);
                ViewModel.ProjectManager.MarkAsModified();
            }, editable);
        }
        if (node.Value is ProjectFileReference file)
        {
            Add("Open", () => OpenProjectFile(node));
            Add("Open with default application", () => OpenExternal(ProjectContentService.ResolveFilePath(ViewModel.CurrentProjectPath!, file.FilePath)));
            Add("Remove reference", () => ViewModel.RemoveProjectFile(file, node.Kind == ProjectNodeKind.Config));
        }
        if (node.Value is UiPathReference reference)
        {
            Add("Edit...", () => EditProjectUiPath(reference), editable);
            Add("Copy reference", () => System.Windows.Clipboard.SetText("@{" + reference.Name + "}"));
            Add("Remove reference", () => ViewModel.RemoveProjectUiPath(reference), editable);
        }
    }

    private void OpenProjectNode(ProjectTreeNode node) => ProjectOperation(() =>
    {
        switch (node.Value)
        {
            case ObservableFlow flow:
                ViewModel.OpenTestFlowInTabCommand.Execute(flow);
                break;
            case ProjectFileReference:
                OpenProjectFile(node);
                break;
            case UiPathReference reference:
                EditProjectUiPath(reference);
                break;
            default:
                node.IsExpanded = !node.IsExpanded;
                break;
        }
    });

    private void ProjectOperation(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Project", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AddProjectFiles(bool config)
    {
        if (ViewModel.CurrentProjectPath == null) throw new InvalidOperationException("Save the project before adding files.");
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = config ? "Add configuration files" : "Add resources",
            Filter = config ? "JSON / YAML|*.json;*.yaml;*.yml" : "All files|*.*",
            Multiselect = true,
            CheckFileExists = true,
            InitialDirectory = ProjectContentService.ProjectDirectory(ViewModel.CurrentProjectPath)
        };
        if (dialog.ShowDialog(this) == true) ViewModel.AddProjectFiles(dialog.FileNames, config);
    }

    private void CreateProjectConfig(bool yaml)
    {
        if (ViewModel.CurrentProjectPath == null) throw new InvalidOperationException("Save the project before creating configs.");
        var directory = Path.Combine(ProjectContentService.ProjectDirectory(ViewModel.CurrentProjectPath), "Configs");
        Directory.CreateDirectory(directory);
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "New configuration file", InitialDirectory = directory,
            FileName = yaml ? "config.yaml" : "config.json",
            DefaultExt = yaml ? ".yaml" : ".json",
            Filter = yaml ? "YAML|*.yaml;*.yml" : "JSON|*.json",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;
        if (!ProjectContentService.IsConfigFile(dialog.FileName)) throw new ArgumentException("Use a .json, .yaml or .yml file name.");
        File.WriteAllText(dialog.FileName, Path.GetExtension(dialog.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase) ? "{\n}\n" : "# Project configuration\n");
        ViewModel.AddProjectFiles([dialog.FileName], configs: true);
        new ProjectFileEditorWindow(dialog.FileName, config: true) { Owner = this }.ShowDialog();
    }

    private void EditProjectUiPath(UiPathReference? reference)
    {
        if (ViewModel.IsTestFlowRunning) throw new InvalidOperationException("Stop the flow before editing UI path references.");
        string? capturedXPath = null;
        if (reference == null)
        {
            var capture = new ElementCaptureDialog { Owner = this, UseButtonText = "Use UI path" };
            if (capture.ShowDialog() != true) return;
            capturedXPath = capture.CapturedXPath;
        }
        new UiPathReferenceDialog(reference, (name, xpath) => ViewModel.SetProjectUiPath(reference, name, xpath), capturedXPath) { Owner = this }.ShowDialog();
    }

    private void OpenProjectFile(ProjectTreeNode node)
    {
        var reference = (ProjectFileReference)node.Value!;
        var path = ProjectContentService.ResolveFilePath(ViewModel.CurrentProjectPath!, reference.FilePath);
        if (!File.Exists(path)) throw new FileNotFoundException("The referenced file was not found.", path);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".ico")
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            var preview = new Wpf.Ui.Controls.FluentWindow
            {
                Owner = this, Title = Path.GetFileName(path), Width = 900, Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.None
            };
            var panel = new DockPanel();
            var titleBar = new Wpf.Ui.Controls.TitleBar { Title = preview.Title };
            DockPanel.SetDock(titleBar, Dock.Top);
            panel.Children.Add(titleBar);
            var close = new System.Windows.Controls.Button { Content = "Close", MinWidth = 90, Margin = new Thickness(12), HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            close.Click += (_, _) => preview.Close();
            DockPanel.SetDock(close, Dock.Bottom);
            panel.Children.Add(close);
            panel.Children.Add(new System.Windows.Controls.Image { Source = image, Stretch = Stretch.Uniform, Margin = new Thickness(12) });
            preview.Content = panel;
            preview.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; preview.Close(); } };
            preview.ShowDialog();
        }
        else if (node.Kind == ProjectNodeKind.Config || IsTextResource(path))
        {
            new ProjectFileEditorWindow(path, node.Kind == ProjectNodeKind.Config) { Owner = this }.ShowDialog();
        }
        else OpenExternal(path);
    }

    private static bool IsTextResource(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[Math.Min(8192, stream.Length)];
        var count = stream.Read(buffer);
        if (count >= 2 && ((buffer[0] == 0xFF && buffer[1] == 0xFE) || (buffer[0] == 0xFE && buffer[1] == 0xFF))) return true;
        return !buffer.Take(count).Contains((byte)0);
    }

    private static void OpenExternal(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
}
