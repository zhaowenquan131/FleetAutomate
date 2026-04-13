using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Project;
using FleetAutomate.Utilities;
using FleetAutomate.Cli.Infrastructure;

namespace FleetAutomate.Cli.Services;

internal sealed class ProjectFacade
{
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
}
