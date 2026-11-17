using System.Windows;
using System.Windows.Controls;
using FleetAutomate.Model.Project;
using FleetAutomate.ViewModel;

namespace FleetAutomate.View.Dialog;

public partial class SetVariableDialog
{
    private TestProject? _configProject;
    private ConfigValueReference? _initialConfigReference;
    private ConfigValueNode? _selectedConfigNode;

    public Type ConfigValueType { get; private set; } = typeof(object);

    private void InitializeConfigPicker(string? reference = null, TestProject? project = null)
    {
        _configProject = project;
        if (!string.IsNullOrEmpty(reference))
        {
            try { _initialConfigReference = ConfigValueReference.Parse(reference); }
            catch { ConfigPreviewText.Text = "The saved configuration reference is invalid. Select a file and item again."; }
        }
        Loaded += (_, _) =>
        {
            var owner = Owner;
            while (owner != null && owner is not MainWindow) owner = owner.Owner;
            _configProject ??= (owner as MainWindow)?.ViewModel.ActiveProject?.Model;
            ConfigFileComboBox.ItemsSource = _configProject?.Configs.Select(file => new ProjectConfigOption(file)).ToList();
            if (_initialConfigReference != null)
            {
                ConfigFileComboBox.SelectedValue = _initialConfigReference.FileId;
                if (ConfigFileComboBox.SelectedIndex < 0)
                    ConfigPreviewText.Text = "The saved configuration file is no longer in this project. Select another file.";
            }
            else if (ConfigFileComboBox.Items.Count > 0) ConfigFileComboBox.SelectedIndex = 0;
            else ConfigPreviewText.Text = "Add a JSON or YAML file under the project's configs group first.";
            UpdateOkButtonState();
        };
    }

    private void ConfigFileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedConfigNode = null;
        ConfigItemTree.ItemsSource = null;
        ConfigPreviewText.Text = string.Empty;
        if (_configProject != null && ConfigFileComboBox.SelectedItem is ProjectConfigOption { Reference: var file })
        {
            try
            {
                var root = ProjectConfigValues.Read(_configProject, file);
                if (_initialConfigReference?.FileId == file.Id)
                {
                    _selectedConfigNode = ProjectConfigValues.Find(root, _initialConfigReference.ItemPath);
                    if (_selectedConfigNode == null) ConfigPreviewText.Text = "The saved configuration item no longer exists. Select another item.";
                }
                ConfigItemTree.ItemsSource = new[] { new ConfigItemNode(root, _initialConfigReference?.FileId == file.Id ? _initialConfigReference.ItemPath : null) };
                UpdateConfigPreview();
            }
            catch (Exception ex) { ConfigPreviewText.Text = ex.Message; }
        }
        UpdateOkButtonState();
    }

    private void ConfigItemTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _selectedConfigNode = (e.NewValue as ConfigItemNode)?.Model;
        UpdateConfigPreview();
        UpdateOkButtonState();
    }

    private void UpdateConfigPreview()
    {
        if (_selectedConfigNode == null) return;
        ConfigValueType = _selectedConfigNode.Value?.GetType() ?? typeof(object);
        ConfigPreviewText.Text = $"Type: {_selectedConfigNode.TypeName}\nValue: {_selectedConfigNode.Preview}";
    }

    private string GetConfigReference()
    {
        if (_configProject == null || ConfigFileComboBox.SelectedItem is not ProjectConfigOption { Reference: var file } || _selectedConfigNode == null)
            throw new InvalidOperationException("Select a configuration file and item.");
        var reference = new ConfigValueReference(file.Id, _selectedConfigNode.Path);
        var currentValue = ProjectConfigValues.Resolve(_configProject, reference);
        ConfigValueType = currentValue?.GetType() ?? typeof(object);
        return reference.Serialize();
    }
}
