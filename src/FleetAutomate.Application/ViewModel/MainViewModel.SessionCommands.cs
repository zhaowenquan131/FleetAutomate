using CommunityToolkit.Mvvm.Input;
using FleetAutomate.Model.Flow;
using System.IO;

namespace FleetAutomate.ViewModel;

public partial class MainViewModel
{
    internal bool SaveProjectFromSession(out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            if (!IsProjectLoaded || ActiveProject == null)
            {
                errorMessage = "No project is currently loaded.";
                return false;
            }

            foreach (var flow in ActiveProject.TestFlows)
            {
                flow.SyncToModel();
            }

            var success = ProjectManager.SaveProject();
            if (!success)
            {
                errorMessage = "Failed to save the project. You may need to use Save As for a new project.";
                return false;
            }

            NotifySessionUiStateChanged();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    internal ObservableFlow? FindFlowForSession(string flowName)
    {
        if (ActiveProject == null)
        {
            return null;
        }

        return ActiveProject.FindTestFlowByName(flowName);
    }

    internal ObservableFlow AddTestFlowInMemory(string name, string? description = null)
    {
        if (!IsProjectLoaded || ActiveProject == null)
        {
            throw new InvalidOperationException("No project is currently loaded.");
        }

        var project = ActiveProject.Model;
        var projectPath = ProjectManager.CurrentProjectFilePath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new InvalidOperationException("Current project has no file path.");
        }

        var projectDirectory = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            throw new InvalidOperationException("Invalid project file path.");
        }

        if (project.FindTestFlowByName(name) != null)
        {
            throw new InvalidOperationException($"Flow '{name}' already exists.");
        }

        var flowFilePath = BuildInMemoryFlowFilePath(project, projectDirectory, name);
        var relativePath = GetRelativeProjectPath(projectDirectory, flowFilePath);

        var flow = new TestFlow
        {
            Name = name,
            Description = description ?? string.Empty,
            FileName = flowFilePath,
            IsEnabled = true,
            ParentProject = project
        };

        project.AddTestFlowToCollection(flow);
        project.TestFlowFileNames = [.. project.TestFlowFileNames, relativePath];

        var observable = new ObservableFlow(flow)
        {
            HasUnsavedChanges = true
        };

        ActiveProject.TestFlows.Add(observable);
        ProjectManager.MarkAsModified();
        NotifySessionUiStateChanged();
        return observable;
    }

    internal void NotifySessionUiStateChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(HasAnyUnsavedFlowChanges));
        OnPropertyChanged(nameof(CurrentProjectPath));
        ((RelayCommand)SaveProjectCommand).NotifyCanExecuteChanged();
        ((RelayCommand)SaveProjectAsCommand).NotifyCanExecuteChanged();
        ((RelayCommand)CloseProjectCommand).NotifyCanExecuteChanged();
        ((RelayCommand)AddExistingTestFlowCommand).NotifyCanExecuteChanged();
    }

    private static string BuildInMemoryFlowFilePath(FleetAutomate.Model.Project.TestProject project, string projectDirectory, string flowName)
    {
        var sanitized = SanitizeFlowFileName(flowName);
        var testFlowsDirectory = Path.Combine(projectDirectory, "TestFlows");
        var basePath = Path.Combine(testFlowsDirectory, $"{sanitized}.testfl");

        var existingPaths = new HashSet<string>(
            (project.TestFlows ?? [])
                .Where(flow => !string.IsNullOrWhiteSpace(flow.FileName))
                .Select(flow => Path.GetFullPath(flow.FileName)),
            StringComparer.OrdinalIgnoreCase);

        var candidate = basePath;
        var counter = 1;
        while (existingPaths.Contains(Path.GetFullPath(candidate)))
        {
            candidate = Path.Combine(testFlowsDirectory, $"{sanitized}_{counter}.testfl");
            counter++;
        }

        return candidate;
    }

    private static string SanitizeFlowFileName(string flowName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", flowName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "TestFlow" : sanitized;
    }

    private static string GetRelativeProjectPath(string projectDirectory, string targetPath)
    {
        var projectUri = new Uri(Path.GetFullPath(projectDirectory) + Path.DirectorySeparatorChar);
        var targetUri = new Uri(Path.GetFullPath(targetPath));
        var relativeUri = projectUri.MakeRelativeUri(targetUri);
        return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
    }
}
