using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FleetAutomate.Model.Actions.MouseAndKeyboard;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace FleetAutomate.View.Controls;

/// <summary>Records a key or chord without allowing it to invoke dialog commands.</summary>
public sealed class KeyboardShortcutInput : System.Windows.Controls.UserControl
{
    private readonly WpfTextBox _input;
    private readonly WpfButton _recordButton;
    private readonly TextBlock _hint;
    private bool _recording;
    private bool _hasRecordedKey;
    private Key _primaryKey;
    private string _previousValue = string.Empty;

    public string Value
    {
        get => _input.Text;
        set => _input.Text = value ?? string.Empty;
    }

    public KeyboardShortcutInput()
    {
        var panel = new StackPanel();
        var row = new DockPanel();
        _recordButton = new WpfButton { Content = "Record keys", MinWidth = 110, Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(_recordButton, Dock.Right);
        _input = new WpfTextBox { MinHeight = 32, VerticalContentAlignment = VerticalAlignment.Center };
        _hint = new TextBlock
        {
            Text = "Record a key or shortcut, for example Ctrl + C, Enter, Tab or F5.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        _hint.SetResourceReference(ForegroundProperty, "TextFillColorSecondaryBrush");
        row.Children.Add(_recordButton);
        row.Children.Add(_input);
        panel.Children.Add(row);
        panel.Children.Add(_hint);
        Content = panel;
        _recordButton.Click += (_, _) =>
        {
            if (_recording)
            {
                StopRecording(cancel: true);
            }
            else
            {
                _previousValue = Value;
                _recording = true;
                _hasRecordedKey = false;
                _primaryKey = Key.None;
                _input.IsReadOnly = true;
                _recordButton.Content = "Cancel recording";
                _hint.Text = "Press a key or shortcut. Tab, Enter and Esc are recorded as keys.";
                _input.Focus();
            }
        };
        PreviewKeyDown += RecordKeyDown;
        PreviewKeyUp += RecordKeyUp;
        _input.LostKeyboardFocus += (_, e) =>
        {
            if (_recording && !ReferenceEquals(e.NewFocus, _recordButton))
            {
                StopRecording(cancel: !_hasRecordedKey);
            }
        };
        Unloaded += (_, _) => StopRecording(cancel: true);
    }

    private void RecordKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_recording)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (e.IsRepeat || key is Key.None or Key.ImeProcessed or Key.DeadCharProcessed)
        {
            return;
        }

        var keys = new List<ushort>();
        var modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Control)) keys.Add(0x11);
        if (modifiers.HasFlag(ModifierKeys.Alt)) keys.Add(0x12);
        if (modifiers.HasFlag(ModifierKeys.Shift)) keys.Add(0x10);
        if (modifiers.HasFlag(ModifierKeys.Windows)) keys.Add(0x5B);
        if (!IsModifier(key))
        {
            keys.Add((ushort)KeyInterop.VirtualKeyFromKey(key));
            _primaryKey = key;
        }
        else if (keys.Count == 0)
        {
            keys.Add((ushort)KeyInterop.VirtualKeyFromKey(key));
        }

        Value = KeyboardKeys.Format(keys);
        _hasRecordedKey = true;
    }

    private void RecordKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_recording)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_hasRecordedKey && (key == _primaryKey || _primaryKey == Key.None))
        {
            StopRecording(cancel: false);
        }
    }

    private static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private void StopRecording(bool cancel)
    {
        if (!_recording)
        {
            return;
        }

        if (cancel) Value = _previousValue;
        _recording = false;
        _input.IsReadOnly = false;
        _recordButton.Content = "Record keys";
        _hint.Text = "Record a key or shortcut, for example Ctrl + C, Enter, Tab or F5.";
    }
}
