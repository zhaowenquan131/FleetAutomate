using FleetAutomate.Application.Commanding;
using FleetAutomate.Cli.Output;
using FleetAutomate.Cli.Services;
using CliActionMutationService = FleetAutomate.Cli.Services.ActionMutationService;
using CliActionPathResolver = FleetAutomate.Cli.Services.ActionPathResolver;

namespace FleetAutomate.Cli.Infrastructure;

internal sealed class OfflineCliCommandExecutor : IOfflineCommandExecutor
{
    private readonly ProjectFacade _projectFacade = new();
    private readonly CliActionPathResolver _actionPathResolver = new();
    private readonly CliActionMutationService _actionMutationService = new();

    public Task<CommandResult> ExecuteAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
    {
        var projectPath = command.ProjectPath ?? throw new CliUsageException("Missing required option '--project'.");

        return command.Command switch
        {
            "testproj.create" => CreateProjectAsync(projectPath, command.Arguments["name"] ?? string.Empty),
            "testproj.show" => WriteProjectAsync(projectPath),
            "testproj.list-flows" => WriteProjectFlowsAsync(projectPath),
            "testflow.create" => CreateFlowAsync(projectPath, command.Arguments["name"] ?? string.Empty, GetValue(command.Arguments, "description")),
            "testflow.show" => WriteFlowAsync(projectPath, command.Arguments["flow"] ?? string.Empty),
            "testflow.tree" => WriteFlowTreeAsync(projectPath, command.Arguments["flow"] ?? string.Empty),
            "action.add" => AddActionAsync(projectPath, command.Arguments),
            "action.set" => SetActionPropertyAsync(projectPath, command.Arguments),
            "action.remove" => RemoveActionAsync(projectPath, command.Arguments),
            "action.list" => WriteActionListAsync(projectPath, command.Arguments["flow"] ?? string.Empty),
            "action.tree" => WriteFlowTreeAsync(projectPath, command.Arguments["flow"] ?? string.Empty),
            "action.show" => WriteActionAsync(projectPath, command.Arguments["flow"] ?? string.Empty, command.Arguments["path"] ?? string.Empty),
            "project.save" => SaveProjectAsync(projectPath),
            _ => throw new CliUsageException($"Unsupported command: {command.Command}")
        };
    }

    private Task<CommandResult> WriteProjectAsync(string projectPath)
    {
        var project = _projectFacade.LoadProject(projectPath);
        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, new
        {
            projectPath = Path.GetFullPath(projectPath),
            projectName = project.Name,
            flowCount = project.TestFlows?.Count ?? 0,
            runtimeStatePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(projectPath))!, ".fleet")
        }));
    }

    private Task<CommandResult> WriteProjectFlowsAsync(string projectPath)
    {
        var project = _projectFacade.LoadProject(projectPath);
        var rows = (project.TestFlows ?? [])
            .Select(flow => new
            {
                flow.Name,
                flow.FileName,
                flow.IsEnabled,
                ActionCount = flow.Actions.Count
            })
            .ToArray();
        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, rows));
    }

    private Task<CommandResult> WriteFlowAsync(string projectPath, string flowName)
    {
        var flow = _projectFacade.LoadFlow(projectPath, flowName);
        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, new
        {
            projectPath = Path.GetFullPath(projectPath),
            flowName = flow.Name,
            flow.FileName,
            flow.Description,
            flow.IsEnabled,
            actionCount = flow.Actions.Count,
            state = flow.State.ToString()
        }));
    }

    private Task<CommandResult> CreateProjectAsync(string projectPath, string projectName)
    {
        var project = _projectFacade.CreateProject(projectPath, projectName);
        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, new
        {
            projectPath = Path.GetFullPath(projectPath),
            projectName = project.Name,
            flowCount = project.TestFlows?.Count ?? 0
        }));
    }

    private Task<CommandResult> CreateFlowAsync(string projectPath, string flowName, string? description)
    {
        var flow = _projectFacade.CreateFlow(projectPath, flowName, description);
        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, new
        {
            projectPath = Path.GetFullPath(projectPath),
            flowName = flow.Name,
            flow.FileName,
            flow.Description,
            actionCount = flow.Actions.Count
        }));
    }

    private Task<CommandResult> WriteFlowTreeAsync(string projectPath, string flowName)
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
        }).ToArray();
        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, rows));
    }

    private Task<CommandResult> WriteActionListAsync(string projectPath, string flowName)
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
            })
            .ToArray();
        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, rows));
    }

    private Task<CommandResult> WriteActionAsync(string projectPath, string flowName, string actionPath)
    {
        var flow = _projectFacade.LoadFlow(projectPath, flowName);
        var node = _actionPathResolver.Resolve(flow, actionPath);
        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, new
        {
            node.Path,
            node.Container,
            node.Type,
            node.Name,
            node.Description,
            node.IsEnabled,
            node.State,
            Config = _actionPathResolver.ExtractConfig(node.Action)
        }));
    }

    private Task<CommandResult> AddActionAsync(string projectPath, IReadOnlyDictionary<string, string?> arguments)
    {
        var flowName = arguments["flow"] ?? string.Empty;
        var flow = _projectFacade.LoadFlow(projectPath, flowName);
        var action = _actionMutationService.CreateAction(arguments["type"] ?? string.Empty);
        var path = _actionMutationService.AddAction(
            flow,
            action,
            GetValue(arguments, "parent-path"),
            GetValue(arguments, "container"),
            TryParseOptionalInt(GetValue(arguments, "index"), "index"));
        PersistFlow(projectPath, flow);

        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, new
        {
            flow = flow.Name,
            path,
            type = action.GetType().Name,
            action.Name,
            action.Description
        }));
    }

    private Task<CommandResult> SetActionPropertyAsync(string projectPath, IReadOnlyDictionary<string, string?> arguments)
    {
        var flow = _projectFacade.LoadFlow(projectPath, arguments["flow"] ?? string.Empty);
        var path = arguments["path"] ?? string.Empty;
        var property = arguments["property"] ?? string.Empty;
        var value = arguments["value"] ?? string.Empty;
        _actionMutationService.SetProperty(flow, path, property, value);
        PersistFlow(projectPath, flow);

        var node = _actionPathResolver.Resolve(flow, path);
        var config = _actionPathResolver.ExtractConfig(node.Action);
        config.TryGetValue(property, out var storedValue);
        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, new
        {
            flow = flow.Name,
            path = node.Path,
            property,
            value = storedValue
        }));
    }

    private Task<CommandResult> RemoveActionAsync(string projectPath, IReadOnlyDictionary<string, string?> arguments)
    {
        var flow = _projectFacade.LoadFlow(projectPath, arguments["flow"] ?? string.Empty);
        var path = arguments["path"] ?? string.Empty;
        _actionMutationService.RemoveAction(flow, path);
        PersistFlow(projectPath, flow);

        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, new
        {
            flow = flow.Name,
            removedPath = path,
            actionCount = flow.Actions.Count
        }));
    }

    private Task<CommandResult> SaveProjectAsync(string projectPath)
    {
        var project = _projectFacade.LoadProject(projectPath);
        _projectFacade.SaveProject(projectPath, project);
        return Task.FromResult(CommandResult.Success(CommandExecutionMode.Offline, new
        {
            projectPath = Path.GetFullPath(projectPath),
            projectName = project.Name,
            flowCount = project.TestFlows?.Count ?? 0,
            saved = true
        }));
    }

    private void PersistFlow(string projectPath, FleetAutomate.Model.Flow.TestFlow flow)
    {
        if (flow.ParentProject == null)
        {
            throw new InvalidOperationException($"Flow '{flow.Name}' is not attached to a project.");
        }

        _projectFacade.SaveProject(projectPath, flow.ParentProject);
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> arguments, string name)
    {
        return arguments.TryGetValue(name, out var value) ? value : null;
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
