using System.Runtime.Serialization;
using System.Xml.Linq;
using FleetAutomate.Model.Actions.MouseAndKeyboard;

namespace FleetAutomate.Tests.Actions;

public sealed class KeyboardInputTests
{
    [Theory]
    [InlineData("Ctrl + Shift + C", "Ctrl + Shift + C")]
    [InlineData("control + alt + del", "Ctrl + Alt + Delete")]
    [InlineData("{ENTER}", "Enter")]
    [InlineData("Esc", "Escape")]
    [InlineData("Win + 1", "Win + 1")]
    [InlineData("Ctrl + Plus", "Ctrl + Plus")]
    [InlineData("Ctrl + Ctrl + F5", "Ctrl + F5")]
    [InlineData("C + Shift + Ctrl", "Ctrl + Shift + C")]
    public void KeyNames_RoundTrip(string input, string expected)
    {
        Assert.Equal(expected, KeyboardKeys.Format(KeyboardKeys.Parse(input)));
        Assert.Equal(KeyboardKeys.Parse(input), KeyboardKeys.Parse(expected));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl +")]
    [InlineData("Ctrl + not-a-key")]
    public void InvalidShortcut_DoesNotPressAnyKeys(string input)
    {
        var backend = new RecordingBackend();
        Assert.Throws<ArgumentException>(() => KeyboardKeys.SendShortcut(backend, input, CancellationToken.None));
        Assert.Empty(backend.Calls);
    }

    [Fact]
    public async Task Shortcut_PressesInOrderAndReleasesInReverse()
    {
        var backend = new RecordingBackend();
        var action = new SendKeysAction
        {
            InputMode = KeyboardInputMode.Shortcut, KeyCombination = "Ctrl + Shift + C", Backend = backend
        };
        Assert.True(await action.ExecuteAsync(CancellationToken.None));
        Assert.Equal(["down:Ctrl", "down:Shift", "down:C", "up:C", "up:Shift", "up:Ctrl"], backend.Calls);
    }

    [Fact]
    public void Shortcut_FailedPressStillReleasesModifiers()
    {
        var backend = new RecordingBackend { FailPress = "C" };
        Assert.Throws<InvalidOperationException>(() => KeyboardKeys.SendShortcut(backend, "Ctrl + Shift + C", CancellationToken.None));
        Assert.Equal(["down:Ctrl", "down:Shift", "down:C", "up:Shift", "up:Ctrl"], backend.Calls);
    }

    [Fact]
    public void Shortcut_CancellationStillReleasesModifiers()
    {
        using var cancellation = new CancellationTokenSource();
        var backend = new RecordingBackend { AfterPress = cancellation.Cancel };
        Assert.Throws<OperationCanceledException>(() => KeyboardKeys.SendShortcut(backend, "Ctrl + C", cancellation.Token));
        Assert.Equal(["down:Ctrl", "up:Ctrl"], backend.Calls);
    }

    [Fact]
    public void Shortcut_FailedReleaseStillReleasesOtherKeys()
    {
        var backend = new RecordingBackend { FailRelease = "C" };
        Assert.Throws<AggregateException>(() => KeyboardKeys.SendShortcut(backend, "Ctrl + C", CancellationToken.None));
        Assert.Equal(["down:Ctrl", "down:C", "up:C", "up:Ctrl"], backend.Calls);
    }

    [Fact]
    public async Task Serialization_PreservesShortcutAndLoadsOldTextActions()
    {
        var serializer = new DataContractSerializer(typeof(SendKeysAction));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, new SendKeysAction
        {
            InputMode = KeyboardInputMode.Shortcut, KeyCombination = "Alt + F4", Text = "legacy"
        });
        stream.Position = 0;
        var shortcut = (SendKeysAction)serializer.ReadObject(stream)!;
        Assert.Equal(KeyboardInputMode.Shortcut, shortcut.InputMode);
        Assert.Equal("Alt + F4", shortcut.KeyCombination);

        stream.Position = 0;
        var xml = XDocument.Load(stream);
        xml.Root!.Elements().Where(element => element.Name.LocalName is "InputMode" or "KeyCombination").Remove();
        using var reader = xml.CreateReader();
        var oldAction = (SendKeysAction)serializer.ReadObject(reader)!;
        var backend = new RecordingBackend();
        oldAction.Backend = backend;
        Assert.Equal(KeyboardInputMode.Text, oldAction.InputMode);
        Assert.True(await oldAction.ExecuteAsync(CancellationToken.None));
        Assert.Equal(["text:legacy"], backend.Calls);
    }

    private sealed class RecordingBackend : IInputAutomationBackend
    {
        public List<string> Calls { get; } = [];
        public string? FailPress { get; init; }
        public string? FailRelease { get; init; }
        public Action? AfterPress { get; init; }
        public void MoveMouse(int x, int y) => throw new NotSupportedException();
        public void Click(MouseButton button) => throw new NotSupportedException();
        public void DoubleClick(MouseButton button) => throw new NotSupportedException();
        public void SendKeys(string text, bool wait) => Calls.Add($"text:{text}");
        public void KeyDown(string key)
        {
            Calls.Add($"down:{key}");
            if (key == FailPress) throw new InvalidOperationException("Press failed");
            AfterPress?.Invoke();
        }
        public void KeyUp(string key)
        {
            Calls.Add($"up:{key}");
            if (key == FailRelease) throw new InvalidOperationException("Release failed");
        }
    }
}
