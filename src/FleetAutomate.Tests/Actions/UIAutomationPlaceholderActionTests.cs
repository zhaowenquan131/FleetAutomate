using FleetAutomate.Model.Actions.UIAutomation;

namespace FleetAutomate.Tests.Actions;

public sealed class UIAutomationPlaceholderActionTests
{
    [Fact]
    public async Task IfWindowContainsElementAction_SetsExistsResult()
    {
        var backend = new RecordingUiElementBackend { ElementExistsResult = true };
        var action = Create(new IfWindowContainsElementAction(), backend);

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.True(action.Exists);
        Assert.Equal(["Exists:AutomationId:target"], backend.Calls);
    }

    [Fact]
    public async Task SetFocusOnElementAction_FocusesElement()
    {
        var backend = new RecordingUiElementBackend();
        var action = Create(new SetFocusOnElementAction(), backend);

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(["Focus:AutomationId:target"], backend.Calls);
    }

    [Fact]
    public async Task SelectRadioButtonAction_SelectsElement()
    {
        var backend = new RecordingUiElementBackend();
        var action = Create(new SelectRadioButtonAction(), backend);

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(["SelectRadio:AutomationId:target"], backend.Calls);
    }

    [Fact]
    public async Task SetCheckBoxStateAction_SetsDesiredState()
    {
        var backend = new RecordingUiElementBackend();
        var action = Create(new SetCheckBoxStateAction { IsChecked = true }, backend);

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(["SetCheckBox:AutomationId:target:True"], backend.Calls);
    }

    [Fact]
    public async Task SelectComboBoxItemAction_SelectsItem()
    {
        var backend = new RecordingUiElementBackend();
        var action = Create(new SelectComboBoxItemAction { ItemText = "item" }, backend);

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(["SelectComboBox:AutomationId:target:item"], backend.Calls);
    }

    [Fact]
    public async Task SelectTabItemAction_SelectsElement()
    {
        var backend = new RecordingUiElementBackend();
        var action = Create(new SelectTabItemAction(), backend);

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(["SelectTab:AutomationId:target"], backend.Calls);
    }

    private static T Create<T>(T action, IUiElementAutomationBackend backend)
        where T : UiElementActionBase
    {
        action.IdentifierType = "AutomationId";
        action.ElementIdentifier = "target";
        action.Backend = backend;
        return action;
    }

    private sealed class RecordingUiElementBackend : IUiElementAutomationBackend
    {
        public List<string> Calls { get; } = [];
        public bool ElementExistsResult { get; init; }

        public bool ElementExists(UiElementActionContext context)
        {
            Calls.Add($"Exists:{context.IdentifierType}:{context.ElementIdentifier}");
            return ElementExistsResult;
        }

        public void Focus(UiElementActionContext context) => Calls.Add($"Focus:{context.IdentifierType}:{context.ElementIdentifier}");

        public void SelectRadioButton(UiElementActionContext context) => Calls.Add($"SelectRadio:{context.IdentifierType}:{context.ElementIdentifier}");

        public void SetCheckBoxState(UiElementActionContext context, bool isChecked) => Calls.Add($"SetCheckBox:{context.IdentifierType}:{context.ElementIdentifier}:{isChecked}");

        public void SelectComboBoxItem(UiElementActionContext context, string itemText) => Calls.Add($"SelectComboBox:{context.IdentifierType}:{context.ElementIdentifier}:{itemText}");

        public void SelectTabItem(UiElementActionContext context) => Calls.Add($"SelectTab:{context.IdentifierType}:{context.ElementIdentifier}");
    }
}
