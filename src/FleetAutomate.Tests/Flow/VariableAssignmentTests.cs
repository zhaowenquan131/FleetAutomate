using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Tests.Flow;

public sealed class VariableAssignmentTests
{
    [Fact]
    public async Task SetVariableAction_WhenVariableAlreadyExists_UpdatesInsteadOfAppending()
    {
        var environment = new FleetAutomate.Model.Actions.Logic.Environment();
        environment.Variables.Add(new Variable("stepCount", 1, typeof(int)));

        var action = new SetVariableAction<int>("stepCount", 2)
        {
            Environment = environment
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(ActionState.Completed, action.State);
        var variable = Assert.Single(environment.Variables);
        Assert.Equal("stepCount", variable.Name);
        Assert.Equal(2, variable.Value);
        Assert.Equal(typeof(int), variable.Type);
    }

    [Fact]
    public void GetRuntimeVariableValues_WhenDuplicateNamesExist_UsesLatestValue()
    {
        var flow = new TestFlow
        {
            Name = "flow",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };
        flow.Environment.Variables.Add(new Variable("stepCount", 1, typeof(int)));
        flow.Environment.Variables.Add(new Variable("stepCount", 2, typeof(int)));

        var values = flow.GetRuntimeVariableValues();

        Assert.Single(values);
        Assert.Equal(2, values["stepCount"]);
    }
}
