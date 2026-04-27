using CommunityToolkit.Mvvm.ComponentModel;

using FleetAutomate.Model.Flow;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.Serialization;

namespace FleetAutomate.Model.Actions.Logic
{
    [ObservableObject]
    [DataContract]
    [KnownType(typeof(SetVariableAction<object>))]
    [KnownType(typeof(Loops.WhileLoopAction))]
    [KnownType(typeof(Loops.ForLoopAction))]
    [KnownType(typeof(IfAction))]
    [KnownType(typeof(SubFlowAction))]
    [KnownType(typeof(TestFlow))]
    [KnownType(typeof(System.LaunchApplicationAction))]
    [KnownType(typeof(System.LogAction))]
    [KnownType(typeof(UIAutomation.WaitForElementAction))]
    [KnownType(typeof(UIAutomation.ClickElementAction))]
    [KnownType(typeof(Expression.UIElementExistsExpression))]
    [KnownType(typeof(Expression.EqualExpression))]
    [KnownType(typeof(Expression.NotEqualExpression))]
    [KnownType(typeof(Expression.GreaterThanExpression))]
    [KnownType(typeof(Expression.GreaterThanOrEqualExpression))]
    [KnownType(typeof(Expression.SmallerThanExpression))]
    [KnownType(typeof(Expression.SmallerThanOrEqualExpression))]
    [KnownType(typeof(Expression.LiteralExpression<bool>))]
    [KnownType(typeof(Expression.LiteralExpression<int>))]
    [KnownType(typeof(Expression.LiteralExpression<double>))]
    [KnownType(typeof(Expression.LiteralExpression<string>))]
    public partial class IfAction : ILogicAction, ICompositeAction, IPauseAwareAction
    {
        public ActionPauseBehavior PauseBehavior => ActionPauseBehavior.Cooperative;

        public IfAction()
        {
            if (ElseIfs.Count > 1)
            {
                for (int i = 1; i < ElseIfs.Count; i++)
                {
                    ElseIfs[i - 1].ElseBlock.Add(ElseIfs[i]);
                }
            }
            else if (ElseIfs.Count == 1)
            {
                ElseBlock.Add(ElseIfs[0]);
            }

            // Initialize ChildActions collection
            RefreshChildActions();

            // Attach collection changed handler using stored delegate
            EnsureIfBlockHandlerAttached();
        }

        /// <summary>
        /// Ensures the IfBlock.CollectionChanged handler is properly attached.
        /// Can be called multiple times safely - detaches and reattaches.
        /// </summary>
        private void EnsureIfBlockHandlerAttached()
        {
            // Create stored handler if not exists
            _ifBlockCollectionChangedHandler ??= (s, e) => RefreshChildActions();

            // Remove existing handler if attached
            IfBlock.CollectionChanged -= _ifBlockCollectionChangedHandler;

            // Attach the stored handler
            IfBlock.CollectionChanged += _ifBlockCollectionChangedHandler;
        }

        [ObservableProperty]
        [DataMember(Name = "Condition")]
        private object _condition;

        partial void OnConditionChanged(object value)
        {
            OnPropertyChanged(nameof(Name));
        }

        [ObservableProperty]
        [DataMember(Name = "Environment")]
        private Environment _environment;

        public string Name
        {
            get
            {
                if (Condition == null)
                {
                    return "If Action";
                }

                // Handle UIElementExistsExpression
                if (Condition is Expression.UIElementExistsExpression uiElementExpr)
                {
                    string elementDesc = Helpers.ElementDescriptionHelper.ExtractElementDescription(uiElementExpr.ElementIdentifier, uiElementExpr.IdentifierType);
                    return $"If {elementDesc} exists";
                }

                // Handle boolean expressions with RawText
                if (Condition is ExpressionBase<bool> boolExpr && !string.IsNullOrWhiteSpace(boolExpr.RawText))
                {
                    string expr = boolExpr.RawText.Trim();
                    // Limit expression length for display
                    if (expr.Length > 50)
                    {
                        expr = string.Concat(expr.AsSpan(0, 47), "...");
                    }
                    return $"If {expr}";
                }

                // Handle literal boolean values
                if (Condition is bool boolVal)
                {
                    return $"If {boolVal.ToString().ToLower()}";
                }

                // Fallback
                return "If Action";
            }
        }

        [ObservableProperty]
        [DataMember(Name = "Description")]
        private string _description = "Execute Actions in one of two given blocks by condition";

        [ObservableProperty]
        [DataMember(Name = "IsEnabled")]
        private bool _isEnabled = true;

        public ObservableCollection<IAction> IfBlock { get; set; } = [];

        public ObservableCollection<IfAction> ElseIfs { get; set; } = [];

        public ObservableCollection<IAction> ElseBlock { get; set; } = [];

        [ObservableProperty]
        [IgnoreDataMember]
        private ActionState _state;

        /// <summary>
        /// Tracks whether the ElseBlock is visible as a pseudo-node in the TreeView.
        /// </summary>
        [ObservableProperty]
        private bool _showElseBlock = false;

        /// <summary>
        /// The observable collection of child actions (IfBlock + optional ElseBlock pseudo-node).
        /// This gets updated whenever ShowElseBlock changes.
        /// Not serialized - derived from IfBlock and ShowElseBlock state.
        /// </summary>
        [ObservableProperty]
        [IgnoreDataMember]
        private ObservableCollection<IAction> _childActions = [];

        /// <summary>
        /// Stored event handler for IfBlock.CollectionChanged to allow proper attach/detach.
        /// </summary>
        private NotifyCollectionChangedEventHandler? _ifBlockCollectionChangedHandler;

        [IgnoreDataMember]
        private IfResumeBranch? _resumeBranch;

        [IgnoreDataMember]
        private int _resumeIndex;

        private enum IfResumeBranch
        {
            If,
            Else
        }


        /// <summary>
        /// XML serialization property for IfBlock collection.
        /// </summary>
        [DataMember]
        public IAction[] IfBlockArray
        {
            get => [.. IfBlock];
            set
            {
                IfBlock ??= [];
                IfBlock.Clear();
                if (value != null)
                {
                    foreach (var action in value)
                    {
                        IfBlock.Add(action);
                    }
                }
                // Refresh display collection after IfBlock is populated
                RefreshChildActions();

                // Ensure CollectionChanged handler is attached (may be bypassed during deserialization)
                EnsureIfBlockHandlerAttached();
            }
        }

        /// <summary>
        /// XML serialization property for ElseIfs collection.
        /// </summary>
        [DataMember]
        public IfAction[] ElseIfsArray
        {
            get => [.. ElseIfs];
            set
            {
                ElseIfs ??= [];
                ElseIfs.Clear();
                if (value != null)
                {
                    foreach (var action in value)
                    {
                        ElseIfs.Add(action);
                    }
                }
            }
        }

        /// <summary>
        /// XML serialization property for ElseBlock collection.
        /// </summary>
        [DataMember]
        public IAction[] ElseBlockArray
        {
            get => [.. ElseBlock];
            set
            {
                ElseBlock ??= [];
                ElseBlock.Clear();
                if (value != null)
                {
                    foreach (var action in value)
                    {
                        ElseBlock.Add(action);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the child actions from the IfBlock (actual data store for adding/removing).
        /// </summary>
        /// <returns>The collection of child actions in the IfBlock.</returns>
        public ObservableCollection<IAction> GetChildActions()
        {
            return IfBlock;
        }

        /// <summary>
        /// Called when ShowElseBlock property changes to update the ChildActions collection.
        /// </summary>
        partial void OnShowElseBlockChanged(bool oldValue, bool newValue)
        {
            RefreshChildActions();
        }

        /// <summary>
        /// Rebuilds the ChildActions collection to include only actions from IfBlock.
        /// The Else Block pseudo-node is managed as a sibling at the TestFlow.Actions level.
        /// The IfAction itself represents the If Block - its children are only the IfBlock actions.
        /// </summary>
        private void RefreshChildActions()
        {
            // Ensure ChildActions is initialized (may be null after deserialization)
            if (ChildActions == null)
            {
                ChildActions = new ObservableCollection<IAction>();
            }

            ChildActions.Clear();

            // Add all actions from IfBlock directly (IfAction represents the If Block)
            foreach (var action in IfBlock)
            {
                ChildActions.Add(action);
            }

            // NOTE: Else Block is NOT added here anymore - it's managed as a sibling
            // at the TestFlow.Actions level by ObservableFlow when ShowElseBlock changes
        }

        /// <summary>
        /// Initializes the ChildActions collection after deserialization.
        /// Must be called after loading an IfAction from file to populate the display collection.
        /// </summary>
        public void InitializeAfterDeserialization()
        {
            // Re-initialize child actions to ensure ChildActions is populated
            RefreshChildActions();

            // Reconnect the stored collection change handler in case it was lost.
            EnsureIfBlockHandlerAttached();
        }


        public void Cancel()
        {
            // Composite cancellation is coordinated by child actions via the shared flow token.
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            ObservableCollection<IAction> actionsToExecute;
            IfResumeBranch activeBranch;
            int startIndex;

            if (State == ActionState.Paused && _resumeBranch.HasValue)
            {
                activeBranch = _resumeBranch.Value;
                actionsToExecute = activeBranch == IfResumeBranch.If ? IfBlock : ElseBlock;
                startIndex = Math.Clamp(_resumeIndex, 0, actionsToExecute.Count);
            }
            else
            {
                ClearResumePoint();

                // Evaluate condition without replacing it
                bool conditionResult;

                if (Condition is ExpressionBase<bool> boolExp)
                {
                    // Some expressions, such as UI element existence checks, perform blocking UIA work.
                    // Evaluate them off the UI thread so the shell remains responsive to pause requests.
                    conditionResult = await Task.Run(() =>
                    {
                        boolExp.Environment = Environment;
                        boolExp.Evaluate();
                        return boolExp.Result;
                    }, cancellationToken);

                    // IMPORTANT: Don't replace Condition - keep expression for re-evaluation
                }
                else if (Condition is bool boolValue)
                {
                    conditionResult = boolValue;
                }
                else
                {
                    State = ActionState.Failed;
                    throw new InvalidOperationException("Condition must be an Expression<bool> or a boolean value.");
                }

                activeBranch = conditionResult ? IfResumeBranch.If : IfResumeBranch.Else;
                actionsToExecute = conditionResult ? IfBlock : ElseBlock;
                startIndex = 0;
            }

            for (int index = startIndex; index < actionsToExecute.Count; index++)
            {
                var action = actionsToExecute[index];
                if (action.IsEnabled)
                {
                    var rst = await action.ExecuteAsync(cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        SetResumePoint(activeBranch, rst ? index + 1 : index);
                        State = ActionState.Paused;
                        return false;
                    }

                    if (!rst)
                    {
                        ClearResumePoint();
                        State = ActionState.Failed;
                        return false;
                    }

                    await Task.Yield();
                }
            }

            ClearResumePoint();
            State = ActionState.Completed;
            return true;
        }

        private void SetResumePoint(IfResumeBranch branch, int nextIndex)
        {
            _resumeBranch = branch;
            _resumeIndex = nextIndex;
        }

        private void ClearResumePoint()
        {
            _resumeBranch = null;
            _resumeIndex = 0;
        }

    }
}
