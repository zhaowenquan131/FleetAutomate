using FleetAutomate.Cli.Output;
using FleetAutomate.Cli.Services;

namespace FleetAutomate.Cli.Infrastructure;

internal sealed class CliCommandDispatcher
{
    private readonly CliOutputWriter _writer;
    private readonly ProjectFacade _projectFacade = new();
    private readonly ActionPathResolver _actionPathResolver = new();
    private readonly ActionMutationService _actionMutationService = new();

    public CliCommandDispatcher(CliOutputWriter writer)
    {
        _writer = writer;
    }

    public Task<int> DispatchAsync(string resource, string verb, string projectPath, CliArgumentParser parser)
    {
        return (resource, verb) switch
        {
            ("testproj", "create") => CreateProjectAsync(projectPath, parser.GetRequiredOption("name")),
            ("testproj", "show") => WriteProjectAsync(projectPath),
            ("testproj", "list-flows") => WriteProjectFlowsAsync(projectPath),
            ("testflow", "create") => CreateFlowAsync(projectPath, parser.GetRequiredOption("name"), parser.GetOption("description")),
            ("testflow", "show") => WriteFlowAsync(projectPath, parser.GetRequiredOption("flow")),
            ("testflow", "tree") => WriteFlowTreeAsync(projectPath, parser.GetRequiredOption("flow")),
            ("action", "add") => AddActionAsync(projectPath, parser),
            ("action", "set") => SetActionPropertyAsync(projectPath, parser),
            ("action", "remove") => RemoveActionAsync(projectPath, parser),
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

    private Task<int> CreateProjectAsync(string projectPath, string projectName)
    {
        var project = _projectFacade.CreateProject(projectPath, projectName);
        _writer.WriteObject(new
        {
            projectPath = Path.GetFullPath(projectPath),
            projectName = project.Name,
            flowCount = project.TestFlows?.Count ?? 0
        });

        return Task.FromResult(0);
    }

    private Task<int> CreateFlowAsync(string projectPath, string flowName, string? description)
    {
        var flow = _projectFacade.CreateFlow(projectPath, flowName, description);
        _writer.WriteObject(new
        {
            projectPath = Path.GetFullPath(projectPath),
            flowName = flow.Name,
            flow.FileName,
            flow.Description,
            actionCount = flow.Actions.Count
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

    private Task<int> AddActionAsync(string projectPath, CliArgumentParser parser)
    {
        var flowName = parser.GetRequiredOption("flow");
        var flow = _projectFacade.LoadFlow(projectPath, flowName);
        var action = _actionMutationService.CreateAction(parser.GetRequiredOption("type"));
        var parentPath = parser.GetOption("parent-path");
        var container = parser.GetOption("container");
        var index = TryParseOptionalInt(parser.GetOption("index"), "index");
        var path = _actionMutationService.AddAction(flow, action, parentPath, container, index);
        PersistFlow(projectPath, flow);

        _writer.WriteObject(new
        {
            flow = flow.Name,
            path,
            type = action.GetType().Name,
            action.Name,
            action.Description
        });

        return Task.FromResult(0);
    }

    private Task<int> SetActionPropertyAsync(string projectPath, CliArgumentParser parser)
    {
        var flow = _projectFacade.LoadFlow(projectPath, parser.GetRequiredOption("flow"));
        var path = parser.GetRequiredOption("path");
        var property = parser.GetRequiredOption("property");
        var value = parser.GetRequiredOption("value");
        _actionMutationService.SetProperty(flow, path, property, value);
        PersistFlow(projectPath, flow);

        var node = _actionPathResolver.Resolve(flow, path);
        var config = _actionPathResolver.ExtractConfig(node.Action);
        config.TryGetValue(property, out var storedValue);
        _writer.WriteObject(new
        {
            flow = flow.Name,
            path = node.Path,
            property,
            value = storedValue
        });

        return Task.FromResult(0);
    }

    private Task<int> RemoveActionAsync(string projectPath, CliArgumentParser parser)
    {
        var flow = _projectFacade.LoadFlow(projectPath, parser.GetRequiredOption("flow"));
        var path = parser.GetRequiredOption("path");
        _actionMutationService.RemoveAction(flow, path);
        PersistFlow(projectPath, flow);

        _writer.WriteObject(new
        {
            flow = flow.Name,
            removedPath = path,
            actionCount = flow.Actions.Count
        });

        return Task.FromResult(0);
    }

    private void PersistFlow(string projectPath, FleetAutomate.Model.Flow.TestFlow flow)
    {
        if (flow.ParentProject == null)
        {
            throw new InvalidOperationException($"Flow '{flow.Name}' is not attached to a project.");
        }

        _projectFacade.SaveProject(projectPath, flow.ParentProject);
    }

    private static int? TryParseOptionalInt(string? rawValue, string optionName)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (int.TryParse(rawValue, out var parsed))
        {
            return parsed;
        }

        throw new CliUsageException($"Option '--{optionName}' must be an integer.");
    }
}
