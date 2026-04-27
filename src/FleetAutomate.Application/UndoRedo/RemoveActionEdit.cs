using FleetAutomate.Model;
using FleetAutomate.ViewModel;

namespace FleetAutomate.UndoRedo;

public sealed class RemoveActionEdit : IUndoableFlowEdit
{
    private readonly ActionCollectionRef _source;
    private readonly int _index;
    private readonly IAction _action;

    public RemoveActionEdit(ActionCollectionRef source, int index, IAction action, string displayName = "Remove action")
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _index = index;
        _action = action ?? throw new ArgumentNullException(nameof(action));
        DisplayName = displayName;
    }

    public string DisplayName { get; }

    public void Apply(ObservableFlow flow)
    {
        var collection = _source.Resolve(flow);
        if (_index >= 0 && _index < collection.Count && ReferenceEquals(collection[_index], _action))
        {
            collection.RemoveAt(_index);
            return;
        }

        collection.Remove(_action);
    }

    public void Unapply(ObservableFlow flow)
    {
        var collection = _source.Resolve(flow);
        collection.Insert(Math.Clamp(_index, 0, collection.Count), _action);
    }
}
