using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;
using CommunityToolkit.Mvvm.ComponentModel;

using FleetAutomate.Model;
using FleetAutomate.Model.Flow;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace FleetAutomate.ViewModel
{
    public partial class ObservableFlow : ObservableObject
    {
        private readonly TestFlow _model;
        private readonly int _instanceId; // Track instance for debugging
        private static int _instanceCounter = 0;
        private bool _isRefreshingFromModel = false; // Flag to suppress change tracking during model refresh

        // Maps IfAction -> (pseudo-node, parent-collection)
        private readonly Dictionary<IfAction, (ActionBlock pseudoNode, ObservableCollection<IAction> parentCollection)> _elseBlockNodes = new();
        // Tracks which collections we've already set up to avoid duplicate event subscriptions
        private readonly HashSet<ObservableCollection<IAction>> _setupCollections = new();
        // Tracks which IfActions we've already subscribed to (to avoid duplicate PropertyChanged subscriptions)
        private readonly HashSet<IfAction> _subscribedIfActions = new();
        // Tracks which actions we've subscribed to for general property change tracking
        private readonly HashSet<IAction> _subscribedActions = new();
        // Maps collection -> event handler so we can unregister handlers when clearing
        private readonly Dictionary<ObservableCollection<IAction>, NotifyCollectionChangedEventHandler> _collectionHandlers = new();
        // Re-entrancy guard: tracks which IfActions are currently being processed for ShowElseBlock changes
        private readonly HashSet<IfAction> _processingShowElseBlock = new();

        public ObservableFlow(TestFlow model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _instanceId = ++_instanceCounter;
            Debug.WriteLine($"[ELSE_BLOCK] ***** ObservableFlow CONSTRUCTOR: instanceId={_instanceId} *****");
            Actions = [];

            // Subscribe to Actions collection changes BEFORE RefreshFromModel
            // so we catch all the IfAction subscriptions during initial load
            Actions.CollectionChanged += Actions_CollectionChanged;

            RefreshFromModel();
        }

        [ObservableProperty]
        private IAction? _currentAction;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private ActionState _state = ActionState.Ready;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private bool _hasUnsavedChanges = false;

        public ObservableCollection<IAction> Actions { get; }

        /// <summary>
        /// Gets the underlying model for serialization and business logic.
        /// </summary>
        public TestFlow Model => _model;

        /// <summary>
        /// Handles collection changes in the Actions collection.
        /// Subscribes to IfAction property changes when added, and unsubscribes when removed.
        /// ALSO syncs the collection changes to the underlying model.
        /// </summary>
        private void Actions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Debug.WriteLine($"[ELSE_BLOCK] Actions_CollectionChanged: Action={e.Action}");

            // Mark as having unsaved changes when actions are added or removed (but not during model refresh)
            if (!_isRefreshingFromModel && (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Remove))
            {
                HasUnsavedChanges = true;
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    // Sync ALL non-pseudo-node actions to the model
                    // This ensures actions added via Actions.Add() are also synced
                    if (item is IAction action && action is not ActionBlock)
                    {
                        // Use IndexOf to check if already in model (safer than Contains for complex objects)
                        int modelIndex = -1;
                        for (int i = 0; i < _model.Actions.Count; i++)
                        {
                            if (ReferenceEquals(_model.Actions[i], action))
                            {
                                modelIndex = i;
                                break;
                            }
                        }

                        if (modelIndex == -1)
                        {
                            // Not in model, add it
                            _model.Actions.Add(action);
                            Debug.WriteLine($"  → Synced action to model: {action.Name}");
                        }

                        // Subscribe to general property changes on this action
                        SubscribeToActionPropertyChanges(action);
                    }

                    if (item is IfAction ifAction)
                    {
                        int ifActionId = ifAction.GetHashCode();
                        Debug.WriteLine($"  → New IfAction: {ifAction.Name}, ifActionId={ifActionId}");

                        // Subscribe to property changes on the IfAction (avoid duplicate subscriptions)
                        if (!_subscribedIfActions.Contains(ifAction))
                        {
                            _subscribedIfActions.Add(ifAction);
                            Debug.WriteLine($"    → Subscribed NEW. Total: {_subscribedIfActions.Count}");
                            // CRITICAL: Unsubscribe first to ensure we never have duplicate handlers
                            ((INotifyPropertyChanged)ifAction).PropertyChanged -= IfAction_PropertyChanged;
                            ((INotifyPropertyChanged)ifAction).PropertyChanged += IfAction_PropertyChanged;
                        }
                        else
                        {
                            Debug.WriteLine($"    → Already subscribed, SKIPPING");
                        }

                        // If this IfAction already has ShowElseBlock=true, insert the pseudo-node
                        if (ifAction.ShowElseBlock)
                        {
                            Debug.WriteLine($"    → ShowElseBlock=true, calling InsertElseBlockNode");
                            InsertElseBlockNode(ifAction, Actions);
                        }

                        // Also set up sibling management for nested IfActions in this IfAction's collections
                        SetupElseBlockManagementForCollection(ifAction.IfBlock, ifAction);
                        SetupElseBlockManagementForCollection(ifAction.ElseBlock, ifAction);
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    // Sync to model: Remove non-pseudo-node actions from the model
                    if (item is IAction action && action is not ActionBlock)
                    {
                        _model.Actions.Remove(action);
                        Debug.WriteLine($"  → Removed action from model: {action.Name}");

                        // Unsubscribe from general property changes
                        UnsubscribeFromActionPropertyChanges(action);
                    }

                    if (item is IfAction ifAction)
                    {
                        Debug.WriteLine($"  → Removed IfAction: {ifAction.Name}");

                        // Unsubscribe from property changes
                        if (_subscribedIfActions.Contains(ifAction))
                        {
                            _subscribedIfActions.Remove(ifAction);
                            Debug.WriteLine($"    → Unsubscribed. Total: {_subscribedIfActions.Count}");
                            ((INotifyPropertyChanged)ifAction).PropertyChanged -= IfAction_PropertyChanged;
                        }

                        // Remove the associated Else Block pseudo-node if it exists
                        RemoveElseBlockNode(ifAction);
                    }
                }
            }
        }

        /// <summary>
        /// Sets up Else Block management for a collection of actions within a parent IfAction.
        /// This enables nested IfActions to have their own Else Block siblings.
        /// </summary>
        private void SetupElseBlockManagementForCollection(ObservableCollection<IAction> collection, IfAction parentIfAction)
        {
            int collectionId = collection.GetHashCode();
            Debug.WriteLine($"[ELSE_BLOCK] SetupElseBlockManagementForCollection (instance={_instanceId}): parent={parentIfAction.Name}, collectionId={collectionId}");

            // Avoid subscribing to the same collection multiple times
            if (_setupCollections.Contains(collection))
            {
                Debug.WriteLine($"  → Collection already set up (id={collectionId}), RETURNING");
                return;
            }

            _setupCollections.Add(collection);
            Debug.WriteLine($"  → Added to _setupCollections (id={collectionId}). Count: {_setupCollections.Count}");

            // IMPORTANT: Unregister any EXISTING handler before registering a new one!
            // This prevents duplicate handlers when the same collection is set up multiple times
            if (_collectionHandlers.TryGetValue(collection, out var oldHandler))
            {
                Debug.WriteLine($"  → Found OLD handler for collection (id={collectionId}), unregistering it first");
                collection.CollectionChanged -= oldHandler;
            }

            // Create and store the event handler so we can unregister it later
            NotifyCollectionChangedEventHandler handler = (s, e) => HandleNestedCollectionChanged(collection, e);
            _collectionHandlers[collection] = handler;
            collection.CollectionChanged += handler;
            Debug.WriteLine($"  → Registered NEW CollectionChanged handler for collection id={collectionId}");

            // Subscribe to existing actions in the collection (all types)
            foreach (var action in collection)
            {
                if (action is not ActionBlock)
                {
                    SubscribeToActionPropertyChanges(action);
                }
            }

            // Subscribe to existing IfActions in the collection
            foreach (var action in collection.OfType<IfAction>())
            {
                int actionId = action.GetHashCode();
                Debug.WriteLine($"  → Found existing IfAction in collection: {action.Name}, ifActionId={actionId}");

                // Avoid duplicate subscriptions
                if (!_subscribedIfActions.Contains(action))
                {
                    _subscribedIfActions.Add(action);
                    Debug.WriteLine($"    → Subscribed NEW to PropertyChanged. Total subscribed: {_subscribedIfActions.Count}");
                    // CRITICAL: Unsubscribe first to ensure we never have duplicate handlers
                    ((INotifyPropertyChanged)action).PropertyChanged -= IfAction_PropertyChanged;
                    ((INotifyPropertyChanged)action).PropertyChanged += IfAction_PropertyChanged;
                }
                else
                {
                    Debug.WriteLine($"    → Already subscribed, SKIPPING");
                }

                if (action.ShowElseBlock)
                {
                    Debug.WriteLine($"    → ShowElseBlock=true, calling InsertElseBlockNode");
                    InsertElseBlockNode(action, collection);
                }

                // Recursively set up management for nested collections
                SetupElseBlockManagementForCollection(action.IfBlock, action);
                SetupElseBlockManagementForCollection(action.ElseBlock, action);
            }
        }

        /// <summary>
        /// Handles collection changes in nested action collections (IfBlock, ElseBlock, Body, etc.).
        /// </summary>
        private void HandleNestedCollectionChanged(ObservableCollection<IAction> collection, NotifyCollectionChangedEventArgs e)
        {
            int collectionId = collection.GetHashCode();
            Debug.WriteLine($"[ELSE_BLOCK] HandleNestedCollectionChanged: Action={e.Action}, collectionId={collectionId}, Collection has {collection.Count} items");

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    // Subscribe to all actions in nested collections
                    if (item is IAction action && action is not ActionBlock)
                    {
                        SubscribeToActionPropertyChanges(action);
                    }

                    if (item is IfAction ifAction)
                    {
                        int ifActionId = ifAction.GetHashCode();
                        Debug.WriteLine($"  → New IfAction (nested): {ifAction.Name}, ifActionId={ifActionId}");

                        // Subscribe to property changes on the IfAction (avoid duplicate subscriptions)
                        if (!_subscribedIfActions.Contains(ifAction))
                        {
                            _subscribedIfActions.Add(ifAction);
                            Debug.WriteLine($"    → Subscribed NEW. Total: {_subscribedIfActions.Count}");
                            // CRITICAL: Unsubscribe first to ensure we never have duplicate handlers
                            ((INotifyPropertyChanged)ifAction).PropertyChanged -= IfAction_PropertyChanged;
                            ((INotifyPropertyChanged)ifAction).PropertyChanged += IfAction_PropertyChanged;
                        }
                        else
                        {
                            Debug.WriteLine($"    → Already subscribed, SKIPPING");
                        }

                        // If this IfAction already has ShowElseBlock=true, insert the pseudo-node into THIS collection
                        if (ifAction.ShowElseBlock)
                        {
                            Debug.WriteLine($"    → ShowElseBlock=true (nested), calling InsertElseBlockNode");
                            InsertElseBlockNode(ifAction, collection);
                        }

                        // Recursively set up management for nested collections
                        Debug.WriteLine($"    → Setting up nested collections for this IfAction");
                        SetupElseBlockManagementForCollection(ifAction.IfBlock, ifAction);
                        SetupElseBlockManagementForCollection(ifAction.ElseBlock, ifAction);
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    // Unsubscribe from all actions in nested collections
                    if (item is IAction action && action is not ActionBlock)
                    {
                        UnsubscribeFromActionPropertyChanges(action);
                    }

                    if (item is IfAction ifAction)
                    {
                        int ifActionId = ifAction.GetHashCode();
                        Debug.WriteLine($"  → Removed IfAction (nested): {ifAction.Name}, ifActionId={ifActionId}");

                        // Unsubscribe from property changes
                        if (_subscribedIfActions.Contains(ifAction))
                        {
                            _subscribedIfActions.Remove(ifAction);
                            Debug.WriteLine($"    → Unsubscribed. Total: {_subscribedIfActions.Count}");
                            ((INotifyPropertyChanged)ifAction).PropertyChanged -= IfAction_PropertyChanged;
                        }

                        // Remove the associated Else Block pseudo-node
                        RemoveElseBlockNode(ifAction);
                    }
                }
            }
        }

        /// <summary>
        /// Handles property changes on IfAction objects.
        /// When ShowElseBlock property changes, manages the Else Block pseudo-node.
        /// </summary>
        private void IfAction_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not IfAction ifAction)
                return;

            if (e.PropertyName == nameof(IfAction.ShowElseBlock))
            {
                int ifActionId = ifAction.GetHashCode();

                // RE-ENTRANCY GUARD: Prevent processing the same IfAction twice if PropertyChanged fires multiple times
                if (_processingShowElseBlock.Contains(ifAction))
                {
                    Debug.WriteLine($"[ELSE_BLOCK] IfAction.PropertyChanged: DUPLICATE CALL (re-entrancy), ifActionId={ifActionId}, SKIPPING");
                    return;
                }

                _processingShowElseBlock.Add(ifAction);
                Debug.WriteLine($"[ELSE_BLOCK] IfAction.PropertyChanged (instance={_instanceId}): ShowElseBlock={ifAction.ShowElseBlock}, IfAction Name={ifAction.Name}, ifActionId={ifActionId}");

                try
                {
                    if (ifAction.ShowElseBlock)
                    {
                        // Find which collection contains this IfAction
                        ObservableCollection<IAction>? parentCol = null;

                        if (_elseBlockNodes.TryGetValue(ifAction, out var nodeInfo))
                        {
                            // If already tracked, use the stored parent collection
                            parentCol = nodeInfo.parentCollection;
                            Debug.WriteLine($"  → Already tracked in _elseBlockNodes");
                        }
                        else
                        {
                            // Find the parent collection by searching all collections
                            parentCol = FindParentCollectionForAction(ifAction);
                            Debug.WriteLine($"  → Found parent collection: {(parentCol != null ? "YES" : "NO")}");
                        }

                        if (parentCol != null)
                        {
                            Debug.WriteLine($"  → Calling InsertElseBlockNode");
                            InsertElseBlockNode(ifAction, parentCol);
                        }
                    }
                    else
                    {
                        // Remove the Else Block pseudo-node
                        Debug.WriteLine($"  → Calling RemoveElseBlockNode");
                        RemoveElseBlockNode(ifAction);
                    }
                }
                finally
                {
                    // Always remove from processing set when done
                    _processingShowElseBlock.Remove(ifAction);
                }
            }
        }

        /// <summary>
        /// Subscribes to PropertyChanged events on an action to track modifications.
        /// </summary>
        private void SubscribeToActionPropertyChanges(IAction action)
        {
            if (action is INotifyPropertyChanged notifyAction)
            {
                if (!_subscribedActions.Contains(action))
                {
                    _subscribedActions.Add(action);
                    notifyAction.PropertyChanged -= Action_PropertyChanged; // Ensure no duplicates
                    notifyAction.PropertyChanged += Action_PropertyChanged;
                }
            }
        }

        /// <summary>
        /// Unsubscribes from PropertyChanged events on an action.
        /// </summary>
        private void UnsubscribeFromActionPropertyChanges(IAction action)
        {
            if (action is INotifyPropertyChanged notifyAction)
            {
                if (_subscribedActions.Contains(action))
                {
                    _subscribedActions.Remove(action);
                    notifyAction.PropertyChanged -= Action_PropertyChanged;
                }
            }
        }

        /// <summary>
        /// Handles property changes on any action to mark the flow as having unsaved changes.
        /// </summary>
        private void Action_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Don't mark as changed during model refresh
            if (!_isRefreshingFromModel)
            {
                HasUnsavedChanges = true;
            }
        }

        /// <summary>
        /// Finds the parent collection that contains the given IfAction by searching all collections.
        /// </summary>
        private ObservableCollection<IAction>? FindParentCollectionForAction(IfAction targetAction)
        {
            // Check top-level Actions
            if (Actions.Contains(targetAction))
                return Actions;

            // Check all IfActions' IfBlock and ElseBlock
            foreach (var action in Actions.OfType<IfAction>())
            {
                var col = FindInNestedCollections(action, targetAction);
                if (col != null)
                    return col;
            }

            return null;
        }

        /// <summary>
        /// Recursively searches nested collections for the target action.
        /// </summary>
        private ObservableCollection<IAction>? FindInNestedCollections(IfAction parentIfAction, IfAction targetAction)
        {
            // Check this IfAction's collections
            if (parentIfAction.IfBlock.Contains(targetAction))
                return parentIfAction.IfBlock;
            if (parentIfAction.ElseBlock.Contains(targetAction))
                return parentIfAction.ElseBlock;

            // Recursively check nested IfActions
            foreach (var nestedAction in parentIfAction.IfBlock.OfType<IfAction>())
            {
                var col = FindInNestedCollections(nestedAction, targetAction);
                if (col != null)
                    return col;
            }

            foreach (var nestedAction in parentIfAction.ElseBlock.OfType<IfAction>())
            {
                var col = FindInNestedCollections(nestedAction, targetAction);
                if (col != null)
                    return col;
            }

            return null;
        }

        /// <summary>
        /// Inserts an Else Block pseudo-node as a sibling after the given IfAction.
        /// The pseudo-node is inserted into the same collection that contains the IfAction.
        /// </summary>
        private void InsertElseBlockNode(IfAction ifAction, ObservableCollection<IAction> parentCollection)
        {
            int parentCollectionId = parentCollection.GetHashCode();
            Debug.WriteLine($"[ELSE_BLOCK] InsertElseBlockNode: IfAction Name={ifAction.Name}, parentCollectionId={parentCollectionId}");

            if (parentCollection == null)
            {
                Debug.WriteLine($"  → parentCollection is null, RETURNING");
                return;
            }

            // Check if we already have an Else Block node for this IfAction
            if (_elseBlockNodes.ContainsKey(ifAction))
            {
                Debug.WriteLine($"  → Already has Else Block in _elseBlockNodes, RETURNING");
                return;
            }

            // Find the IfAction in its parent collection
            int ifActionIndex = parentCollection.IndexOf(ifAction);
            Debug.WriteLine($"  → IfAction index in parent collection: {ifActionIndex}");

            if (ifActionIndex == -1)
            {
                Debug.WriteLine($"  → IfAction NOT found in parent collection, RETURNING");
                return;
            }

            // Create the Else Block pseudo-node
            var elseBlockNode = new ActionBlock
            {
                Name = "Else Block",
                Description = "Actions executed when the condition is false",
                ParentIfAction = ifAction,
                ManagedCollection = ifAction.ElseBlock
            };

            // Insert right after the IfAction in the SAME collection
            int insertIndex = ifActionIndex + 1;
            Debug.WriteLine($"  → Inserting Else Block at index {insertIndex} in collection id={parentCollectionId}");
            parentCollection.Insert(insertIndex, elseBlockNode);

            // Store the reference with parent collection info
            _elseBlockNodes[ifAction] = (elseBlockNode, parentCollection);
            Debug.WriteLine($"  → Stored in _elseBlockNodes. Current count: {_elseBlockNodes.Count}");
        }

        /// <summary>
        /// Removes the Else Block pseudo-node associated with the given IfAction.
        /// </summary>
        private void RemoveElseBlockNode(IfAction ifAction)
        {
            if (_elseBlockNodes.TryGetValue(ifAction, out var nodeInfo))
            {
                nodeInfo.parentCollection.Remove(nodeInfo.pseudoNode);
                _elseBlockNodes.Remove(ifAction);
            }
        }

        /// <summary>
        /// Gets the environment for logic actions.
        /// </summary>
        public FleetAutomate.Model.Actions.Logic.Environment Environment => _model.Environment;

        /// <summary>
        /// Refreshes all properties from the model.
        /// </summary>
        public void RefreshFromModel()
        {
            _isRefreshingFromModel = true;
            Debug.WriteLine($"[ELSE_BLOCK] ***** RefreshFromModel START: instanceId={_instanceId} *****");
            CurrentAction = _model.CurrentAction;
            Name = _model.Name ?? string.Empty;
            Description = _model.Description ?? string.Empty;
            FileName = _model.FileName ?? string.Empty;
            State = _model.State;
            IsEnabled = _model.IsEnabled;

            // Clear existing subscriptions and pseudo-nodes
            foreach (var ifAction in _subscribedIfActions.ToList())
            {
                ((INotifyPropertyChanged)ifAction).PropertyChanged -= IfAction_PropertyChanged;
            }
            _subscribedIfActions.Clear();

            // Unsubscribe from all action property changes
            foreach (var action in _subscribedActions.ToList())
            {
                if (action is INotifyPropertyChanged notifyAction)
                {
                    notifyAction.PropertyChanged -= Action_PropertyChanged;
                }
            }
            _subscribedActions.Clear();

            _elseBlockNodes.Clear();

            // Unregister all collection handlers before clearing _setupCollections
            Debug.WriteLine($"[ELSE_BLOCK] RefreshFromModel (instance={_instanceId}): Unregistering {_setupCollections.Count} collection handlers");
            int unregisteredCount = 0;
            foreach (var collection in _setupCollections.ToList())
            {
                int collectionId = collection.GetHashCode();
                if (_collectionHandlers.TryGetValue(collection, out var handler))
                {
                    Debug.WriteLine($"  → Unregistering handler for collection id={collectionId}");
                    collection.CollectionChanged -= handler;
                    _collectionHandlers.Remove(collection);
                    unregisteredCount++;
                }
                else
                {
                    Debug.WriteLine($"  → WARNING: Collection id={collectionId} in _setupCollections but NOT in _collectionHandlers!");
                }
            }
            Debug.WriteLine($"[ELSE_BLOCK] RefreshFromModel: Unregistered {unregisteredCount} handlers");
            _setupCollections.Clear();

            // Clear Actions - this triggers CollectionChanged handler which will unsubscribe IfActions
            Actions.Clear();

            // Add all actions from the model
            // The CollectionChanged handler will:
            // - Subscribe to new IfActions' PropertyChanged
            // - Insert Else Block pseudo-nodes if ShowElseBlock=true
            // - Set up nested collection management
            foreach (var action in _model.Actions)
            {
                Actions.Add(action);
            }

            _isRefreshingFromModel = false;
            // Reset HasUnsavedChanges after refresh since we just loaded from model
            HasUnsavedChanges = false;
        }

        /// <summary>
        /// Synchronizes changes back to the model.
        /// Filters out Else Block pseudo-nodes so only real actions are saved.
        /// Resets the HasUnsavedChanges flag after syncing.
        /// </summary>
        public void SyncToModel()
        {
            _model.CurrentAction = CurrentAction;
            _model.Name = Name;
            _model.Description = Description;
            _model.FileName = FileName;
            _model.State = State;
            _model.IsEnabled = IsEnabled;

            _model.Actions.Clear();
            foreach (var action in Actions)
            {
                // Skip Else Block pseudo-nodes - they're not real actions
                if (action is not ActionBlock)
                {
                    _model.Actions.Add(action);
                }
            }

            // Reset unsaved changes flag after syncing to model
            HasUnsavedChanges = false;
        }

        /// <summary>
        /// Adds an action to the flow.
        /// </summary>
        /// <param name="action">The action to add.</param>
        public void AddAction(IAction action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            Actions.Add(action);

            // Subscribe to IfAction property changes
            if (action is IfAction ifAction)
            {
                ((INotifyPropertyChanged)ifAction).PropertyChanged += IfAction_PropertyChanged;
            }

            // Only add non-pseudo-nodes to the model
            if (action is not ActionBlock)
            {
                _model.Actions.Add(action);
            }
        }

        /// <summary>
        /// Removes an action from the flow.
        /// Also removes associated Else Block pseudo-nodes if removing an IfAction.
        /// </summary>
        /// <param name="action">The action to remove.</param>
        /// <returns>True if the action was removed, false otherwise.</returns>
        public bool RemoveAction(IAction action)
        {
            if (Actions.Remove(action))
            {
                // If removing an IfAction, also remove its Else Block pseudo-node
                if (action is IfAction ifAction)
                {
                    RemoveElseBlockNode(ifAction);
                    _model.Actions.Remove(ifAction);
                }
                else if (action is not ActionBlock)
                {
                    // For non-pseudo-node actions, sync to model
                    _model.Actions.Remove(action);
                }
                // Don't remove Else Block pseudo-nodes from model (they're not in it)
                return true;
            }
            return false;
        }

        /// <summary>
        /// Inserts an action at the specified index.
        /// </summary>
        /// <param name="index">The index to insert at.</param>
        /// <param name="action">The action to insert.</param>
        public void InsertAction(int index, IAction action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            Actions.Insert(index, action);

            // Subscribe to IfAction property changes
            if (action is IfAction ifAction)
            {
                ((INotifyPropertyChanged)ifAction).PropertyChanged += IfAction_PropertyChanged;
            }

            // Only add non-pseudo-nodes to the model
            if (action is not ActionBlock)
            {
                _model.Actions.Insert(index, action);
            }
        }

        /// <summary>
        /// Moves an action from one index to another.
        /// </summary>
        /// <param name="oldIndex">The current index of the action.</param>
        /// <param name="newIndex">The new index for the action.</param>
        public void MoveAction(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= Actions.Count)
                throw new ArgumentOutOfRangeException(nameof(oldIndex));
            if (newIndex < 0 || newIndex >= Actions.Count)
                throw new ArgumentOutOfRangeException(nameof(newIndex));

            var action = Actions[oldIndex];
            Actions.RemoveAt(oldIndex);
            Actions.Insert(newIndex, action);

            // Only move non-pseudo-nodes in the model
            if (action is not ActionBlock)
            {
                _model.Actions.RemoveAt(oldIndex);
                _model.Actions.Insert(newIndex, action);
            }
        }

        /// <summary>
        /// Clears all actions from the flow.
        /// </summary>
        public void ClearActions()
        {
            // Unsubscribe from all IfAction property changes
            foreach (var ifAction in _subscribedIfActions.ToList())
            {
                ((INotifyPropertyChanged)ifAction).PropertyChanged -= IfAction_PropertyChanged;
            }
            _subscribedIfActions.Clear();

            // Unsubscribe from all action property changes
            foreach (var action in _subscribedActions.ToList())
            {
                if (action is INotifyPropertyChanged notifyAction)
                {
                    notifyAction.PropertyChanged -= Action_PropertyChanged;
                }
            }
            _subscribedActions.Clear();

            _elseBlockNodes.Clear();

            // Unregister all collection handlers before clearing _setupCollections
            foreach (var collection in _setupCollections.ToList())
            {
                if (_collectionHandlers.TryGetValue(collection, out var handler))
                {
                    collection.CollectionChanged -= handler;
                    _collectionHandlers.Remove(collection);
                }
            }
            _setupCollections.Clear();

            Actions.Clear();
            _model.Actions.Clear();
        }

        /// <summary>
        /// Cancels the execution of the flow.
        /// </summary>
        public void Cancel()
        {
            _model.Cancel();
        }

        /// <summary>
        /// Executes the flow asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if execution was successful, false otherwise.</returns>
        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            var result = await _model.ExecuteAsync(cancellationToken);
            RefreshFromModel(); // Refresh to get updated state
            return result;
        }

        /// <summary>
        /// Validates the entire Flow and returns all syntax errors found.
        /// </summary>
        /// <param name="options">Validation options. If null, default options are used.</param>
        /// <returns>A collection of syntax errors found in the Flow.</returns>
        public IEnumerable<SyntaxError> ValidateSyntax(SyntaxValidationOptions? options = null)
        {
            return _model.ValidateSyntax(options);
        }

        /// <summary>
        /// Checks if the Flow has any syntax errors.
        /// </summary>
        /// <param name="options">Validation options. If null, default options are used.</param>
        /// <returns>True if the Flow has syntax errors, false otherwise.</returns>
        public bool HasSyntaxErrors(SyntaxValidationOptions? options = null)
        {
            return _model.HasSyntaxErrors(options);
        }

        /// <summary>
        /// Gets a summary of syntax validation results.
        /// </summary>
        /// <param name="options">Validation options. If null, default options are used.</param>
        /// <returns>A summary object containing error counts and details.</returns>
        public FlowValidationSummary GetValidationSummary(SyntaxValidationOptions? options = null)
        {
            return _model.GetValidationSummary(options);
        }

        /// <summary>
        /// Initializes runtime objects after deserialization.
        /// </summary>
        public void InitializeAfterDeserialization()
        {
            _model.InitializeAfterDeserialization();
            RefreshFromModel();
        }

        partial void OnCurrentActionChanged(IAction? value)
        {
            if (_model.CurrentAction != value)
            {
                _model.CurrentAction = value;
            }
        }

        partial void OnNameChanged(string value)
        {
            if (_model.Name != value)
            {
                _model.Name = value;
                if (!_isRefreshingFromModel)
                {
                    HasUnsavedChanges = true;
                }
            }
        }

        partial void OnDescriptionChanged(string value)
        {
            if (_model.Description != value)
            {
                _model.Description = value;
                if (!_isRefreshingFromModel)
                {
                    HasUnsavedChanges = true;
                }
            }
        }

        partial void OnFileNameChanged(string value)
        {
            if (_model.FileName != value)
            {
                _model.FileName = value;
            }
        }

        partial void OnStateChanged(ActionState value)
        {
            if (_model.State != value)
            {
                _model.State = value;
            }
        }

        partial void OnIsEnabledChanged(bool value)
        {
            if (_model.IsEnabled != value)
            {
                _model.IsEnabled = value;
                if (!_isRefreshingFromModel)
                {
                    HasUnsavedChanges = true;
                }
            }
        }
    }
}
