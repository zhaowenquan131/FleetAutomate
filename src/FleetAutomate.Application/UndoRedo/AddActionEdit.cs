using FleetAutomate.Model;
using FleetAutomate.ViewModel;

namespace FleetAutomate.UndoRedo;

public sealed class AddActionEdit : IUndoableFlowEdit
{
    private readonly ActionCollectionRef _target;
    private readonly int _index;
    private readonly IAction _action;

    public AddActionEdit(ActionCollectionRef target, int index, IAction action, string displayName = "Add action")
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _index = index;
        _action = action ?? throw new ArgumentNullException(nameof(action));
        DisplayName = displayName;
    }

    public string DisplayName { get; }

    public void Apply(ObservableFlow flow)
    {
        var collection = _target.Resolve(flow);
        collection.Insert(Math.Clamp(_index, 0, collection.Count), _action);
    }

    public void Unapply(ObservableFlow flow)
    {
        _target.Resolve(flow).Remove(_action);
    }
}
