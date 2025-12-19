using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.UIAutomation;
using Canvas.TestRunner.Model.Actions;
using FleetAutomate.Model.Flow;

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

    [DataContract]
    [KnownType(typeof(SetVariableAction<object>))]
    [KnownType(typeof(WhileLoopAction))]
    [KnownType(typeof(ForLoopAction))]
    [KnownType(typeof(IfAction))]
    [KnownType(typeof(LaunchApplicationAction))]
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


        [DataMember]
        public IAction CurrentAction { get; set; }

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

        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public bool IsEnabled { get; set; } = true;
        [DataMember]
        public ActionState State { get; set; } = ActionState.Ready;

        [DataMember] // Don't serialize file path, this is for runtime use
        public string FileName { get; set; }

        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
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

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            // Prepare for execution (recreate CancellationTokenSource if needed)
            PrepareForExecution();

            var leftActions = Actions;
            if (State == ActionState.Paused || State == ActionState.Failed)
            {
                leftActions = [.. Actions.SkipWhile(a => a != CurrentAction)];
            }
            else
            {
                // Reset all action states to Ready when starting a fresh execution
                ResetAllActionStates();
            }

            State = ActionState.Running;
            // Yield to allow UI to update the TestFlow state
            await Task.Yield();

            foreach (var action in leftActions)
            {
                CurrentAction = action;
                // Yield to allow UI to update CurrentAction indicator
                await Task.Yield();

                try
                {
                    if (CurrentAction is ILogicAction logicAction)
                    {
                        logicAction.Environment = Environment;
                    }
                    // Use our own CancellationToken so we can cancel and resume
                    var rst = await CurrentAction.ExecuteAsync(_cancellationTokenSource.Token);
                    if (!rst)
                    {
                        // Check if cancellation was requested (user clicked Pause)
                        if (_cancellationTokenSource.IsCancellationRequested)
                        {
                            State = ActionState.Paused;
                            await Task.Yield();
                            return true;
                        }
                        // Otherwise, it's a real failure
                        State = ActionState.Failed;
                        await Task.Yield();
                        return false;
                    }

                    // Yield after each action to allow UI to update action states
                    await Task.Yield();
                }
                catch (OperationCanceledException ex)
                {
                    State = ActionState.Paused;
                    await Task.Yield();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"[TestFlow] ERROR executing action '{CurrentAction?.Name}': {ex.GetType().Name} - {ex.Message}");
                    State = ActionState.Failed;
                    await Task.Yield();
                    return false;
                }
            }
            State = ActionState.Completed;
            await Task.Yield();
            return true;
        }

        /// <summary>
        /// Executes the TestFlow starting from the specified action.
        /// </summary>
        /// <param name="startAction">The action to start execution from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if execution completed successfully, false otherwise.</returns>
        public async Task<bool> ExecuteFromAction(IAction startAction, CancellationToken cancellationToken)
        {
            // Verify the action exists in the Actions collection
            if (!Actions.Contains(startAction))
            {
                throw new ArgumentException("The specified action is not part of this TestFlow.", nameof(startAction));
            }

            // Prepare for execution (recreate CancellationTokenSource if needed)
            PrepareForExecution();

            // Set the current action and state to Paused so ExecuteAsync will use SkipWhile logic
            CurrentAction = startAction;
            State = ActionState.Paused;

            // Reset all action states to Ready (except those before startAction)
            ResetAllActionStates();

            // Call ExecuteAsync which will start from CurrentAction
            return await ExecuteAsync(cancellationToken);
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
            CurrentAction = null!;

            // Reset all action states to Ready when project is opened
            ResetAllActionStates();
        }

        /// <summary>
        /// Recursively resets all action states to Ready, including nested composite actions.
        /// </summary>
        private void ResetAllActionStates()
        {
            foreach (var action in Actions)
            {
                ResetActionState(action);
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
