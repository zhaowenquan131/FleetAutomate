using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.UIAutomation;
using Canvas.TestRunner.Model.Actions;
using FleetAutomate.Model.Flow;

using System.Collections.ObjectModel;
using System.Runtime.Serialization;

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
    [KnownType(typeof(IfWindowContainsTextAction))]
    [KnownType(typeof(NotImplementedAction))]
    public partial class TestFlow : ILogicAction
    {
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
            _cancellationTokenSource.Cancel();
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            var leftActions = Actions;
            if (State == ActionState.Paused || State == ActionState.Failed)
            {
                leftActions = [.. Actions.SkipWhile(a => a != CurrentAction)];
            }

            State = ActionState.Running;
            foreach (var action in leftActions)
            {
                CurrentAction = action;
                try
                {
                    if (CurrentAction is ILogicAction logicAction)
                    {
                        logicAction.Environment = Environment;
                    }
                    var rst = await CurrentAction.ExecuteAsync(cancellationToken);
                    if (!rst)
                    {
                        return false;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    State = ActionState.Paused;
                    return true;
                }
                catch (Exception ex)
                {
                    State = ActionState.Failed;
                    return false;
                }
            }
            State = ActionState.Completed;
            return true;
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
