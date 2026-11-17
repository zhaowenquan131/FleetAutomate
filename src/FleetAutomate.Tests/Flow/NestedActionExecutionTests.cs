using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;
using FleetAutomate.UndoRedo;
using FleetAutomate.ViewModel;

namespace FleetAutomate.Tests.Flow;

public sealed class NestedActionExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartFromMovedChild_RunsBranchRemainderThenOuterActions(bool elseBranch)
    {
        var calls = new List<string>();
        var model = new TestFlow();
        var flow = new ObservableFlow(model);
        var before = new OrderedAction("before", calls) { State = ActionState.Completed };
        var target = new OrderedAction("target", calls);
        var after = new OrderedAction("after", calls);
        var parent = new IfAction { Condition = elseBranch, ShowElseBlock = true };
        var branch = elseBranch ? parent.ElseBlock : parent.IfBlock;
        var other = elseBranch ? parent.IfBlock : parent.ElseBlock;
        var skipped = new OrderedAction("skipped", calls) { State = ActionState.Completed };
        branch.Add(skipped);
        other.Add(new OrderedAction("other branch", calls));
        flow.Actions.Add(before);
        flow.Actions.Add(target);
        flow.Actions.Add(parent);
        flow.Actions.Add(after);

        // Exercise the actual drag edit and wrapper-to-model execution path.
        var block = new ActionBlock { ParentIfAction = parent, ManagedCollection = branch };
        flow.UndoRedoService.Execute(MoveActionsEdit.Create(flow, [target], block, ActionInsertionMode.Into)!);
        branch.Add(new OrderedAction("branch tail", calls));
        flow.SyncToModel();
        var result = await flow.StartFromActionAsync(target, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(["target", "branch tail", "after"], calls);
        Assert.Equal(ActionState.Completed, model.State);
        Assert.Equal(ActionState.Completed, before.State);
        Assert.Equal(ActionState.Completed, skipped.State);
        Assert.Same(target, branch[1]);
        Assert.Equal([before, parent, after], model.Actions);
    }

    [Fact]
    public async Task StartFromDeepChild_ContinuesThroughEachEnclosingBlock()
    {
        var calls = new List<string>();
        var flow = new TestFlow();
        var outer = new IfAction { Condition = false };
        var inner = new IfAction { Condition = true };
        var target = new OrderedAction("target", calls);
        inner.ElseBlock.Add(new OrderedAction("skipped", calls));
        inner.ElseBlock.Add(target);
        inner.ElseBlock.Add(new OrderedAction("inner tail", calls));
        inner.IfBlock.Add(new OrderedAction("other inner branch", calls));
        outer.IfBlock.Add(inner);
        outer.IfBlock.Add(new OrderedAction("outer tail", calls));
        outer.ElseBlock.Add(new OrderedAction("other outer branch", calls));
        flow.Actions.Add(outer);
        flow.Actions.Add(new OrderedAction("root tail", calls));

        Assert.True(await flow.StartFromActionAsync(target, CancellationToken.None));
        Assert.Equal(["target", "inner tail", "outer tail", "root tail"], calls);
    }

    [Fact]
    public async Task StepNestedChild_ContinueStartsAtNextChild()
    {
        var calls = new List<string>();
        var flow = new TestFlow();
        var parent = new IfAction { Condition = false };
        var target = new OrderedAction("target", calls);
        var next = new OrderedAction("next", calls);
        parent.IfBlock.Add(new OrderedAction("skipped", calls));
        parent.IfBlock.Add(target);
        parent.IfBlock.Add(next);
        flow.Actions.Add(parent);
        flow.Actions.Add(new OrderedAction("root tail", calls));

        Assert.True(await flow.StepActionAsync(target, CancellationToken.None));
        Assert.Equal(["target"], calls);
        Assert.Equal(TestFlowBreakReason.StepCompleted, flow.BreakReason);
        Assert.Same(next, flow.CurrentAction);
        Assert.True(await flow.ContinueAsync(CancellationToken.None));
        Assert.Equal(["target", "next", "root tail"], calls);
    }

    [Fact]
    public async Task NestedFailure_SkipContinuesAtNextChild()
    {
        var calls = new List<string>();
        var flow = new TestFlow();
        var parent = new IfAction { Condition = true };
        var target = new OrderedAction("failed", calls) { Result = false };
        parent.IfBlock.Add(target);
        parent.IfBlock.Add(new OrderedAction("next", calls));
        parent.ElseBlock.Add(new OrderedAction("other branch", calls));
        flow.Actions.Add(parent);
        flow.Actions.Add(new OrderedAction("root tail", calls));

        Assert.False(await flow.StartFromActionAsync(target, CancellationToken.None));
        Assert.Same(target, flow.LastFailedAction);
        Assert.Same(target, flow.CurrentAction);
        Assert.Equal(["failed"], calls);
        Assert.True(await flow.SkipFailedActionAndContinueAsync(CancellationToken.None));
        Assert.Equal(["failed", "next", "root tail"], calls);
    }

    [Fact]
    public async Task NestedChild_ReceivesFlowEnvironment()
    {
        var flow = new TestFlow();
        var parent = new IfAction { Condition = false };
        var target = new SetVariableAction<int>("value", 42) { Environment = null! };
        parent.IfBlock.Add(target);
        flow.Actions.Add(parent);

        Assert.True(await flow.StartFromActionAsync(target, CancellationToken.None));
        Assert.Equal(42, flow.GetRuntimeVariableValues()["value"]);
    }

    private sealed class OrderedAction(string name, List<string> calls) : IAction
    {
        public string Name => name;
        public string Description => name;
        public bool IsEnabled => true;
        public ActionState State { get; set; }
        public bool Result { get; init; } = true;
        public Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            calls.Add(name);
            State = Result ? ActionState.Completed : ActionState.Failed;
            return Task.FromResult(Result);
        }
        public void Cancel() { }
    }
}
