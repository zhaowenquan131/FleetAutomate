using FleetAutomate.ViewModel;

namespace FleetAutomate.UndoRedo;

public sealed class BatchFlowEdit : IUndoableFlowEdit
{
    private readonly IReadOnlyList<IUndoableFlowEdit> _edits;

    public BatchFlowEdit(IEnumerable<IUndoableFlowEdit> edits, string displayName = "Batch edit")
    {
        _edits = edits?.ToList() ?? throw new ArgumentNullException(nameof(edits));
        DisplayName = displayName;
    }

    public string DisplayName { get; }

    public void Apply(ObservableFlow flow)
    {
        foreach (var edit in _edits)
        {
            edit.Apply(flow);
        }
    }

    public void Unapply(ObservableFlow flow)
    {
        for (var i = _edits.Count - 1; i >= 0; i--)
        {
            _edits[i].Unapply(flow);
        }
    }
}
