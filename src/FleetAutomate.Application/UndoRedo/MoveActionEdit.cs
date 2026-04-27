using FleetAutomate.Model;
using FleetAutomate.ViewModel;

namespace FleetAutomate.UndoRedo;

public sealed class MoveActionEdit : IUndoableFlowEdit
{
    private readonly ActionCollectionRef _source;
    private readonly int _oldIndex;
    private readonly ActionCollectionRef _target;
    private readonly int _newIndex;

    public MoveActionEdit(ActionCollectionRef source, int oldIndex, ActionCollectionRef target, int newIndex, string displayName = "Move action")
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _oldIndex = oldIndex;
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _newIndex = newIndex;
        DisplayName = displayName;
    }

    public string DisplayName { get; }

    public void Apply(ObservableFlow flow) => Move(flow, _source, _oldIndex, _target, _newIndex);

    public void Unapply(ObservableFlow flow) => Move(flow, _target, _newIndex, _source, _oldIndex);

    private static void Move(ObservableFlow flow, ActionCollectionRef sourceRef, int sourceIndex, ActionCollectionRef targetRef, int targetIndex)
    {
        var source = sourceRef.Resolve(flow);
        var target = targetRef.Resolve(flow);
        var action = source[sourceIndex];

        if (ReferenceEquals(source, target))
        {
            source.Move(sourceIndex, targetIndex);
            return;
        }

        source.RemoveAt(sourceIndex);
        target.Insert(Math.Clamp(targetIndex, 0, target.Count), action);
    }
}
