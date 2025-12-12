using Canvas.TestRunner.Model.Flow;

using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.Serialization;

namespace Canvas.TestRunner.Model.Actions.Logic
{
    [ObservableObject]
    [DataContract]
    [KnownType(typeof(SetVariableAction<object>))]
    [KnownType(typeof(Loops.WhileLoopAction))]
    [KnownType(typeof(Loops.ForLoopAction))]
    [KnownType(typeof(IfAction))]
    [KnownType(typeof(TestFlow))]
    [KnownType(typeof(System.LaunchApplicationAction))]
    [KnownType(typeof(UIAutomation.WaitForElementAction))]
    [KnownType(typeof(UIAutomation.ClickElementAction))]
    [KnownType(typeof(Expression.UIElementExistsExpression))]
    public partial class IfAction : ILogicAction, ICompositeAction
    {
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

        [DataMember]
        public object Condition { get; set; }

        [DataMember]
        public Environment Environment { get; set; }

        [DataMember]
        public string Name { get; set; } = "If Action";

        [DataMember]
        public string Description { get; set; } = "Execute Actions in one of two given blocks by condition";

        [ObservableProperty]
        [DataMember(Name = "IsEnabled")]
        private bool _isEnabled = true;

        public ObservableCollection<IAction> IfBlock { get; set; } = [];

        public ObservableCollection<IfAction> ElseIfs { get; set; } = [];

        public ObservableCollection<IAction> ElseBlock { get; set; } = [];

        [DataMember]
        public ActionState State { get; set; }

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

            // Reconnect the collection change handler in case it was lost
            IfBlock.CollectionChanged -= (s, e) => RefreshChildActions();
            IfBlock.CollectionChanged += (s, e) => RefreshChildActions();
        }

        public void Cancel()
        {
            throw new NotImplementedException();
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            // Evaluate condition without replacing it
            bool conditionResult;

            if (Condition is ExpressionBase<bool> boolExp)
            {
                boolExp.Evaluate();
                conditionResult = boolExp.Result;
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

            // Execute based on condition result
            if (conditionResult)
            {
                foreach (var action in IfBlock)
                {
                    if (action.IsEnabled)
                    {
                        var rst = await action.ExecuteAsync(cancellationToken);
                        if (cancellationToken.IsCancellationRequested || !rst)
                        {
                            State = ActionState.Failed;
                            return false; // Stop execution if cancellation is requested
                        }
                    }
                }
            }
            else
            {
                foreach (var action in ElseBlock)
                {
                    if (action.IsEnabled)
                    {
                        var rst = await action.ExecuteAsync(cancellationToken);
                        if (cancellationToken.IsCancellationRequested || !rst)
                        {
                            State = ActionState.Failed;
                            return false; // Stop execution if cancellation is requested
                        }
                    }
                }
            }

            State = ActionState.Completed;
            return true;
        }

    }
}
