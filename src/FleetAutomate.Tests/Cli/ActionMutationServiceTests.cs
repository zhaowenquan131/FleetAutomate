using FleetAutomate.Application.Commanding;
using FleetAutomate.Model.Actions.System;

namespace FleetAutomate.Tests.Cli;

public sealed class ActionMutationServiceTests
{
    [Fact]
    public void CreateAction_SupportsWaitDurationAction()
    {
        var service = new ActionMutationService();

        var action = service.CreateAction("WaitDurationAction");

        Assert.IsType<WaitDurationAction>(action);
    }
}
