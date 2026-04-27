using FleetAutomate.ViewModel;

namespace FleetAutomate.UndoRedo;

public sealed class FlowUndoRedoService
{
    public const int DefaultHistoryLimit = 100;

    private readonly ObservableFlow _flow;
    private readonly int _historyLimit;
    private readonly List<IUndoableFlowEdit> _undoStack = [];
    private readonly List<IUndoableFlowEdit> _redoStack = [];
    private long _version;
    private long _savedVersion;

    public FlowUndoRedoService(ObservableFlow flow, int historyLimit = DefaultHistoryLimit)
    {
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        _historyLimit = historyLimit > 0 ? historyLimit : throw new ArgumentOutOfRangeException(nameof(historyLimit));
    }

    public event EventHandler? StateChanged;

    public bool IsApplying { get; private set; }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public string UndoDisplayName => CanUndo ? _undoStack[^1].DisplayName : string.Empty;

    public string RedoDisplayName => CanRedo ? _redoStack[^1].DisplayName : string.Empty;

    public bool IsAtSavedCheckpoint => _version == _savedVersion;

    public void Execute(IUndoableFlowEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        ApplyCore(() => edit.Apply(_flow));
        _undoStack.Add(edit);
        if (_undoStack.Count > _historyLimit)
        {
            _undoStack.RemoveAt(0);
        }

        _redoStack.Clear();
        _version++;
        NotifyChanged();
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        var edit = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        ApplyCore(() => edit.Unapply(_flow));
        _redoStack.Add(edit);
        _version--;
        NotifyChanged();
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        var edit = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);

        ApplyCore(() => edit.Apply(_flow));
        _undoStack.Add(edit);
        _version++;
        NotifyChanged();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _version = 0;
        _savedVersion = 0;
        NotifyChanged();
    }

    public void MarkSavedCheckpoint()
    {
        _savedVersion = _version;
        NotifyChanged();
    }

    private void ApplyCore(Action action)
    {
        IsApplying = true;
        try
        {
            action();
            _flow.SyncStructureToModelPreservingDirty();
        }
        finally
        {
            IsApplying = false;
        }
    }

    private void NotifyChanged()
    {
        _flow.RefreshUndoRedoState();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
