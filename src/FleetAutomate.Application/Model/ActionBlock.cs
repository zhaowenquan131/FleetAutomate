using System.Collections.ObjectModel;
using Canvas.TestRunner.Model.Flow;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Model
{
    /// <summary>
    /// Represents a visual action block within a composite action (e.g., "If Block", "Else Block").
    /// This is a pseudo-node that appears in the TreeView but actually manages a specific collection
    /// within a parent composite action.
    /// </summary>
    public class ActionBlock : IAction, ICompositeAction
    {
        /// <summary>
        /// The parent IfAction that owns this block.
        /// </summary>
        public Canvas.TestRunner.Model.Actions.Logic.IfAction ParentIfAction { get; set; }

        /// <summary>
        /// The collection that this block manages.
        /// </summary>
        public ObservableCollection<IAction> ManagedCollection { get; set; }

        /// <summary>
        /// Gets or sets the name of this block (e.g., "If Block", "Else Block").
        /// </summary>
        public string Name { get; set; } = "Action Block";

        /// <summary>
        /// Gets or sets the description of this block.
        /// </summary>
        public string Description { get; set; } = "A block of actions";

        /// <summary>
        /// Gets or sets whether this block is enabled.
        /// </summary>
        public bool IsEnabled => true;

        /// <summary>
        /// Not used for pseudo-nodes, but required by IAction.
        /// </summary>
        public ActionState State { get; set; } = ActionState.Ready;

        /// <summary>
        /// Returns the managed collection as child actions.
        /// This is exposed as a property for TreeView binding.
        /// </summary>
        public ObservableCollection<IAction> ChildActions => ManagedCollection;

        /// <summary>
        /// Returns the managed collection as child actions.
        /// </summary>
        public ObservableCollection<IAction> GetChildActions()
        {
            return ManagedCollection;
        }

        /// <summary>
        /// Not implemented for pseudo-nodes.
        /// </summary>
        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            // Pseudo-nodes don't execute directly
            return true;
        }

        /// <summary>
        /// Not implemented for pseudo-nodes.
        /// </summary>
        public void Cancel()
        {
            // Pseudo-nodes don't cancel
        }
    }
}

