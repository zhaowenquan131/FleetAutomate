using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using FormsCursor = System.Windows.Forms.Cursor;

namespace FleetAutomate.Model.Actions.MouseAndKeyboard;

public enum MouseButton
{
    Left,
    Right,
    Middle
}

public interface IInputAutomationBackend
{
    void MoveMouse(int x, int y);
    void Click(MouseButton button);
    void DoubleClick(MouseButton button);
    void SendKeys(string text, bool wait);
    void KeyDown(string key);
    void KeyUp(string key);
}

[DataContract]
public sealed class MoveMouseToAction : InputActionBase
{
    private int _x;
    private int _y;

    public override string Name => "Move Mouse To";
    protected override string DefaultDescription => "Move mouse to screen coordinates";

    [DataMember]
    public int X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    [DataMember]
    public int Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Backend.MoveMouse(X, Y);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class MouseSingleClickAction : PointerClickActionBase
{
    public override string Name => "Mouse Single Click";
    protected override string DefaultDescription => "Perform a mouse click";

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Backend.MoveMouse(X, Y);
        Backend.Click(Button);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class MouseDoubleClickAction : PointerClickActionBase
{
    public override string Name => "Mouse Double Click";
    protected override string DefaultDescription => "Perform a mouse double click";

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Backend.MoveMouse(X, Y);
        Backend.DoubleClick(Button);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class SendKeysAction : InputActionBase
{
    private string _text = string.Empty;
    private bool _wait = true;

    public override string Name => "Send Keys";
    protected override string DefaultDescription => "Send keyboard input";

    [DataMember]
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    [DataMember]
    public bool Wait
    {
        get => _wait;
        set => SetProperty(ref _wait, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Backend.SendKeys(Text, Wait);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class SendKeyDownAction : KeyTransitionActionBase
{
    public override string Name => "Send Key Down";
    protected override string DefaultDescription => "Press key down";

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Backend.KeyDown(Key);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class SendKeyUpAction : KeyTransitionActionBase
{
    public override string Name => "Send Key Up";
    protected override string DefaultDescription => "Release key";

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Backend.KeyUp(Key);
        return Task.CompletedTask;
    }
}

[DataContract]
public abstract class InputActionBase : ActionBase
{
    private IInputAutomationBackend? _backend;

    [IgnoreDataMember]
    public IInputAutomationBackend Backend
    {
        get => _backend ??= WinFormsInputAutomationBackend.Instance;
        set => _backend = value ?? throw new ArgumentNullException(nameof(value));
    }
}

[DataContract]
public abstract class PointerClickActionBase : InputActionBase
{
    private int _x;
    private int _y;
    private MouseButton _button = MouseButton.Left;

    [DataMember]
    public int X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    [DataMember]
    public int Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    [DataMember]
    public MouseButton Button
    {
        get => _button;
        set => SetProperty(ref _button, value);
    }
}

[DataContract]
public abstract class KeyTransitionActionBase : InputActionBase
{
    private string _key = string.Empty;

    [DataMember]
    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }
}

internal sealed class WinFormsInputAutomationBackend : IInputAutomationBackend
{
    public static WinFormsInputAutomationBackend Instance { get; } = new();

    private WinFormsInputAutomationBackend()
    {
    }

    public void MoveMouse(int x, int y)
    {
        FormsCursor.Position = new global::System.Drawing.Point(x, y);
    }

    public void Click(MouseButton button)
    {
        var (down, up) = GetMouseEvents(button);
        mouse_event(down, 0, 0, 0, UIntPtr.Zero);
        mouse_event(up, 0, 0, 0, UIntPtr.Zero);
    }

    public void DoubleClick(MouseButton button)
    {
        Click(button);
        Click(button);
    }

    public void SendKeys(string text, bool wait)
    {
        if (wait)
        {
            global::System.Windows.Forms.SendKeys.SendWait(text ?? string.Empty);
            return;
        }

        global::System.Windows.Forms.SendKeys.Send(text ?? string.Empty);
    }

    public void KeyDown(string key)
    {
        global::System.Windows.Forms.SendKeys.SendWait($"{{{RequireKey(key)} down}}");
    }

    public void KeyUp(string key)
    {
        global::System.Windows.Forms.SendKeys.SendWait($"{{{RequireKey(key)} up}}");
    }

    private static string RequireKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Key cannot be empty.");
        }

        return key;
    }

    private static (uint Down, uint Up) GetMouseEvents(MouseButton button)
    {
        return button switch
        {
            MouseButton.Right => (0x0008, 0x0010),
            MouseButton.Middle => (0x0020, 0x0040),
            _ => (0x0002, 0x0004)
        };
    }

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
}
