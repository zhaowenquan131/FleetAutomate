using FleetAutomate.Icons;
using FleetAutomate.Model;

namespace FleetAutomate.Tests.Icons;

public class ActionIconRegistryTests
{
    [Fact]
    public void AllConcreteActionTypesHaveDedicatedIcons()
    {
        var actionTypes = typeof(IAction).Assembly
            .GetTypes()
            .Where(type => typeof(IAction).IsAssignableFrom(type))
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(type => type.FullName?.Contains(".Model.Actions.") == true)
            .OrderBy(type => type.FullName)
            .ToArray();

        var missing = actionTypes
            .Where(type => ActionIconRegistry.GetDescriptor(type).Key == "action-fallback")
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(missing.Length == 0, $"Missing action icons: {string.Join(", ", missing)}");
    }

    [Fact]
    public void SetVariableGenericVariantsUseSetVariableIcon()
    {
        var descriptor = ActionIconRegistry.GetDescriptor(typeof(FleetAutomate.Model.Actions.Logic.SetVariableAction<int>));

        Assert.Equal("logic-set-variable", descriptor.Key);
    }
}
