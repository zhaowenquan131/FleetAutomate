using FleetAutomate.Model;
using FleetAutomate.ViewModel;

namespace FleetAutomate.UndoRedo;

public sealed class ReplaceActionEdit : IUndoableFlowEdit
{
    private readonly ActionCollectionRef _target;
    private readonly int _index;
    private readonly ActionSnapshotService.ActionSnapshot _oldSnapshot;
    private readonly ActionSnapshotService.ActionSnapshot _newSnapshot;

    public ReplaceActionEdit(
        ActionCollectionRef target,
        int index,
        IAction oldAction,
        IAction newAction,
        ActionSnapshotService snapshots,
        string displayName = "Replace action")
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _index = index;
        ArgumentNullException.ThrowIfNull(snapshots);
        _oldSnapshot = snapshots.Capture(oldAction);
        _newSnapshot = snapshots.Capture(newAction);
        DisplayName = displayName;
    }

    public string DisplayName { get; }

    public void Apply(ObservableFlow flow)
    {
        Replace(flow, _newSnapshot);
    }

    public void Unapply(ObservableFlow flow)
    {
        Replace(flow, _oldSnapshot);
    }

    private void Replace(ObservableFlow flow, ActionSnapshotService.ActionSnapshot snapshot)
    {
        var collection = _target.Resolve(flow);
        collection[_index] = snapshot.Restore();
    }
}
