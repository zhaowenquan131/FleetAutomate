using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Flow;
using FleetAutomate.UndoRedo;
using FleetAutomate.ViewModel;

namespace FleetAutomate.Tests.Flow;

public sealed class FlowUndoRedoTests
{
    [Fact]
    public void FlowPropertyEdit_UndoRedo_StoresOnlyOldAndNewValues()
    {
        var flow = new ObservableFlow(new TestFlow { Name = "before" });
        var snapshots = new ActionSnapshotService();

        flow.UndoRedoService.Execute(new FlowPropertyEdit(flow, nameof(ObservableFlow.Name), "before", "after"));

        Assert.Equal("after", flow.Name);
        Assert.Equal(0, snapshots.SnapshotCount);

        flow.Undo();

        Assert.Equal("before", flow.Name);
        Assert.False(flow.CanUndo);
        Assert.True(flow.CanRedo);
        Assert.Equal(0, snapshots.SnapshotCount);

        flow.Redo();

        Assert.Equal("after", flow.Name);
        Assert.True(flow.CanUndo);
        Assert.False(flow.CanRedo);
        Assert.Equal(0, snapshots.SnapshotCount);
    }

    [Fact]
    public void MoveActionEdit_UndoRedo_DoesNotCreateSnapshots()
    {
        var flow = new ObservableFlow(new TestFlow());
        var first = new LogAction { Message = "first" };
        var second = new LaunchApplicationAction { ExecutablePath = "notepad.exe" };
        flow.Actions.Add(first);
        flow.Actions.Add(second);
        flow.MarkSavedCheckpoint();
        var snapshots = new ActionSnapshotService();

        flow.UndoRedoService.Execute(new MoveActionEdit(
            ActionCollectionRef.Root,
            oldIndex: 0,
            ActionCollectionRef.Root,
            newIndex: 1,
            "Move action"));

        Assert.Same(second, flow.Actions[0]);
        Assert.Same(first, flow.Actions[1]);
        Assert.Equal(0, snapshots.SnapshotCount);

        flow.Undo();

        Assert.Same(first, flow.Actions[0]);
        Assert.Same(second, flow.Actions[1]);
        Assert.Equal(0, snapshots.SnapshotCount);

        flow.Redo();

        Assert.Same(second, flow.Actions[0]);
        Assert.Same(first, flow.Actions[1]);
        Assert.Equal(0, snapshots.SnapshotCount);
    }

    [Fact]
    public void AddActionEdit_UndoRedo_ReusesSameActionInstance()
    {
        var flow = new ObservableFlow(new TestFlow());
        var action = new LogAction { Message = "created once" };

        flow.UndoRedoService.Execute(new AddActionEdit(ActionCollectionRef.Root, index: 0, action));

        Assert.Same(action, Assert.Single(flow.Actions));

        flow.Undo();

        Assert.Empty(flow.Actions);

        flow.Redo();

        Assert.Same(action, Assert.Single(flow.Actions));
    }

    [Fact]
    public void RemoveActionEdit_UndoRedo_RestoresSameActionAndNestedPosition()
    {
        var parent = new IfAction();
        var child = new LogAction { Message = "nested" };
        parent.IfBlock.Add(child);
        var flow = new ObservableFlow(new TestFlow());
        flow.Actions.Add(parent);

        var nestedRef = ActionCollectionRef.ForIfBlock([0]);
        flow.UndoRedoService.Execute(new RemoveActionEdit(nestedRef, index: 0, child));

        Assert.Empty(parent.IfBlock);

        flow.Undo();

        Assert.Same(child, Assert.Single(parent.IfBlock));

        flow.Redo();

        Assert.Empty(parent.IfBlock);
    }

    [Fact]
    public void ReplaceActionEdit_UsesSnapshotsAndRestoresTypesAndProperties()
    {
        var flow = new ObservableFlow(new TestFlow());
        var snapshots = new ActionSnapshotService();
        var oldAction = new LogAction { Message = "old", LogLevel = LogLevel.Warn };
        var newAction = new LaunchApplicationAction { ExecutablePath = "calc.exe", Arguments = "/x" };
        flow.Actions.Add(oldAction);

        flow.UndoRedoService.Execute(new ReplaceActionEdit(ActionCollectionRef.Root, index: 0, oldAction, newAction, snapshots));

        var current = Assert.IsType<LaunchApplicationAction>(Assert.Single(flow.Actions));
        Assert.Equal("calc.exe", current.ExecutablePath);
        Assert.Equal("/x", current.Arguments);
        Assert.True(snapshots.SnapshotCount >= 2);

        flow.Undo();

        var restoredOld = Assert.IsType<LogAction>(Assert.Single(flow.Actions));
        Assert.Equal("old", restoredOld.Message);
        Assert.Equal(LogLevel.Warn, restoredOld.LogLevel);

        flow.Redo();

        var restoredNew = Assert.IsType<LaunchApplicationAction>(Assert.Single(flow.Actions));
        Assert.Equal("calc.exe", restoredNew.ExecutablePath);
        Assert.Equal("/x", restoredNew.Arguments);
    }

    [Fact]
    public void FlowUndoRedoService_DropsOldestHistoryAfterLimit()
    {
        var flow = new ObservableFlow(new TestFlow { Name = "0" });

        for (var i = 1; i <= FlowUndoRedoService.DefaultHistoryLimit + 1; i++)
        {
            flow.UndoRedoService.Execute(new FlowPropertyEdit(
                flow,
                nameof(ObservableFlow.Name),
                (i - 1).ToString(),
                i.ToString()));
        }

        for (var i = 0; i < FlowUndoRedoService.DefaultHistoryLimit; i++)
        {
            flow.Undo();
        }

        Assert.Equal("1", flow.Name);
        Assert.False(flow.CanUndo);
    }
}
