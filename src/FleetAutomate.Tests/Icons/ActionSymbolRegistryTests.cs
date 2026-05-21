using FleetAutomate.Icons;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;

namespace FleetAutomate.Tests.Icons;

public class ActionSymbolRegistryTests
{
    [Fact]
    public void AllConcreteActionTypesHaveDedicatedSymbols()
    {
        var actionTypes = typeof(IAction).Assembly
            .GetTypes()
            .Where(type => typeof(IAction).IsAssignableFrom(type))
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(type => type.FullName?.Contains(".Model.Actions.") == true)
            .OrderBy(type => type.FullName)
            .ToArray();

        var missing = actionTypes
            .Where(type => ActionSymbolRegistry.GetSymbol(type) == ActionSymbolRegistry.FallbackSymbol)
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(missing.Length == 0, $"Missing action symbols: {string.Join(", ", missing)}");
    }

    [Fact]
    public void SetVariableGenericVariantsUseSetVariableSymbol()
    {
        var expected = ActionSymbolRegistry.GetSymbol(typeof(SetVariableAction<object>));
        var actual = ActionSymbolRegistry.GetSymbol(typeof(SetVariableAction<int>));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Logic & Flow")]
    [InlineData("System")]
    [InlineData("UI Automation")]
    [InlineData("Scripts")]
    [InlineData("File System")]
    [InlineData("Mouse & Keyboard")]
    [InlineData("Text")]
    [InlineData("Date & Time")]
    public void ToolboxCategoriesHaveDedicatedSymbols(string categoryName)
    {
        Assert.NotEqual(ActionSymbolRegistry.FallbackSymbol, ActionSymbolRegistry.GetCategorySymbol(categoryName));
    }
}
