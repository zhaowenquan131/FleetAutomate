using FleetAutomate.Cli.Output;
using FleetAutomate.Cli.Services;

namespace FleetAutomate.Cli.Infrastructure;

internal sealed class CliCommandDispatcher
{
    private readonly CliOutputWriter _writer;
    private readonly ProjectFacade _projectFacade = new();
    private readonly ActionPathResolver _actionPathResolver = new();

    public CliCommandDispatcher(CliOutputWriter writer)
    {
        _writer = writer;
    }

    public Task<int> DispatchAsync(string resource, string verb, string projectPath, CliArgumentParser parser)
    {
        return (resource, verb) switch
        {
            ("testproj", "show") => WriteProjectAsync(projectPath),
            ("testproj", "list-flows") => WriteProjectFlowsAsync(projectPath),
            ("testflow", "show") => WriteFlowAsync(projectPath, parser.GetRequiredOption("flow")),
            ("testflow", "tree") => WriteFlowTreeAsync(projectPath, parser.GetRequiredOption("flow")),
            ("action", "list") => WriteActionListAsync(projectPath, parser.GetRequiredOption("flow")),
            ("action", "tree") => WriteFlowTreeAsync(projectPath, parser.GetRequiredOption("flow")),
            ("action", "show") => WriteActionAsync(projectPath, parser.GetRequiredOption("flow"), parser.GetRequiredOption("path")),
            _ => throw new CliUsageException($"Unsupported command: {resource} {verb}")
        };
    }

    private Task<int> WriteProjectAsync(string projectPath)
    {
        var project = _projectFacade.LoadProject(projectPath);
        _writer.WriteObject(new
        {
            projectPath = Path.GetFullPath(projectPath),
            projectName = project.Name,
            flowCount = project.TestFlows?.Count ?? 0,
            runtimeStatePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(projectPath))!, ".fleet")
        });
        return Task.FromResult(0);
    }

    private Task<int> WriteProjectFlowsAsync(string projectPath)
    {
        var project = _projectFacade.LoadProject(projectPath);
        var rows = (project.TestFlows ?? [])
            .Select(flow => new
            {
                flow.Name,
                flow.FileName,
                flow.IsEnabled,
                ActionCount = flow.Actions.Count
            });
        _writer.WriteRows(rows);
        return Task.FromResult(0);
    }

    private Task<int> WriteFlowAsync(string projectPath, string flowName)
    {
        var flow = _projectFacade.LoadFlow(projectPath, flowName);
        _writer.WriteObject(new
        {
            projectPath = Path.GetFullPath(projectPath),
            flowName = flow.Name,
            flow.FileName,
            flow.Description,
            flow.IsEnabled,
            actionCount = flow.Actions.Count,
            state = flow.State.ToString()
        });
        return Task.FromResult(0);
    }

    private Task<int> WriteFlowTreeAsync(string projectPath, string flowName)
    {
        var flow = _projectFacade.LoadFlow(projectPath, flowName);
        var rows = _actionPathResolver.Flatten(flow).Select(node => new
        {
            node.Path,
            node.Depth,
            node.Container,
            node.Type,
            node.Name,
            node.Description,
            node.IsEnabled
        });
        _writer.WriteRows(rows);
        return Task.FromResult(0);
    }

    private Task<int> WriteActionListAsync(string projectPath, string flowName)
    {
        var flow = _projectFacade.LoadFlow(projectPath, flowName);
        var rows = _actionPathResolver.Flatten(flow)
            .Where(node => node.Depth == 0)
            .Select(node => new
            {
                node.Path,
                node.Type,
                node.Name,
                node.Description,
                node.IsEnabled
            });
        _writer.WriteRows(rows);
        return Task.FromResult(0);
    }

    private Task<int> WriteActionAsync(string projectPath, string flowName, string actionPath)
    {
        var flow = _projectFacade.LoadFlow(projectPath, flowName);
        var node = _actionPathResolver.Resolve(flow, actionPath);
        _writer.WriteObject(new
        {
            node.Path,
            node.Container,
            node.Type,
            node.Name,
            node.Description,
            node.IsEnabled,
            node.State,
            Config = _actionPathResolver.ExtractConfig(node.Action)
        });
        return Task.FromResult(0);
    }
}
