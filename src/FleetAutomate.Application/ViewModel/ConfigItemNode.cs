using CommunityToolkit.Mvvm.ComponentModel;
using FleetAutomate.Model.Project;

namespace FleetAutomate.ViewModel;

public sealed partial class ConfigItemNode : ObservableObject
{
    public ConfigItemNode(ConfigValueNode model, string? selectedPath)
    {
        Model = model;
        _isSelected = selectedPath != null && model.Path == selectedPath;
        Children = model.Children.Select(child => new ConfigItemNode(child, selectedPath)).ToList();
    }

    public ConfigValueNode Model { get; }
    public string DisplayName => Model.DisplayName;
    public string Preview => Model.Preview;
    public IReadOnlyList<ConfigItemNode> Children { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed record ProjectConfigOption(ProjectFileReference Reference)
{
    public string Id => Reference.Id;
    public string FilePath => Reference.FilePath;
}
