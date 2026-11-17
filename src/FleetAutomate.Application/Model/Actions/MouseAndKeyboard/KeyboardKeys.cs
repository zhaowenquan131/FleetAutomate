using FormsKeys = System.Windows.Forms.Keys;

namespace FleetAutomate.Model.Actions.MouseAndKeyboard;

/// <summary>Shared key names for the editor and execution backend.</summary>
public static class KeyboardKeys
{
    private static readonly Dictionary<string, FormsKeys> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = FormsKeys.ControlKey, ["Control"] = FormsKeys.ControlKey,
        ["Alt"] = FormsKeys.Menu, ["Shift"] = FormsKeys.ShiftKey,
        ["Win"] = FormsKeys.LWin, ["Windows"] = FormsKeys.LWin,
        ["LCtrl"] = FormsKeys.LControlKey, ["RCtrl"] = FormsKeys.RControlKey,
        ["LAlt"] = FormsKeys.LMenu, ["RAlt"] = FormsKeys.RMenu,
        ["LShift"] = FormsKeys.LShiftKey, ["RShift"] = FormsKeys.RShiftKey,
        ["Esc"] = FormsKeys.Escape, ["Return"] = FormsKeys.Enter,
        ["Del"] = FormsKeys.Delete, ["Ins"] = FormsKeys.Insert,
        ["PgUp"] = FormsKeys.PageUp, ["PgDn"] = FormsKeys.PageDown,
        ["Backspace"] = FormsKeys.Back, ["BS"] = FormsKeys.Back,
        ["Plus"] = FormsKeys.Oemplus, ["Minus"] = FormsKeys.OemMinus,
        ["CapsLock"] = FormsKeys.Capital, ["PrintScreen"] = FormsKeys.Snapshot
    };

    public static IReadOnlyList<ushort> Parse(string? combination)
    {
        if (string.IsNullOrWhiteSpace(combination))
        {
            throw new ArgumentException("Please choose a key or shortcut.");
        }

        var result = new List<ushort>();
        foreach (var part in combination.Split('+'))
        {
            var name = part.Trim().Trim('{', '}');
            FormsKeys key;
            if (name.Length == 1 && char.IsAsciiDigit(name[0]))
            {
                key = FormsKeys.D0 + (name[0] - '0');
            }
            else if (!Aliases.TryGetValue(name, out key) &&
                (!Enum.TryParse(name, ignoreCase: true, out key) || !Enum.IsDefined(key)))
            {
                throw new ArgumentException($"Unknown key '{name}'. Use Record keys to choose a key or shortcut.");
            }

            var code = (int)key;
            if (code < 1 || code > 254 || name.Length == 0)
            {
                throw new ArgumentException($"Invalid key '{name}'.");
            }

            if (!result.Contains((ushort)code))
            {
                result.Add((ushort)code);
            }
        }

        return result.OrderBy(code => code switch
        {
            0x11 or 0xA2 or 0xA3 => 0,
            0x12 or 0xA4 or 0xA5 => 1,
            0x10 or 0xA0 or 0xA1 => 2,
            0x5B or 0x5C => 3,
            _ => 4
        }).ToArray();
    }

    public static string Format(IEnumerable<ushort> keys) => string.Join(" + ", keys.Distinct().Select(GetName));

    public static string GetName(ushort code) => (FormsKeys)code switch
    {
        FormsKeys.ControlKey => "Ctrl", FormsKeys.ShiftKey => "Shift", FormsKeys.Menu => "Alt",
        FormsKeys.LWin => "Win", FormsKeys.LControlKey => "LCtrl", FormsKeys.RControlKey => "RCtrl",
        FormsKeys.LShiftKey => "LShift", FormsKeys.RShiftKey => "RShift",
        FormsKeys.LMenu => "LAlt", FormsKeys.RMenu => "RAlt",
        FormsKeys.Back => "Backspace", FormsKeys.Capital => "CapsLock",
        FormsKeys.Prior => "PageUp", FormsKeys.Next => "PageDown", FormsKeys.Snapshot => "PrintScreen",
        FormsKeys.Oemplus => "Plus", FormsKeys.OemMinus => "Minus",
        >= FormsKeys.D0 and <= FormsKeys.D9 => ((char)code).ToString(),
        _ => ((FormsKeys)code).ToString()
    };

    public static void SendShortcut(IInputAutomationBackend backend, string combination, CancellationToken cancellationToken)
    {
        var keys = Parse(combination);
        var pressed = new List<string>();
        try
        {
            foreach (var code in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = GetName(code);
                backend.KeyDown(name);
                pressed.Add(name);
            }
        }
        finally
        {
            // Attempt every release, even if a later key fails to press or release.
            List<Exception>? errors = null;
            foreach (var name in pressed.AsEnumerable().Reverse())
            {
                try { backend.KeyUp(name); }
                catch (Exception ex) { (errors ??= []).Add(ex); }
            }

            if (errors != null)
            {
                throw new AggregateException("Could not release all shortcut keys.", errors);
            }
        }
    }
}
