using FleetAutomate.Model.Project;
using FleetAutomate.Services;

namespace FleetAutomate.ViewModel;

public partial class MainViewModel
{
    public void AddProjectFiles(IEnumerable<string> files, bool configs)
    {
        if (ActiveProject == null || CurrentProjectPath == null) throw new InvalidOperationException("Save or open a project first.");
        try
        {
            foreach (var file in files) ProjectContentService.AddFile(ActiveProject.Model, CurrentProjectPath, file, configs);
        }
        finally { ProjectContentChanged(); }
    }

    public void RemoveProjectFile(ProjectFileReference reference, bool config)
    {
        if (ActiveProject == null) return;
        (config ? ActiveProject.Model.Configs : ActiveProject.Model.Resources).Remove(reference);
        ProjectContentChanged();
    }

    public void SetProjectUiPath(UiPathReference? reference, string name, string xpath)
    {
        if (ActiveProject == null) throw new InvalidOperationException("Open a project first.");
        ProjectContentService.SetUiPath(ActiveProject.Model, reference, name, xpath);
        ProjectContentChanged();
    }

    public void RemoveProjectUiPath(UiPathReference reference)
    {
        if (ActiveProject == null) return;
        ActiveProject.Model.UiPathReferences.Remove(reference);
        ProjectContentChanged();
    }

    private void ProjectContentChanged()
    {
        ActiveProject?.RefreshProjectTree();
        ProjectManager.MarkAsModified();
    }
}
