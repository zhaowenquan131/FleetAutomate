using FleetAutomate.Model;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Tests.Flow;

public sealed class TestFlowExecutionTests
{
    [Fact]
    public async Task StartAsync_CompletesAllActions()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        var first = new RecordingAction("first");
        var second = new RecordingAction("second");
        flow.Actions.Add(first);
        flow.Actions.Add(second);

        var result = await flow.StartAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(ActionState.Completed, flow.State);
        Assert.Equal(TestFlowBreakReason.Completed, flow.BreakReason);
        Assert.Null(flow.CurrentAction);
        Assert.Equal(1, first.ExecutionCount);
        Assert.Equal(1, second.ExecutionCount);
    }

    [Fact]
    public async Task StepAsync_PausesBeforeNextAction()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        var first = new RecordingAction("first");
        var second = new RecordingAction("second");
        flow.Actions.Add(first);
        flow.Actions.Add(second);

        var result = await flow.StepAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(ActionState.Paused, flow.State);
        Assert.Equal(TestFlowBreakReason.StepCompleted, flow.BreakReason);
        Assert.Same(second, flow.CurrentAction);
        Assert.Equal(1, first.ExecutionCount);
        Assert.Equal(0, second.ExecutionCount);
    }

    [Fact]
    public async Task ContinueAsync_AfterStep_RunsRemainingActions()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        var first = new RecordingAction("first");
        var second = new RecordingAction("second");
        flow.Actions.Add(first);
        flow.Actions.Add(second);

        await flow.StepAsync(CancellationToken.None);
        var result = await flow.ContinueAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(ActionState.Completed, flow.State);
        Assert.Equal(TestFlowBreakReason.Completed, flow.BreakReason);
        Assert.Null(flow.CurrentAction);
        Assert.Equal(1, first.ExecutionCount);
        Assert.Equal(1, second.ExecutionCount);
    }

    [Fact]
    public async Task SkipFailedActionAndContinueAsync_RunsNextAction()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        var first = new RecordingAction("first");
        var failing = new ResultAction("failing", false);
        var third = new RecordingAction("third");
        flow.Actions.Add(first);
        flow.Actions.Add(failing);
        flow.Actions.Add(third);

        var initialResult = await flow.StartAsync(CancellationToken.None);

        Assert.False(initialResult);
        Assert.Equal(ActionState.Failed, flow.State);
        Assert.Same(failing, flow.CurrentAction);
        Assert.Same(failing, flow.LastFailedAction);

        var continueResult = await flow.SkipFailedActionAndContinueAsync(CancellationToken.None);

        Assert.True(continueResult);
        Assert.Equal(ActionState.Completed, flow.State);
        Assert.Equal(TestFlowBreakReason.Completed, flow.BreakReason);
        Assert.Null(flow.CurrentAction);
        Assert.Equal(1, third.ExecutionCount);
    }

    [Fact]
    public async Task Pause_OnAtomicAction_WaitsUntilCurrentActionFinishes()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        var current = new AtomicGateAction("atomic");
        var next = new RecordingAction("next");
        flow.Actions.Add(current);
        flow.Actions.Add(next);

        var runTask = flow.StartAsync(CancellationToken.None);
        await current.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        flow.Pause();
        current.Release();

        var result = await runTask;

        Assert.True(result);
        Assert.Equal(ActionState.Paused, flow.State);
        Assert.Equal(TestFlowBreakReason.PauseRequested, flow.BreakReason);
        Assert.Same(next, flow.CurrentAction);
        Assert.Equal(1, current.ExecutionCount);
        Assert.Equal(0, next.ExecutionCount);
    }

    [Fact]
    public async Task Pause_OnCooperativeAction_InterruptsCurrentAction()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        var current = new CooperativeBlockingAction("wait");
        var next = new RecordingAction("next");
        flow.Actions.Add(current);
        flow.Actions.Add(next);

        var runTask = flow.StartAsync(CancellationToken.None);
        await current.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        flow.Pause();
        var result = await runTask;

        Assert.True(result);
        Assert.Equal(ActionState.Paused, flow.State);
        Assert.Equal(TestFlowBreakReason.PauseRequested, flow.BreakReason);
        Assert.Same(current, flow.CurrentAction);
        Assert.Equal(0, next.ExecutionCount);
    }
}

internal class RecordingAction : IAction
{
    public RecordingAction(string name)
    {
        Name = name;
        Description = name;
    }

    public string Name { get; }

    public string Description { get; }

    public ActionState State { get; set; } = ActionState.Ready;

    public bool IsEnabled => true;

    public int ExecutionCount { get; protected set; }

    public virtual void Cancel()
    {
    }

    public virtual Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        ExecutionCount++;
        State = ActionState.Completed;
        return Task.FromResult(true);
    }
}

internal sealed class ResultAction : RecordingAction
{
    private readonly bool _result;

    public ResultAction(string name, bool result) : base(name)
    {
        _result = result;
    }

    public override Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        ExecutionCount++;
        State = _result ? ActionState.Completed : ActionState.Failed;
        return Task.FromResult(_result);
    }
}

internal sealed class AtomicGateAction : RecordingAction, IPauseAwareAction
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AtomicGateAction(string name) : base(name)
    {
    }

    public ActionPauseBehavior PauseBehavior => ActionPauseBehavior.None;

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release()
    {
        _release.TrySetResult();
    }

    public override async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        ExecutionCount++;
        Started.TrySetResult();
        await _release.Task.WaitAsync(TimeSpan.FromSeconds(2));
        State = ActionState.Completed;
        return true;
    }
}

internal sealed class CooperativeBlockingAction : RecordingAction, IPauseAwareAction
{
    public CooperativeBlockingAction(string name) : base(name)
    {
    }

    public ActionPauseBehavior PauseBehavior => ActionPauseBehavior.Cooperative;

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        Started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return true;
    }
}
