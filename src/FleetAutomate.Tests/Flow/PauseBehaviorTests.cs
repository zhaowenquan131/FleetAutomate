using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Tests.Flow;

public sealed class PauseBehaviorTests
{
    [Fact]
    public void ClickElementAction_WithRetries_IsPauseAwareBetweenAttempts()
    {
        var action = new ClickElementAction
        {
            RetryTimes = 3
        };

        Assert.Equal(ActionPauseBehavior.BetweenAttempts, action.PauseBehavior);
    }

    [Fact]
    public void ClickElementAction_WithoutRetries_IsAtomicForPause()
    {
        var action = new ClickElementAction
        {
            RetryTimes = 0
        };

        Assert.Equal(ActionPauseBehavior.None, action.PauseBehavior);
    }

    [Fact]
    public void WaitForElementAction_IsCooperativelyPausable()
    {
        var action = new WaitForElementAction();

        Assert.Equal(ActionPauseBehavior.Cooperative, action.PauseBehavior);
    }

    [Fact]
    public void SetVariableAction_Cancel_DoesNotThrow()
    {
        var action = new SetVariableAction<int>("stepCount", 1)
        {
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };

        var exception = Record.Exception(action.Cancel);

        Assert.Null(exception);
    }
}
