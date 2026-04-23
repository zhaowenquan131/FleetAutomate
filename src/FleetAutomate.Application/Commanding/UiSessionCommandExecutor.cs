using FleetAutomate.Model.Flow;
using FleetAutomate.ViewModel;
using NLog;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;

namespace FleetAutomate.Application.Commanding;

public sealed class UiSessionCommandExecutor
{
    private static readonly Logger Logger = LogManager.GetLogger("ExternalCommand");

    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly MainViewModel _viewModel;
    private readonly ActionPathResolver _pathResolver = new();
    private readonly ActionMutationService _actionMutationService = new();
    private readonly string _sessionId;

    public UiSessionCommandExecutor(MainViewModel viewModel, string sessionId)
    {
        _viewModel = viewModel;
        _sessionId = sessionId;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public async Task<CommandResult> ExecuteAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (_dispatcher.CheckAccess())
            {
                return ExecuteCore(command);
            }

            return await _dispatcher.InvokeAsync(() => ExecuteCore(command), DispatcherPriority.Normal, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private CommandResult ExecuteCore(CommandEnvelope command)
    {
        var target = command.ProjectPath ?? _viewModel.CurrentProjectPath ?? "(no project)";
        try
        {
            if (!IsReadOnlyCommand(command.Command) && _viewModel.IsTestFlowRunning)
            {
                return LogAndReturn(command, target, CommandResult.Failure(
                    CommandExecutionMode.UiSession,
                    "BUSY",
                    "execution in progress",
                    _sessionId));
            }

            if (!ProjectMatches(command.ProjectPath))
            {
                return LogAndReturn(command, target, CommandResult.Failure(
                    CommandExecutionMode.UiSession,
                    "PROJECT_MISMATCH",
                    "UI session project does not match requested project path.",
                    _sessionId));
            }

            var result = command.Command switch
            {
                "testproj.show" => ShowProject(),
                "testproj.list-flows" => ListFlows(),
                "testflow.show" => ShowFlow(GetRequiredArgument(command, "flow")),
                "testflow.tree" => FlowTree(GetRequiredArgument(command, "flow")),
                "testflow.create" => CreateFlow(GetRequiredArgument(command, "name"), GetOptionalArgument(command, "description")),
                "action.list" => ActionList(GetRequiredArgument(command, "flow")),
                "action.tree" => FlowTree(GetRequiredArgument(command, "flow")),
                "action.show" => ShowAction(GetRequiredArgument(command, "flow"), GetRequiredArgument(command, "path")),
                "action.add" => AddAction(command),
                "action.set" => SetAction(command),
                "action.remove" => RemoveAction(command),
                "project.save" => SaveProject(),
                _ => CommandResult.Failure(CommandExecutionMode.UiSession, "UNSUPPORTED", $"Unsupported UI session command '{command.Command}'.", _sessionId)
            };

            return LogAndReturn(command, target, result);
        }
        catch (Exception ex)
        {
            return LogAndReturn(command, target, CommandResult.Failure(
                CommandExecutionMode.UiSession,
                "EXECUTION_ERROR",
                ex.Message,
                _sessionId));
        }
    }

    private CommandResult ShowProject()
    {
        var project = RequireProject();
        return CommandResult.Success(CommandExecutionMode.UiSession, new
        {
            projectPath = Path.GetFullPath(_viewModel.CurrentProjectPath!),
            projectName = project.Model.Name,
            flowCount = project.TestFlows.Count,
            runtimeStatePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_viewModel.CurrentProjectPath!))!, ".fleet")
        }, _sessionId);
    }

    private CommandResult ListFlows()
    {
        var project = RequireProject();
        var rows = project.TestFlows
            .Select(flow =>
            {
                flow.SyncToModel();
                return new
                {
                    flow.Name,
                    flow.FileName,
                    flow.IsEnabled,
                    ActionCount = flow.Actions.Count
                };
            })
            .ToArray();

        return CommandResult.Success(CommandExecutionMode.UiSession, rows, _sessionId);
    }

    private CommandResult ShowFlow(string flowName)
    {
        var flow = RequireFlow(flowName);
        flow.SyncToModel();
        return CommandResult.Success(CommandExecutionMode.UiSession, new
        {
            projectPath = Path.GetFullPath(_viewModel.CurrentProjectPath!),
            flowName = flow.Name,
            flow.FileName,
            flow.Description,
            flow.IsEnabled,
            actionCount = flow.Actions.Count,
            state = flow.State.ToString()
        }, _sessionId);
    }

    private CommandResult FlowTree(string flowName)
    {
        var flow = RequireFlow(flowName);
        flow.SyncToModel();
        var rows = _pathResolver.Flatten(flow.Model).Select(node => new
        {
            node.Path,
            node.Depth,
            node.Container,
            node.Type,
            node.Name,
            node.Description,
            node.IsEnabled
        }).ToArray();

        return CommandResult.Success(CommandExecutionMode.UiSession, rows, _sessionId);
    }

    private CommandResult ActionList(string flowName)
    {
        var flow = RequireFlow(flowName);
        flow.SyncToModel();
        var rows = _pathResolver.Flatten(flow.Model)
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

        return CommandResult.Success(CommandExecutionMode.UiSession, rows, _sessionId);
    }

    private CommandResult ShowAction(string flowName, string path)
    {
        var flow = RequireFlow(flowName);
        flow.SyncToModel();
        var node = _pathResolver.Resolve(flow.Model, path);
        return CommandResult.Success(CommandExecutionMode.UiSession, new
        {
            node.Path,
            node.Container,
            node.Type,
            node.Name,
            node.Description,
            node.IsEnabled,
            node.State,
            Config = _pathResolver.ExtractConfig(node.Action)
        }, _sessionId);
    }

    private CommandResult CreateFlow(string name, string? description)
    {
        var flow = _viewModel.AddTestFlowInMemory(name, description);
        return CommandResult.Success(CommandExecutionMode.UiSession, new
        {
            projectPath = Path.GetFullPath(_viewModel.CurrentProjectPath!),
            flowName = flow.Name,
            flow.FileName,
            flow.Description,
            actionCount = flow.Actions.Count
        }, _sessionId);
    }

    private CommandResult AddAction(CommandEnvelope command)
    {
        var flow = RequireFlow(GetRequiredArgument(command, "flow"));
        flow.SyncToModel();

        var action = _actionMutationService.CreateAction(GetRequiredArgument(command, "type"));
        var path = _actionMutationService.AddAction(
            flow.Model,
            action,
            GetOptionalArgument(command, "parent-path"),
            GetOptionalArgument(command, "container"),
            TryParseOptionalInt(GetOptionalArgument(command, "index"), "index"));

        RefreshFlowAfterMutation(flow);
        return CommandResult.Success(CommandExecutionMode.UiSession, new
        {
            flow = flow.Name,
            path,
            type = action.GetType().Name,
            action.Name,
            action.Description
        }, _sessionId);
    }

    private CommandResult SetAction(CommandEnvelope command)
    {
        var flow = RequireFlow(GetRequiredArgument(command, "flow"));
        flow.SyncToModel();

        var path = GetRequiredArgument(command, "path");
        var property = GetRequiredArgument(command, "property");
        var value = GetRequiredArgument(command, "value");
        _actionMutationService.SetProperty(flow.Model, path, property, value);

        RefreshFlowAfterMutation(flow);
        var node = _pathResolver.Resolve(flow.Model, path);
        var config = _pathResolver.ExtractConfig(node.Action);
        config.TryGetValue(property, out var storedValue);

        return CommandResult.Success(CommandExecutionMode.UiSession, new
        {
            flow = flow.Name,
            path = node.Path,
            property,
            value = storedValue
        }, _sessionId);
    }

    private CommandResult RemoveAction(CommandEnvelope command)
    {
        var flow = RequireFlow(GetRequiredArgument(command, "flow"));
        flow.SyncToModel();

        var path = GetRequiredArgument(command, "path");
        _actionMutationService.RemoveAction(flow.Model, path);
        RefreshFlowAfterMutation(flow);

        return CommandResult.Success(CommandExecutionMode.UiSession, new
        {
            flow = flow.Name,
            removedPath = path,
            actionCount = flow.Actions.Count
        }, _sessionId);
    }

    private CommandResult SaveProject()
    {
        if (!_viewModel.SaveProjectFromSession(out var error))
        {
            return CommandResult.Failure(CommandExecutionMode.UiSession, "SAVE_FAILED", error ?? "Save failed.", _sessionId);
        }

        var project = RequireProject();
        return CommandResult.Success(CommandExecutionMode.UiSession, new
        {
            projectPath = Path.GetFullPath(_viewModel.CurrentProjectPath!),
            projectName = project.Model.Name,
            flowCount = project.TestFlows.Count,
            saved = true
        }, _sessionId);
    }

    private void RefreshFlowAfterMutation(ObservableFlow flow)
    {
        flow.RefreshFromModel();
        flow.HasUnsavedChanges = true;
        _viewModel.ProjectManager.MarkAsModified();
        _viewModel.NotifySessionUiStateChanged();
    }

    private ObservableProject RequireProject()
    {
        return _viewModel.ActiveProject ?? throw new InvalidOperationException("No project is currently loaded.");
    }

    private ObservableFlow RequireFlow(string flowName)
    {
        var flow = _viewModel.FindFlowForSession(flowName);
        if (flow == null)
        {
            throw new InvalidOperationException($"Flow '{flowName}' was not found in the current UI project.");
        }

        return flow;
    }

    private bool ProjectMatches(string? requestedProjectPath)
    {
        var currentPath = _viewModel.CurrentProjectPath;
        return string.Equals(
            UiSessionRegistry.NormalizeProjectPath(currentPath),
            UiSessionRegistry.NormalizeProjectPath(requestedProjectPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadOnlyCommand(string command)
    {
        return command is
            "testproj.show" or
            "testproj.list-flows" or
            "testflow.show" or
            "testflow.tree" or
            "action.list" or
            "action.tree" or
            "action.show";
    }

    private static string GetRequiredArgument(CommandEnvelope command, string name)
    {
        if (command.Arguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Missing required argument '{name}'.");
    }

    private static string? GetOptionalArgument(CommandEnvelope command, string name)
    {
        return command.Arguments.TryGetValue(name, out var value) ? value : null;
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

        throw new InvalidOperationException($"Argument '{optionName}' must be an integer.");
    }

    private CommandResult LogAndReturn(CommandEnvelope command, string target, CommandResult result)
    {
        var status = result.Ok ? "ok" : "failed";
        var message = $"source=CLI/Agent route={_sessionId} command={command.Command} target={target} result={status}";
        if (result.Ok)
        {
            Logger.Info(message);
        }
        else
        {
            Logger.Warn($"{message} error={result.Error?.Code}:{result.Error?.Message}");
        }

        return result;
    }
}
