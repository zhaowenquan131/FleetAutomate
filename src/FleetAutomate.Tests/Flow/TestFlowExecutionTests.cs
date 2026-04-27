using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Expression;
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
    public async Task StartFromActionAsync_RunsFromSpecifiedActionOnly()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        var first = new RecordingAction("first");
        var second = new RecordingAction("second");
        var third = new RecordingAction("third");
        flow.Actions.Add(first);
        flow.Actions.Add(second);
        flow.Actions.Add(third);

        var result = await flow.StartFromActionAsync(second, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(ActionState.Completed, flow.State);
        Assert.Equal(0, first.ExecutionCount);
        Assert.Equal(1, second.ExecutionCount);
        Assert.Equal(1, third.ExecutionCount);
    }

    [Fact]
    public async Task StartFromActionIndexAsync_RunsFromSpecifiedIndexOnly()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        var first = new RecordingAction("first");
        var second = new RecordingAction("second");
        var third = new RecordingAction("third");
        flow.Actions.Add(first);
        flow.Actions.Add(second);
        flow.Actions.Add(third);

        var result = await flow.StartFromActionIndexAsync(2, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(ActionState.Completed, flow.State);
        Assert.Equal(0, first.ExecutionCount);
        Assert.Equal(0, second.ExecutionCount);
        Assert.Equal(1, third.ExecutionCount);
    }

    [Fact]
    public async Task StartFromActionAsync_WhenActionIsNotInFlow_Throws()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };

        var missing = new RecordingAction("missing");

        await Assert.ThrowsAsync<ArgumentException>(() => flow.StartFromActionAsync(missing, CancellationToken.None));
    }

    [Fact]
    public async Task StartFromActionIndexAsync_WhenIndexIsOutOfRange_Throws()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        flow.Actions.Add(new RecordingAction("first"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => flow.StartFromActionIndexAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task StepActionAsync_ExecutesSpecifiedActionAndStops()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        var first = new RecordingAction("first");
        var second = new RecordingAction("second");
        var third = new RecordingAction("third");
        flow.Actions.Add(first);
        flow.Actions.Add(second);
        flow.Actions.Add(third);

        var result = await flow.StepActionAsync(second, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(ActionState.Paused, flow.State);
        Assert.Equal(TestFlowBreakReason.StepCompleted, flow.BreakReason);
        Assert.Equal(0, first.ExecutionCount);
        Assert.Equal(1, second.ExecutionCount);
        Assert.Equal(0, third.ExecutionCount);
        Assert.Same(third, flow.CurrentAction);
    }

    [Fact]
    public async Task StepActionAsync_WhenActionIsNotInFlow_Throws()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        flow.Actions.Add(new RecordingAction("first"));

        var missing = new RecordingAction("missing");

        await Assert.ThrowsAsync<ArgumentException>(() => flow.StepActionAsync(missing, CancellationToken.None));
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

    [Fact]
    public async Task Pause_OnIfAction_WaitsForCurrentChildThenPausesBeforeNextChild()
    {
        var flow = new TestFlow
        {
            Name = "flow"
        };
        var current = new AtomicGateAction("atomic");
        var blockedSibling = new RecordingAction("blocked");
        var next = new RecordingAction("next");
        var ifAction = new IfAction
        {
            Condition = true,
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };
        ifAction.IfBlock.Add(current);
        ifAction.IfBlock.Add(blockedSibling);
        flow.Actions.Add(ifAction);
        flow.Actions.Add(next);

        var runTask = flow.StartAsync(CancellationToken.None);
        await current.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        flow.Pause();
        current.Release();

        var result = await runTask;

        Assert.True(result);
        Assert.Equal(ActionState.Paused, flow.State);
        Assert.Equal(TestFlowBreakReason.PauseRequested, flow.BreakReason);
        Assert.Same(ifAction, flow.CurrentAction);
        Assert.Equal(ActionState.Paused, ifAction.State);
        Assert.Equal(1, current.ExecutionCount);
        Assert.Equal(0, blockedSibling.ExecutionCount);
        Assert.Equal(0, next.ExecutionCount);
    }

    [Fact]
    public async Task IfAction_WhenConditionTrue_ExecutesIfBlockOnly()
    {
        var action = new IfAction
        {
            Condition = true,
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };
        var ifBlockAction = new RecordingAction("if");
        var elseBlockAction = new RecordingAction("else");
        action.IfBlock.Add(ifBlockAction);
        action.ElseBlock.Add(elseBlockAction);

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(ActionState.Completed, action.State);
        Assert.Equal(1, ifBlockAction.ExecutionCount);
        Assert.Equal(0, elseBlockAction.ExecutionCount);
    }

    [Fact]
    public async Task IfAction_WhenConditionFalse_ExecutesElseBlockOnly()
    {
        var action = new IfAction
        {
            Condition = false,
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };
        var ifBlockAction = new RecordingAction("if");
        var elseBlockAction = new RecordingAction("else");
        action.IfBlock.Add(ifBlockAction);
        action.ElseBlock.Add(elseBlockAction);

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(ActionState.Completed, action.State);
        Assert.Equal(0, ifBlockAction.ExecutionCount);
        Assert.Equal(1, elseBlockAction.ExecutionCount);
    }

    [Fact]
    public async Task IfAction_WhenConditionIsExpression_UsesEnvironmentVariables()
    {
        var environment = new FleetAutomate.Model.Actions.Logic.Environment();
        environment.Variables.Add(new Variable("count", 3, typeof(int)));

        var action = new IfAction
        {
            Condition = new EqualExpression
            {
                OperandLeft = "count",
                OperandRight = 3
            },
            Environment = environment
        };
        var ifBlockAction = new RecordingAction("if");
        var elseBlockAction = new RecordingAction("else");
        action.IfBlock.Add(ifBlockAction);
        action.ElseBlock.Add(elseBlockAction);

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(ActionState.Completed, action.State);
        Assert.Equal(1, ifBlockAction.ExecutionCount);
        Assert.Equal(0, elseBlockAction.ExecutionCount);
    }

    [Fact]
    public async Task IfAction_WhenChildFails_ReturnsFalseAndSetsFailed()
    {
        var action = new IfAction
        {
            Condition = true,
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };
        var failingChild = new ResultAction("failing", false);
        var skippedElseChild = new RecordingAction("else");
        action.IfBlock.Add(failingChild);
        action.ElseBlock.Add(skippedElseChild);

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.False(result);
        Assert.Equal(ActionState.Failed, action.State);
        Assert.Equal(1, failingChild.ExecutionCount);
        Assert.Equal(0, skippedElseChild.ExecutionCount);
    }

    [Fact]
    public async Task IfAction_WhenConditionIsInvalid_ThrowsAndSetsFailed()
    {
        var action = new IfAction
        {
            Condition = "invalid",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => action.ExecuteAsync(CancellationToken.None));
        Assert.Equal(ActionState.Failed, action.State);
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
