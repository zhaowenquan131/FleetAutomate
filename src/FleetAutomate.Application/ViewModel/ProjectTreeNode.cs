using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace FleetAutomate.ViewModel;

public enum ProjectNodeKind { Project, TestFlows, Configs, UiPaths, Resources, TestFlow, Config, UiPath, Resource }

public sealed partial class ProjectTreeNode : ObservableObject
{
    public ProjectTreeNode(ProjectNodeKind kind, string title, object? value = null, string? detail = null)
    {
        Kind = kind;
        _title = title;
        Value = value;
        Detail = detail;
    }

    public ProjectNodeKind Kind { get; }
    public object? Value { get; }
    public string? Detail { get; }
    public ObservableCollection<ProjectTreeNode> Children { get; } = [];

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isExpanded = true;

    public string Icon => Kind switch
    {
        ProjectNodeKind.Project => "▣",
        ProjectNodeKind.TestFlow => "▷",
        ProjectNodeKind.Config => "{}",
        ProjectNodeKind.UiPath => "⌖",
        ProjectNodeKind.Resource => "▧",
        _ => "▸"
    };
}
