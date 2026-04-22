using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Project;
using FleetAutomate.Utilities;
using FleetAutomate.Cli.Infrastructure;

namespace FleetAutomate.Cli.Services;

internal sealed class ProjectFacade
{
    public TestProject CreateProject(string projectPath, string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new CliUsageException("Project path cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new CliUsageException("Project name cannot be empty.");
        }

        var fullPath = Path.GetFullPath(projectPath);
        if (File.Exists(fullPath))
        {
            throw new CliUsageException($"Project '{fullPath}' already exists.");
        }

        var project = new TestProject
        {
            Name = projectName
        };

        project.SaveProjectAndTestFlows(fullPath);
        return project;
    }

    public TestProject LoadProject(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new CliUsageException("Project path cannot be empty.");
        }

        var fullPath = Path.GetFullPath(projectPath);
        var project = TestProjectXmlExtensions.LoadFromXmlFile(fullPath);
        if (project == null)
        {
            throw new InvalidOperationException($"Failed to load project '{fullPath}'.");
        }

        return project;
    }

    public TestFlow LoadFlow(string projectPath, string flowName)
    {
        var project = LoadProject(projectPath);
        var flow = project.FindTestFlowByName(flowName);
        if (flow == null)
        {
            throw new InvalidOperationException($"Flow '{flowName}' was not found in project '{projectPath}'.");
        }

        return flow;
    }

    public TestFlow CreateFlow(string projectPath, string flowName, string? description = null)
    {
        var project = LoadProject(projectPath);
        var projectFullPath = Path.GetFullPath(projectPath);
        var projectDirectory = Path.GetDirectoryName(projectFullPath)
            ?? throw new InvalidOperationException($"Project path '{projectPath}' does not have a parent directory.");

        if (project.FindTestFlowByName(flowName) != null)
        {
            throw new CliUsageException($"Flow '{flowName}' already exists in project '{projectPath}'.");
        }

        var flow = new TestFlow
        {
            Name = flowName,
            Description = description ?? string.Empty,
            ParentProject = project
        };

        project.AddNewTestFlow(flow, projectDirectory, flowName);
        project.SaveProjectAndTestFlows(projectFullPath);
        return flow;
    }

    public void SaveProject(string projectPath, TestProject project)
    {
        var fullPath = Path.GetFullPath(projectPath);
        project.SaveProjectAndTestFlows(fullPath);
    }
}
