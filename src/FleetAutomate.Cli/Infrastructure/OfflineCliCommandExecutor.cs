using FleetAutomate.Application.Commanding;
using FleetAutomate.Cli.Output;
using FleetAutomate.Cli.Services;
using CliActionMutationService = FleetAutomate.Cli.Services.ActionMutationService;
using CliActionPathResolver = FleetAutomate.Cli.Services.ActionPathResolver;
using System.ComponentModel;
using System.Diagnostics;

namespace FleetAutomate.Cli.Infrastructure;

internal sealed class OfflineCliCommandExecutor : IOfflineCommandExecutor
{
    private readonly CliOutputWriter? _outputWriter;
    private readonly ProjectFacade _projectFacade = new();
    private readonly CliActionPathResolver _actionPathResolver = new();
    private readonly CliActionMutationService _actionMutationService = new();

    public OfflineCliCommandExecutor(CliOutputWriter? outputWriter = null)
    {
        _outputWriter = outputWriter;
    }

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
            "testflow.run" => RunFlowAsync(projectPath, GetValue(command.Arguments, "flow") ?? string.Empty, cancellationToken),
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

    private async Task<CommandResult> RunFlowAsync(string projectPath, string flowName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(flowName))
        {
            throw new CliUsageException("Missing required option '--flow'.");
        }

        var flow = _projectFacade.LoadFlow(projectPath, flowName);
        flow.InitializeAfterDeserialization();
        _outputWriter?.WriteTestFlowStarted(Path.GetFullPath(projectPath), flow.Name);
        var actionExecutions = new List<ActionExecutionRecord>();
        var activeExecutions = new Dictionary<FleetAutomate.Model.IAction, ActionExecutionBuilder>(ReferenceEqualityComparer.Instance);
        var nodes = _actionPathResolver.Flatten(flow);
        var actionStateHandlers = new List<(INotifyPropertyChanged ObservableAction, PropertyChangedEventHandler Handler)>();

        var completed = false;
        try
        {
            foreach (var node in nodes)
            {
                if (node.Action is not INotifyPropertyChanged observableAction)
                {
                    continue;
                }

                PropertyChangedEventHandler handler = (_, eventArgs) =>
                {
                    if (eventArgs.PropertyName != nameof(FleetAutomate.Model.IAction.State))
                    {
                        return;
                    }

                    var state = node.Action.State.ToString();
                    if (state == "Running")
                    {
                        lock (actionExecutions)
                        {
                            var execution = new ActionExecutionBuilder(node, Stopwatch.GetTimestamp(), actionExecutions.Count);
                            activeExecutions[node.Action] = execution;
                            actionExecutions.Add(execution.ToRecord("Running", 0));
                        }
                        _outputWriter?.WriteActionExecutionStarted(node.Path, node.Name);
                    }
                    else if (state is "Completed" or "Failed" or "Paused")
                    {
                        lock (actionExecutions)
                        {
                            if (activeExecutions.Remove(node.Action, out var execution))
                            {
                                var duration = Stopwatch.GetElapsedTime(execution.StartedAt).TotalMilliseconds;
                                var completedExecution = execution.ToRecord(state, (long)duration);
                                actionExecutions[execution.RecordIndex] = completedExecution;
                                _outputWriter?.WriteActionExecutionCompleted(completedExecution);
                            }
                            else
                            {
                                var completedExecution = new ActionExecutionRecord(node.Path, node.Name, state, 0);
                                actionExecutions.Add(completedExecution);
                                _outputWriter?.WriteActionExecutionCompleted(completedExecution);
                            }
                        }
                    }
                };

                observableAction.PropertyChanged += handler;
                actionStateHandlers.Add((observableAction, handler));
            }

            completed = await flow.StartAsync(cancellationToken);
        }
        finally
        {
            foreach (var (observableAction, handler) in actionStateHandlers)
            {
                observableAction.PropertyChanged -= handler;
            }
        }

        lock (actionExecutions)
        {
            foreach (var node in nodes)
            {
                if (node.Action.State.ToString() != "Ready" && actionExecutions.All(record => record.Path != node.Path))
                {
                    var execution = new ActionExecutionRecord(node.Path, node.Name, node.Action.State.ToString(), 0);
                    actionExecutions.Add(execution);
                    _outputWriter?.WriteActionExecutionCompleted(execution);
                }
            }
        }

        var failedAction = flow.LastFailedAction?.Name;
        var payload = new TestFlowRunResult(
            Path.GetFullPath(projectPath),
            flow.Name,
            flow.State.ToString(),
            flow.BreakReason.ToString(),
            failedAction,
            actionExecutions);

        if (!completed)
        {
            var message = failedAction == null
                ? $"TestFlow '{flow.Name}' execution failed."
                : $"TestFlow '{flow.Name}' failed at action '{failedAction}'.";
            return CommandResult.Failure(CommandExecutionMode.Offline, "EXECUTION_FAILED", message, payload: payload);
        }

        return CommandResult.Success(CommandExecutionMode.Offline, payload);
    }

    private readonly record struct ActionExecutionBuilder(FleetAutomate.Application.Commanding.ActionNode Node, long StartedAt, int RecordIndex)
    {
        public ActionExecutionRecord ToRecord(string state, long durationMilliseconds)
        {
            return new ActionExecutionRecord(Node.Path, Node.Name, state, durationMilliseconds);
        }
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
