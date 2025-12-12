using System.Collections.ObjectModel;

namespace FleetAutomate.Model
{
    /// <summary>
    /// Interface for actions that can contain child actions (composite/container actions).
    /// </summary>
    public interface ICompositeAction : IAction
    {
        /// <summary>
        /// Gets the collection of child actions for this composite action.
        /// </summary>
        ObservableCollection<IAction> GetChildActions();
    }
}
