using FleetAutomate.Model.Flow;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.UndoRedo;
using FleetAutomate.ViewModel;
using NLog;
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
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

        var action = _actionMutationService.CreateAction(GetRequiredArgument(command, "type"));
        var target = ResolveTargetCollection(
            flow,
            GetOptionalArgument(command, "parent-path"),
            GetOptionalArgument(command, "container"));
        var insertIndex = TryParseOptionalInt(GetOptionalArgument(command, "index"), "index") ?? target.Actions.Count;
        if (insertIndex < 0 || insertIndex > target.Actions.Count)
        {
            throw new InvalidOperationException($"Insert index {insertIndex} is out of range for container '{target.ContainerName}'.");
        }

        flow.UndoRedoService.Execute(new AddActionEdit(target.CollectionRef, insertIndex, action));
        var path = GetPathForInsertedAction(target, insertIndex);
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

        var path = GetRequiredArgument(command, "path");
        var property = GetRequiredArgument(command, "property");
        var value = GetRequiredArgument(command, "value");
        var node = ResolveActionLocation(flow, path);
        var propertyInfo = node.Action.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            ?? throw new InvalidOperationException($"Property '{property}' does not exist on action type '{node.Action.GetType().Name}'.");

        if (!propertyInfo.CanWrite || propertyInfo.SetMethod == null || !propertyInfo.SetMethod.IsPublic)
        {
            throw new InvalidOperationException($"Property '{propertyInfo.Name}' on action type '{node.Action.GetType().Name}' is not writable.");
        }

        if (propertyInfo.GetCustomAttribute<IgnoreDataMemberAttribute>() != null)
        {
            throw new InvalidOperationException($"Property '{propertyInfo.Name}' cannot be set because it is runtime-only.");
        }

        var oldValue = propertyInfo.GetValue(node.Action);
        var converted = _actionMutationService.ConvertPropertyValue(value, propertyInfo.PropertyType);
        flow.UndoRedoService.Execute(new ActionPropertyEdit(node.ActionPath, propertyInfo.Name, oldValue, converted, $"Set {propertyInfo.Name}"));
        RefreshFlowAfterMutation(flow);
        var updated = _pathResolver.Resolve(flow.Model, path);
        var config = _pathResolver.ExtractConfig(updated.Action);
        config.TryGetValue(propertyInfo.Name, out var storedValue);

        return CommandResult.Success(CommandExecutionMode.UiSession, new
        {
            flow = flow.Name,
            path = updated.Path,
            property = propertyInfo.Name,
            value = storedValue
        }, _sessionId);
    }

    private CommandResult RemoveAction(CommandEnvelope command)
    {
        var flow = RequireFlow(GetRequiredArgument(command, "flow"));

        var path = GetRequiredArgument(command, "path");
        var location = ResolveActionLocation(flow, path);
        flow.UndoRedoService.Execute(new RemoveActionEdit(location.CollectionRef, location.Index, location.Action));
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
        flow.SyncStructureToModelPreservingDirty();
        flow.RefreshUndoRedoState();
        _viewModel.ProjectManager.MarkAsModified();
        _viewModel.NotifySessionUiStateChanged();
    }

    private TargetCollection ResolveTargetCollection(ObservableFlow flow, string? parentPath, string? containerName)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            var rootContainer = string.IsNullOrWhiteSpace(containerName) ? "root" : containerName;
            if (!rootContainer.Equals("root", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Container can only be 'root' when parent-path is omitted.");
            }

            return new TargetCollection(flow.Actions, ActionCollectionRef.Root, "root", null);
        }

        var parent = ResolveActionLocation(flow, parentPath);
        var container = string.IsNullOrWhiteSpace(containerName) ? "children" : containerName;

        return parent.Action switch
        {
            IfAction ifAction => container.ToLowerInvariant() switch
            {
                "if" => new TargetCollection(ifAction.IfBlock, ActionCollectionRef.ForIfBlock(parent.ActionPath), "if", parent.Path),
                "else" => new TargetCollection(ifAction.ElseBlock, ActionCollectionRef.ForElseBlock(parent.ActionPath), "else", parent.Path),
                "children" => new TargetCollection(ifAction.GetChildActions(), ActionCollectionRef.ForIfBlock(parent.ActionPath), "children", parent.Path),
                _ => throw new InvalidOperationException($"Container '{container}' is not supported for IfAction. Use if, else, or children.")
            },
            WhileLoopAction whileLoop when container.Equals("children", StringComparison.OrdinalIgnoreCase)
                => new TargetCollection(whileLoop.Body, ActionCollectionRef.ForLoopBody(parent.ActionPath), "children", parent.Path),
            ForLoopAction forLoop when container.Equals("children", StringComparison.OrdinalIgnoreCase)
                => new TargetCollection(forLoop.Body, ActionCollectionRef.ForLoopBody(parent.ActionPath), "children", parent.Path),
            ICompositeAction
                => throw new InvalidOperationException($"Container '{container}' is not supported for composite action '{parent.Type}'. Use children."),
            _ => throw new InvalidOperationException($"Action '{parent.Path}' of type '{parent.Type}' does not contain child action collections.")
        };
    }

    private ActionLocation ResolveActionLocation(ObservableFlow flow, string path)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new InvalidOperationException($"Action path '{path}' is invalid.");
        }

        ObservableCollection<IAction> collection = flow.Actions;
        var collectionRef = ActionCollectionRef.Root;
        var actionPath = new List<int>();
        IAction? action = null;
        var index = -1;

        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out index))
            {
                throw new InvalidOperationException($"Action path '{path}' is invalid.");
            }

            if (index < 0 || index >= collection.Count)
            {
                throw new InvalidOperationException($"Action path '{path}' does not exist.");
            }

            action = collection[index];
            actionPath.Add(index);

            if (i == parts.Length - 1)
            {
                return new ActionLocation(collection, collectionRef, index, actionPath.ToArray(), action, path, action.GetType().Name);
            }

            var marker = parts[++i];
            switch (action)
            {
                case IfAction ifAction when marker.Equals("if", StringComparison.OrdinalIgnoreCase) ||
                                            marker.Equals("children", StringComparison.OrdinalIgnoreCase):
                    collection = ifAction.IfBlock;
                    collectionRef = ActionCollectionRef.ForIfBlock(actionPath.ToArray());
                    break;
                case IfAction ifAction when marker.Equals("else", StringComparison.OrdinalIgnoreCase):
                    collection = ifAction.ElseBlock;
                    collectionRef = ActionCollectionRef.ForElseBlock(actionPath.ToArray());
                    break;
                case WhileLoopAction whileLoop when marker.Equals("children", StringComparison.OrdinalIgnoreCase):
                    collection = whileLoop.Body;
                    collectionRef = ActionCollectionRef.ForLoopBody(actionPath.ToArray());
                    break;
                case ForLoopAction forLoop when marker.Equals("children", StringComparison.OrdinalIgnoreCase):
                    collection = forLoop.Body;
                    collectionRef = ActionCollectionRef.ForLoopBody(actionPath.ToArray());
                    break;
                default:
                    throw new InvalidOperationException($"Container '{marker}' is not supported for action path '{path}'.");
            }
        }

        throw new InvalidOperationException($"Action path '{path}' is invalid.");
    }

    private static string GetPathForInsertedAction(TargetCollection target, int insertIndex)
    {
        if (string.IsNullOrEmpty(target.ParentPath))
        {
            return insertIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var prefix = target.ContainerName.Equals("children", StringComparison.OrdinalIgnoreCase)
            ? $"{target.ParentPath}.children"
            : $"{target.ParentPath}.{target.ContainerName}";

        return $"{prefix}.{insertIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    private sealed record TargetCollection(ObservableCollection<IAction> Actions, ActionCollectionRef CollectionRef, string ContainerName, string? ParentPath);

    private sealed record ActionLocation(ObservableCollection<IAction> Actions, ActionCollectionRef CollectionRef, int Index, int[] ActionPath, IAction Action, string Path, string Type);

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
