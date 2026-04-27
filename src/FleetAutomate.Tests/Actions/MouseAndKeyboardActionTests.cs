using FleetAutomate.Model.Actions.MouseAndKeyboard;

namespace FleetAutomate.Tests.Actions;

public sealed class MouseAndKeyboardActionTests
{
    [Fact]
    public async Task MoveMouseToAction_MovesPointer()
    {
        var backend = new RecordingInputAutomationBackend();
        var action = new MoveMouseToAction { X = 10, Y = 20, Backend = backend };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(["MoveMouse:10,20"], backend.Calls);
    }

    [Fact]
    public async Task MouseSingleClickAction_ClicksConfiguredButton()
    {
        var backend = new RecordingInputAutomationBackend();
        var action = new MouseSingleClickAction
        {
            X = 10,
            Y = 20,
            Button = MouseButton.Right,
            Backend = backend
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(["MoveMouse:10,20", "Click:Right"], backend.Calls);
    }

    [Fact]
    public async Task MouseDoubleClickAction_DoubleClicksConfiguredButton()
    {
        var backend = new RecordingInputAutomationBackend();
        var action = new MouseDoubleClickAction
        {
            X = 10,
            Y = 20,
            Button = MouseButton.Left,
            Backend = backend
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(["MoveMouse:10,20", "DoubleClick:Left"], backend.Calls);
    }

    [Fact]
    public async Task SendKeysAction_SendsText()
    {
        var backend = new RecordingInputAutomationBackend();
        var action = new SendKeysAction { Text = "abc", Backend = backend };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(["SendKeys:abc:True"], backend.Calls);
    }

    [Fact]
    public async Task SendKeyDownAndUpActions_SendKeyTransitions()
    {
        var backend = new RecordingInputAutomationBackend();
        var down = new SendKeyDownAction { Key = "A", Backend = backend };
        var up = new SendKeyUpAction { Key = "A", Backend = backend };

        var downResult = await down.ExecuteAsync(CancellationToken.None);
        var upResult = await up.ExecuteAsync(CancellationToken.None);

        Assert.True(downResult);
        Assert.True(upResult);
        Assert.Equal(["KeyDown:A", "KeyUp:A"], backend.Calls);
    }

    private sealed class RecordingInputAutomationBackend : IInputAutomationBackend
    {
        public List<string> Calls { get; } = [];

        public void MoveMouse(int x, int y) => Calls.Add($"MoveMouse:{x},{y}");

        public void Click(MouseButton button) => Calls.Add($"Click:{button}");

        public void DoubleClick(MouseButton button) => Calls.Add($"DoubleClick:{button}");

        public void SendKeys(string text, bool wait) => Calls.Add($"SendKeys:{text}:{wait}");

        public void KeyDown(string key) => Calls.Add($"KeyDown:{key}");

        public void KeyUp(string key) => Calls.Add($"KeyUp:{key}");
    }
}
