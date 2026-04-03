using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Project;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using NLog;

namespace FleetAutomate.Model.Actions.Logic
{
    [DataContract]
    public class SubFlowAction : ILogicAction, ICompositeAction, ISyntaxValidator, INotifyPropertyChanged
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public event PropertyChangedEventHandler? PropertyChanged;

        [DataMember]
        private string _targetFlowName;
        public string TargetFlowName
        {
            get => _targetFlowName;
            set
            {
                if (_targetFlowName != value)
                {
                    _targetFlowName = value;
                    _resolvedTargetFlow = null; // Clear cache when name changes
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetFlowName)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        [DataMember]
        private Environment _environment;
        public Environment Environment
        {
            get => _environment;
            set
            {
                if (_environment != value)
                {
                    _environment = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Environment)));
                }
            }
        }

        [IgnoreDataMember]
        private ActionState _state = ActionState.Ready;
        public ActionState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
                }
            }
        }

        [DataMember]
        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
                }
            }
        }

        [DataMember]
        private string _description = "Execute another flow as sub-flow";
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                }
            }
        }

        public string Name => string.IsNullOrWhiteSpace(_targetFlowName)
            ? "SubFlow"
            : $"SubFlow: {_targetFlowName}";

        // Runtime-only properties (not serialized)
        [IgnoreDataMember]
        public TestProject TestProject { get; set; }

        [IgnoreDataMember]
        private TestFlow _resolvedTargetFlow;

        public void Cancel()
        {
            _resolvedTargetFlow?.Cancel();
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            State = ActionState.Running;
            await Task.Yield(); // Allow UI update

            try
            {
                // Validate TargetFlowName
                if (string.IsNullOrWhiteSpace(TargetFlowName))
                {
                    Logger.Error("SubFlowAction: TargetFlowName is null or empty");
                    State = ActionState.Failed;
                    return false;
                }

                // Resolve target flow
                var targetFlow = ResolveTargetFlow();
                if (targetFlow == null)
                {
                    Logger.Error($"SubFlowAction: Target flow '{TargetFlowName}' not found in project");
                    State = ActionState.Failed;
                    return false;
                }

                // Check if target flow is enabled
                if (!targetFlow.IsEnabled)
                {
                    Logger.Warn($"SubFlowAction: Target flow '{TargetFlowName}' is disabled, skipping execution");
                    State = ActionState.Failed;
                    return false;
                }

                // Share environment with target flow
                targetFlow.Environment = this.Environment;

                // Reset target flow state to ensure clean execution
                targetFlow.State = ActionState.Ready;
                targetFlow.CurrentAction = null;

                // Execute target flow
                Logger.Info($"SubFlowAction: Executing sub-flow '{TargetFlowName}'");
                var result = await targetFlow.ExecuteAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    State = ActionState.Paused;
                    Logger.Info($"SubFlowAction: Sub-flow '{TargetFlowName}' was cancelled");
                    return false;
                }

                if (!result)
                {
                    State = ActionState.Failed;
                    Logger.Error($"SubFlowAction: Sub-flow '{TargetFlowName}' failed");
                    return false;
                }

                State = ActionState.Completed;
                Logger.Info($"SubFlowAction: Sub-flow '{TargetFlowName}' completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"SubFlowAction: Exception during sub-flow '{TargetFlowName}' execution: {ex.Message}");
                State = ActionState.Failed;
                return false;
            }
        }

        public ObservableCollection<IAction> GetChildActions()
        {
            // Return the resolved target flow's actions for display in tree
            var targetFlow = ResolveTargetFlow();
            return targetFlow?.Actions ?? new ObservableCollection<IAction>();
        }

        public IEnumerable<SyntaxError> ValidateSyntax(SyntaxValidationContext context)
        {
            var errors = new List<SyntaxError>();

            // 1. Validate target flow name
            if (string.IsNullOrWhiteSpace(TargetFlowName))
            {
                errors.Add(new SyntaxError(this,
                    "SubFlow target flow name cannot be empty",
                    "TargetFlowName",
                    SyntaxErrorSeverity.Critical));
                return errors;
            }

            // 2. Get TestProject from context
            if (context.TestProject == null)
            {
                errors.Add(new SyntaxError(this,
                    "Cannot validate SubFlow: TestProject not available in context",
                    "TargetFlowName",
                    SyntaxErrorSeverity.Warning));
                return errors;
            }

            // 3. Resolve target flow
            var targetFlow = context.TestProject.FindTestFlowByName(TargetFlowName);
            if (targetFlow == null)
            {
                errors.Add(new SyntaxError(this,
                    $"SubFlow target flow '{TargetFlowName}' not found in project",
                    "TargetFlowName",
                    SyntaxErrorSeverity.Critical));
                return errors;
            }

            // 4. Check if target flow is disabled
            if (!targetFlow.IsEnabled)
            {
                errors.Add(new SyntaxError(this,
                    $"SubFlow target flow '{TargetFlowName}' is disabled",
                    "TargetFlowName",
                    SyntaxErrorSeverity.Warning));
            }

            // 5. Detect circular references
            if (context.CurrentFlow != null && !string.IsNullOrWhiteSpace(context.CurrentFlow.Name))
            {
                var visited = new HashSet<string>();
                var recursionStack = new HashSet<string>();

                var circularPath = DetectCircularReference(
                    context.TestProject,
                    context.CurrentFlow.Name,
                    visited,
                    recursionStack);

                if (circularPath != null)
                {
                    errors.Add(new SyntaxError(this,
                        $"Circular reference detected: {string.Join(" → ", circularPath)}",
                        "TargetFlowName",
                        SyntaxErrorSeverity.Critical));
                }
            }

            return errors;
        }

        private TestFlow ResolveTargetFlow()
        {
            // Return cached if available
            if (_resolvedTargetFlow != null)
                return _resolvedTargetFlow;

            // Look up and cache
            if (TestProject != null && !string.IsNullOrWhiteSpace(TargetFlowName))
            {
                _resolvedTargetFlow = TestProject.FindTestFlowByName(TargetFlowName);
            }

            return _resolvedTargetFlow;
        }

        private List<string> DetectCircularReference(
            TestProject project,
            string flowName,
            HashSet<string> visited,
            HashSet<string> recursionStack)
        {
            // Add current flow to recursion stack
            recursionStack.Add(flowName);

            var flow = project.FindTestFlowByName(flowName);
            if (flow == null)
            {
                recursionStack.Remove(flowName);
                return null;
            }

            // Recursively check all SubFlowActions
            var subFlowActions = FindAllSubFlowActions(flow);
            foreach (var subFlowAction in subFlowActions)
            {
                var targetName = subFlowAction.TargetFlowName;
                if (string.IsNullOrWhiteSpace(targetName))
                    continue;

                // Cycle detected!
                if (recursionStack.Contains(targetName))
                {
                    // Build circular path for error message
                    var path = new List<string>(recursionStack) { targetName };
                    return path;
                }

                // Recurse if not visited
                if (!visited.Contains(targetName))
                {
                    var cyclePath = DetectCircularReference(project, targetName, visited, recursionStack);
                    if (cyclePath != null)
                    {
                        return cyclePath;
                    }
                }
            }

            // Remove from recursion stack and mark as visited
            recursionStack.Remove(flowName);
            visited.Add(flowName);
            return null;
        }

        private IEnumerable<SubFlowAction> FindAllSubFlowActions(TestFlow flow)
        {
            // Recursively find all SubFlowActions in the flow
            foreach (var action in flow.Actions)
            {
                if (action is SubFlowAction subFlowAction)
                {
                    yield return subFlowAction;
                }

                // Check nested actions in composite actions
                if (action is ICompositeAction compositeAction)
                {
                    foreach (var subAction in FindSubFlowActionsRecursive(compositeAction.GetChildActions()))
                    {
                        yield return subAction;
                    }
                }
            }
        }

        private IEnumerable<SubFlowAction> FindSubFlowActionsRecursive(ObservableCollection<IAction> actions)
        {
            foreach (var action in actions)
            {
                if (action is SubFlowAction subFlowAction)
                {
                    yield return subFlowAction;
                }

                if (action is ICompositeAction compositeAction)
                {
                    foreach (var subAction in FindSubFlowActionsRecursive(compositeAction.GetChildActions()))
                    {
                        yield return subAction;
                    }
                }
            }
        }
    }
}
