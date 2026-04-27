using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.UIAutomation;
using Canvas.TestRunner.Model.Actions;
using FleetAutomate.Model.Flow;
using FlaUI.Core.AutomationElements;

using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using NLog;
using LogAction = FleetAutomate.Model.Actions.System.LogAction;

namespace FleetAutomate.Model.Flow
{

    public enum ActionState
    {
        Ready,
        Running,
        Paused,
        Completed,
        Failed
    }

    public enum TestFlowBreakReason
    {
        None,
        PauseRequested,
        StepCompleted,
        ActionFailed,
        Completed,
        Stopped
    }

    [DataContract]
    [KnownType(typeof(SetVariableAction<object>))]
    [KnownType(typeof(WhileLoopAction))]
    [KnownType(typeof(ForLoopAction))]
    [KnownType(typeof(IfAction))]
    [KnownType(typeof(SubFlowAction))]
    [KnownType(typeof(LaunchApplicationAction))]
    [KnownType(typeof(WaitDurationAction))]
    [KnownType(typeof(WaitForElementAction))]
    [KnownType(typeof(ClickElementAction))]
    [KnownType(typeof(SetTextAction))]
    [KnownType(typeof(IfWindowContainsTextAction))]
    [KnownType(typeof(NotImplementedAction))]
    [KnownType(typeof(LogAction))]
    public partial class TestFlow : ILogicAction
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public TestFlow(CancellationTokenSource tokenSource)
        {
            Actions = [];
            _cancellationTokenSource = tokenSource ?? throw new ArgumentNullException(nameof(tokenSource), "CancellationTokenSource cannot be null.");
        }

        // Parameterless constructor for serialization
        public TestFlow() : this(new CancellationTokenSource())
        {
        }

        private CancellationTokenSource _cancellationTokenSource;


        [IgnoreDataMember]
        public IAction CurrentAction { get; set; }

        [IgnoreDataMember]
        public IAction? LastFailedAction { get; private set; }

        [DataMember]
        public string Name { get; set; }

        public ObservableCollection<IAction> Actions { get; set; } = [];

        /// <summary>
        /// Serialization property for Actions collection.
        /// </summary>
        [DataMember]
        public IAction[] ActionsArray
        {
            get => [.. Actions];
            set
            {
                Actions ??= [];
                Actions.Clear();
                if (value != null)
                {
                    foreach (var action in value)
                    {
                        Actions.Add(action);
                    }
                }
            }
        }

        [DataMember]
        public Actions.Logic.Environment Environment { get; set; } = new Actions.Logic.Environment();

        /// <summary>
        /// Global element dictionary for storing UI automation elements.
        /// Keys are defined at design time, values are populated at runtime.
        /// </summary>
        [DataMember]
        public GlobalElementDictionary GlobalElementDictionary { get; set; } = new GlobalElementDictionary();

        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public bool IsEnabled { get; set; } = true;
        [IgnoreDataMember]
        public ActionState State { get; set; } = ActionState.Ready;

        [IgnoreDataMember]
        public TestFlowBreakReason BreakReason { get; private set; } = TestFlowBreakReason.None;

        [IgnoreDataMember] // Don't serialize file path, this is for runtime use
        public string FileName { get; set; }

        /// <summary>
        /// Reference to the parent TestProject (not serialized, set at runtime).
        /// Used by SubFlowAction to look up target flows.
        /// </summary>
        [IgnoreDataMember]
        public Project.TestProject ParentProject { get; set; }

        [IgnoreDataMember]
        private bool _pauseRequested;

        public void Cancel()
        {
            Pause();
        }

        public void Pause()
        {
            _pauseRequested = true;

            if (CanInterruptCurrentActionForPause())
            {
                CurrentAction?.Cancel();
                _cancellationTokenSource?.Cancel();
            }
        }

        public void Stop()
        {
            _pauseRequested = false;
            CurrentAction?.Cancel();
            _cancellationTokenSource?.Cancel();
            State = ActionState.Ready;
            BreakReason = TestFlowBreakReason.Stopped;
            CurrentAction = null!;
        }

        /// <summary>
        /// Prepares for execution by ensuring we have a valid CancellationTokenSource.
        /// </summary>
        private void PrepareForExecution()
        {
            // If the CancellationTokenSource is cancelled or disposed, create a new one
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        public Task<bool> StartAsync(CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(GetFirstRunnableAction(), resetStates: true, stepOnce: false, cancellationToken);
        }

        public Task<bool> StartFromActionAsync(IAction startAction, CancellationToken cancellationToken)
        {
            if (!Actions.Contains(startAction))
            {
                throw new ArgumentException("The specified action is not part of this TestFlow.", nameof(startAction));
            }

            ResetActionStatesFrom(startAction);
            LastFailedAction = null;
            return ExecuteCoreAsync(startAction, resetStates: false, stepOnce: false, cancellationToken);
        }

        public Task<bool> StartFromActionIndexAsync(int actionIndex, CancellationToken cancellationToken)
        {
            if (actionIndex < 0 || actionIndex >= Actions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(actionIndex));
            }

            return StartFromActionAsync(Actions[actionIndex], cancellationToken);
        }

        public Task<bool> ContinueAsync(CancellationToken cancellationToken)
        {
            var resumeAction = CurrentAction ?? GetFirstRunnableAction();
            return ExecuteCoreAsync(resumeAction, resetStates: false, stepOnce: false, cancellationToken);
        }

        public Task<bool> StepAsync(CancellationToken cancellationToken)
        {
            var resumeAction = CurrentAction ?? GetFirstRunnableAction();
            return ExecuteCoreAsync(resumeAction, resetStates: State is not ActionState.Paused and not ActionState.Failed, stepOnce: true, cancellationToken);
        }

        public Task<bool> StepActionAsync(IAction action, CancellationToken cancellationToken)
        {
            if (!Actions.Contains(action))
            {
                throw new ArgumentException("The specified action is not part of this TestFlow.", nameof(action));
            }

            return ExecuteCoreAsync(action, resetStates: true, stepOnce: true, cancellationToken);
        }

        public Task<bool> SkipFailedActionAndContinueAsync(CancellationToken cancellationToken)
        {
            if (State != ActionState.Failed || CurrentAction == null)
            {
                return ContinueAsync(cancellationToken);
            }

            var nextAction = GetNextAction(CurrentAction);
            if (nextAction == null)
            {
                State = ActionState.Completed;
                BreakReason = TestFlowBreakReason.Completed;
                CurrentAction = null!;
                return Task.FromResult(true);
            }

            CurrentAction = nextAction;
            return ExecuteCoreAsync(nextAction, resetStates: false, stepOnce: false, cancellationToken);
        }

        public IReadOnlyDictionary<string, object?> GetRuntimeVariableValues()
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var variable in Environment.Variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Name))
                {
                    continue;
                }

                values[variable.Name] = variable.Value;
            }

            return values;
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            return await ContinueAsync(cancellationToken);
        }

        /// <summary>
        /// Executes the TestFlow starting from the specified action.
        /// </summary>
        /// <param name="startAction">The action to start execution from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if execution completed successfully, false otherwise.</returns>
        public async Task<bool> ExecuteFromAction(IAction startAction, CancellationToken cancellationToken)
        {
            return await StartFromActionAsync(startAction, cancellationToken);
        }

        /// <summary>
        /// Initializes runtime objects after deserialization.
        /// </summary>
        public void InitializeAfterDeserialization()
        {
            if (_cancellationTokenSource == null)
            {
                _cancellationTokenSource = new CancellationTokenSource();
            }
            State = ActionState.Ready;
            BreakReason = TestFlowBreakReason.None;
            LastFailedAction = null;
            CurrentAction = null!;

            // Initialize GlobalElementDictionary if null
            GlobalElementDictionary ??= new GlobalElementDictionary();
            // Clear runtime element values
            GlobalElementDictionary.ClearRuntimeElements();

            // Reset all action states to Ready when project is opened
            ResetAllActionStates();
        }

        /// <summary>
        /// Recursively resets all action states to Ready, including nested composite actions.
        /// </summary>
        private void ResetAllActionStates()
        {
            // Clear runtime element values for fresh execution
            GlobalElementDictionary?.ClearRuntimeElements();

            foreach (var action in Actions)
            {
                ResetActionState(action);
            }
        }

        private void ResetActionStatesFrom(IAction startAction)
        {
            var startIndex = Actions.IndexOf(startAction);
            if (startIndex < 0)
            {
                throw new ArgumentException("The specified action is not part of this TestFlow.", nameof(startAction));
            }

            for (var index = startIndex; index < Actions.Count; index++)
            {
                ResetActionState(Actions[index]);
            }
        }

        /// <summary>
        /// Recursively resets an action's state to Ready, including all child actions if it's a composite action.
        /// </summary>
        /// <param name="action">The action to reset.</param>
        private void ResetActionState(IAction action)
        {
            action.State = ActionState.Ready;

            // Handle composite actions (actions with child actions)
            if (action is ICompositeAction compositeAction)
            {
                var childActions = compositeAction.GetChildActions();
                if (childActions != null)
                {
                    foreach (var childAction in childActions)
                    {
                        ResetActionState(childAction);
                    }
                }
            }

            // Handle IfAction specifically since it has multiple blocks
            if (action is Actions.Logic.IfAction ifAction)
            {
                // Reset IfBlock actions
                foreach (var ifBlockAction in ifAction.IfBlock)
                {
                    ResetActionState(ifBlockAction);
                }

                // Reset ElseBlock actions
                foreach (var elseBlockAction in ifAction.ElseBlock)
                {
                    ResetActionState(elseBlockAction);
                }

                // Reset ElseIf actions (which are themselves IfActions)
                foreach (var elseIfAction in ifAction.ElseIfs)
                {
                    ResetActionState(elseIfAction);
                }
            }
        }

        private async Task<bool> ExecuteCoreAsync(IAction? startAction, bool resetStates, bool stepOnce, CancellationToken cancellationToken)
        {
            PrepareForExecution();

            if (resetStates)
            {
                ResetAllActionStates();
                LastFailedAction = null;
            }

            var action = startAction ?? GetFirstRunnableAction();
            if (action == null)
            {
                State = ActionState.Completed;
                BreakReason = TestFlowBreakReason.Completed;
                CurrentAction = null!;
                return true;
            }

            var startIndex = Actions.IndexOf(action);
            if (startIndex < 0)
            {
                throw new ArgumentException("The specified action is not part of this TestFlow.", nameof(startAction));
            }

            _pauseRequested = false;
            BreakReason = TestFlowBreakReason.None;
            State = ActionState.Running;
            await Task.Yield();

            for (var index = startIndex; index < Actions.Count; index++)
            {
                var current = Actions[index];
                var next = index + 1 < Actions.Count ? Actions[index + 1] : null;

                CurrentAction = current;
                await Task.Yield();

                try
                {
                    InjectExecutionContext(current);
                    var result = await current.ExecuteAsync(_cancellationTokenSource.Token);
                    if (!result)
                    {
                        if (_cancellationTokenSource.IsCancellationRequested)
                        {
                            State = ActionState.Paused;
                            BreakReason = TestFlowBreakReason.PauseRequested;
                            CurrentAction = current;
                            await Task.Yield();
                            return true;
                        }

                        State = ActionState.Failed;
                        BreakReason = TestFlowBreakReason.ActionFailed;
                        LastFailedAction = current;
                        CurrentAction = current;
                        await Task.Yield();
                        return false;
                    }

                    if (stepOnce)
                    {
                        if (next == null)
                        {
                            State = ActionState.Completed;
                            BreakReason = TestFlowBreakReason.Completed;
                            CurrentAction = null!;
                            await Task.Yield();
                            return true;
                        }

                        State = ActionState.Paused;
                        BreakReason = TestFlowBreakReason.StepCompleted;
                        CurrentAction = next;
                        await Task.Yield();
                        return true;
                    }

                    if (_pauseRequested)
                    {
                        if (next == null)
                        {
                            State = ActionState.Completed;
                            BreakReason = TestFlowBreakReason.Completed;
                            CurrentAction = null!;
                            await Task.Yield();
                            return true;
                        }

                        State = ActionState.Paused;
                        BreakReason = TestFlowBreakReason.PauseRequested;
                        CurrentAction = next;
                        await Task.Yield();
                        return true;
                    }

                    await Task.Yield();
                }
                catch (OperationCanceledException)
                {
                    State = ActionState.Paused;
                    BreakReason = TestFlowBreakReason.PauseRequested;
                    CurrentAction = current;
                    await Task.Yield();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"[TestFlow] ERROR executing action '{current.Name}': {ex.GetType().Name} - {ex.Message}");
                    State = ActionState.Failed;
                    BreakReason = TestFlowBreakReason.ActionFailed;
                    LastFailedAction = current;
                    CurrentAction = current;
                    await Task.Yield();
                    return false;
                }
            }

            State = ActionState.Completed;
            BreakReason = TestFlowBreakReason.Completed;
            CurrentAction = null!;
            await Task.Yield();
            return true;
        }

        private IAction? GetFirstRunnableAction()
        {
            return Actions.FirstOrDefault();
        }

        private IAction? GetNextAction(IAction action)
        {
            var index = Actions.IndexOf(action);
            if (index < 0 || index + 1 >= Actions.Count)
            {
                return null;
            }

            return Actions[index + 1];
        }

        private void InjectExecutionContext(IAction action)
        {
            if (action is ILogicAction logicAction)
            {
                logicAction.Environment = Environment;
            }

            if (action is IUIElementAction uiElementAction)
            {
                uiElementAction.ElementDictionary = GlobalElementDictionary;
            }
        }

        private bool CanInterruptCurrentActionForPause()
        {
            if (CurrentAction is not IPauseAwareAction pauseAwareAction)
            {
                return false;
            }

            return pauseAwareAction.PauseBehavior != ActionPauseBehavior.None;
        }

        /// <summary>
        /// Validates the entire Flow and returns all syntax errors found.
        /// </summary>
        /// <param name="options">Validation options. If null, default options are used.</param>
        /// <returns>A collection of syntax errors found in the Flow.</returns>
        public IEnumerable<SyntaxError> ValidateSyntax(SyntaxValidationOptions? options = null)
        {
            return FlowValidator.ValidateFlow(this, options);
        }

        /// <summary>
        /// Checks if the Flow has any syntax errors.
        /// </summary>
        /// <param name="options">Validation options. If null, default options are used.</param>
        /// <returns>True if the Flow has syntax errors, false otherwise.</returns>
        public bool HasSyntaxErrors(SyntaxValidationOptions? options = null)
        {
            return ValidateSyntax(options).Any(e => e.Severity >= SyntaxErrorSeverity.Error);
        }

        /// <summary>
        /// Gets a summary of syntax validation results.
        /// </summary>
        /// <param name="options">Validation options. If null, default options are used.</param>
        /// <returns>A summary object containing error counts and details.</returns>
        public FlowValidationSummary GetValidationSummary(SyntaxValidationOptions? options = null)
        {
            var errors = ValidateSyntax(options).ToList();
            return new FlowValidationSummary
            {
                TotalErrors = errors.Count,
                CriticalErrors = errors.Count(e => e.Severity == SyntaxErrorSeverity.Critical),
                Errors = errors.Count(e => e.Severity == SyntaxErrorSeverity.Error),
                Warnings = errors.Count(e => e.Severity == SyntaxErrorSeverity.Warning),
                AllErrors = errors,
                IsValid = !errors.Any(e => e.Severity >= SyntaxErrorSeverity.Error)
            };
        }
    }
}
