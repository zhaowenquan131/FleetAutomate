using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Canvas.TestRunner.Model.Actions;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions;
using FleetAutomate.Model.Actions.DateAndTime;
using FleetAutomate.Model.Actions.FileSystem;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.Model.Actions.MouseAndKeyboard;
using FleetAutomate.Model.Actions.Scripts;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.Text;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Flow;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace FleetAutomate.Icons;

public static class ActionIconRegistry
{
    private static readonly Dictionary<Type, ActionIconDescriptor> ActionDescriptors = new()
    {
        [typeof(IfAction)] = new("logic-if", "#5E7CE2", IconGlyph.Branch),
        [typeof(WhileLoopAction)] = new("logic-while-loop", "#5E7CE2", IconGlyph.Loop),
        [typeof(ForLoopAction)] = new("logic-for-loop", "#5E7CE2", IconGlyph.CounterLoop),
        [typeof(SetVariableAction<object>)] = new("logic-set-variable", "#5E7CE2", IconGlyph.Variable),
        [typeof(SubFlowAction)] = new("logic-sub-flow", "#5E7CE2", IconGlyph.Link),

        [typeof(LaunchApplicationAction)] = new("system-launch-application", "#1E88E5", IconGlyph.Rocket),
        [typeof(WaitDurationAction)] = new("system-wait", "#1E88E5", IconGlyph.Clock),
        [typeof(LogAction)] = new("system-log", "#1E88E5", IconGlyph.Message),
        [typeof(IfProcessExistsAction)] = new("system-if-process-exists", "#1E88E5", IconGlyph.ProcessSearch),
        [typeof(KillProcessAction)] = new("system-kill-process", "#D84315", IconGlyph.Stop),
        [typeof(GetScreenshotAction)] = new("system-get-screenshot", "#1E88E5", IconGlyph.Camera),
        [typeof(SetClipboardAction)] = new("system-set-clipboard", "#1E88E5", IconGlyph.Clipboard),
        [typeof(PlaySoundAction)] = new("system-play-sound", "#1E88E5", IconGlyph.Sound),

        [typeof(WaitForElementAction)] = new("ui-wait-for-element", "#00ACC1", IconGlyph.TargetClock),
        [typeof(ClickElementAction)] = new("ui-click-element", "#00ACC1", IconGlyph.CursorClick),
        [typeof(IfWindowContainsTextAction)] = new("ui-if-window-contains-text", "#00ACC1", IconGlyph.TextSearch),
        [typeof(IfWindowContainsElementAction)] = new("ui-if-window-contains-element", "#00ACC1", IconGlyph.ElementSearch),
        [typeof(SetTextAction)] = new("ui-set-text", "#00ACC1", IconGlyph.Keyboard),
        [typeof(SetFocusOnElementAction)] = new("ui-set-focus-on-element", "#00ACC1", IconGlyph.Focus),
        [typeof(SelectRadioButtonAction)] = new("ui-select-radio-button", "#00ACC1", IconGlyph.Radio),
        [typeof(SetCheckBoxStateAction)] = new("ui-set-checkbox-state", "#00ACC1", IconGlyph.Checkbox),
        [typeof(SelectComboBoxItemAction)] = new("ui-select-combobox-item", "#00ACC1", IconGlyph.ComboBox),
        [typeof(SelectTabItemAction)] = new("ui-select-tab-item", "#00ACC1", IconGlyph.Tabs),

        [typeof(RunCommandAction)] = new("scripts-run-cmd", "#7E57C2", IconGlyph.Terminal),
        [typeof(RunPowerShellCommandAction)] = new("scripts-run-powershell-command", "#7E57C2", IconGlyph.PowerShell),
        [typeof(RunBatchScriptAction)] = new("scripts-run-batch-script", "#7E57C2", IconGlyph.Script),
        [typeof(RunPowerShellScriptAction)] = new("scripts-run-powershell-script", "#7E57C2", IconGlyph.PowerShellFile),
        [typeof(RunPythonScriptAction)] = new("scripts-run-python-script", "#7E57C2", IconGlyph.Python),

        [typeof(IfFileExistsAction)] = new("file-if-file-exists", "#43A047", IconGlyph.FileSearch),
        [typeof(IfDirectoryExistsAction)] = new("file-if-directory-exists", "#43A047", IconGlyph.FolderSearch),
        [typeof(CreateDirectoryAction)] = new("file-create-directory", "#43A047", IconGlyph.FolderAdd),
        [typeof(ClearDirectoryAction)] = new("file-clear-directory", "#43A047", IconGlyph.FolderClean),
        [typeof(DeleteDirectoryAction)] = new("file-delete-directory", "#D84315", IconGlyph.FolderDelete),
        [typeof(WaitForFileAction)] = new("file-wait-for-file", "#43A047", IconGlyph.FileClock),
        [typeof(CopyFileAction)] = new("file-copy-file", "#43A047", IconGlyph.FileCopy),
        [typeof(MoveFileAction)] = new("file-move-file", "#43A047", IconGlyph.FileMove),
        [typeof(DeleteFileAction)] = new("file-delete-file", "#D84315", IconGlyph.FileDelete),
        [typeof(RenameFileAction)] = new("file-rename-file", "#43A047", IconGlyph.FileEdit),
        [typeof(ReadTextFromFileAction)] = new("file-read-text-from-file", "#43A047", IconGlyph.FileRead),
        [typeof(WriteTextToFileAction)] = new("file-write-text-to-file", "#43A047", IconGlyph.FileWrite),
        [typeof(GetDirectoryOfFileAction)] = new("file-get-directory-of-file", "#43A047", IconGlyph.FolderPath),

        [typeof(MoveMouseToAction)] = new("input-move-mouse-to", "#FB8C00", IconGlyph.MouseMove),
        [typeof(MouseSingleClickAction)] = new("input-mouse-single-click", "#FB8C00", IconGlyph.MouseClick),
        [typeof(MouseDoubleClickAction)] = new("input-mouse-double-click", "#FB8C00", IconGlyph.MouseDoubleClick),
        [typeof(SendKeysAction)] = new("input-send-keys", "#FB8C00", IconGlyph.Keyboard),
        [typeof(SendKeyDownAction)] = new("input-send-key-down", "#FB8C00", IconGlyph.KeyDown),
        [typeof(SendKeyUpAction)] = new("input-send-key-up", "#FB8C00", IconGlyph.KeyUp),

        [typeof(ChangeTextCaseAction)] = new("text-change-case", "#00897B", IconGlyph.TextCase),
        [typeof(ReplaceTextAction)] = new("text-replace", "#00897B", IconGlyph.Replace),
        [typeof(SubstringAction)] = new("text-substring", "#00897B", IconGlyph.Substring),

        [typeof(GetCurrentDateTimeAction)] = new("datetime-current", "#F4511E", IconGlyph.CalendarClock),
        [typeof(FormatDateTimeAction)] = new("datetime-format", "#F4511E", IconGlyph.CalendarText),
        [typeof(AddDateTimeAction)] = new("datetime-add", "#F4511E", IconGlyph.CalendarAdd),

        [typeof(NotImplementedAction)] = new("action-not-implemented", "#78909C", IconGlyph.Placeholder),
        [typeof(ActionBlock)] = new("action-block", "#78909C", IconGlyph.Block),
        [typeof(TestFlow)] = new("test-flow", "#78909C", IconGlyph.Flow),
    };

    private static readonly Dictionary<string, ActionIconDescriptor> CategoryDescriptors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LogicAndFlow"] = new("category-logic-flow", "#5E7CE2", IconGlyph.Branch),
        ["Logic & Flow"] = new("category-logic-flow", "#5E7CE2", IconGlyph.Branch),
        ["System"] = new("category-system", "#1E88E5", IconGlyph.Desktop),
        ["UIAutomation"] = new("category-ui-automation", "#00ACC1", IconGlyph.Target),
        ["UI Automation"] = new("category-ui-automation", "#00ACC1", IconGlyph.Target),
        ["Scripts"] = new("category-scripts", "#7E57C2", IconGlyph.Terminal),
        ["FileSystem"] = new("category-file-system", "#43A047", IconGlyph.Folder),
        ["File System"] = new("category-file-system", "#43A047", IconGlyph.Folder),
        ["MouseAndKeyboard"] = new("category-mouse-keyboard", "#FB8C00", IconGlyph.KeyboardMouse),
        ["Mouse & Keyboard"] = new("category-mouse-keyboard", "#FB8C00", IconGlyph.KeyboardMouse),
        ["Text"] = new("category-text", "#00897B", IconGlyph.Text),
        ["DateAndTime"] = new("category-date-time", "#F4511E", IconGlyph.CalendarClock),
        ["Date & Time"] = new("category-date-time", "#F4511E", IconGlyph.CalendarClock),
    };

    private static readonly ActionIconDescriptor FallbackDescriptor = new("action-fallback", "#78909C", IconGlyph.Gear);
    private static readonly Dictionary<string, DrawingImage> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<Type> RegisteredActionTypes => ActionDescriptors.Keys;

    public static ActionIconDescriptor GetDescriptor(object? value)
    {
        return value switch
        {
            ActionTemplate template => GetDescriptor(template.ActionType),
            ActionCategory category => GetCategoryDescriptor(category.Name),
            Type type => GetDescriptor(type),
            null => FallbackDescriptor,
            _ => GetDescriptor(value.GetType())
        };
    }

    public static ActionIconDescriptor GetDescriptor(Type? actionType)
    {
        if (actionType == null)
        {
            return FallbackDescriptor;
        }

        if (ActionDescriptors.TryGetValue(actionType, out var descriptor))
        {
            return descriptor;
        }

        if (actionType.IsGenericType && actionType.GetGenericTypeDefinition() == typeof(SetVariableAction<>))
        {
            return ActionDescriptors[typeof(SetVariableAction<object>)];
        }

        return FallbackDescriptor;
    }

    public static ActionIconDescriptor GetCategoryDescriptor(string? categoryName)
    {
        return categoryName != null && CategoryDescriptors.TryGetValue(categoryName, out var descriptor)
            ? descriptor
            : FallbackDescriptor;
    }

    public static ImageSource GetImage(object? value)
    {
        var descriptor = GetDescriptor(value);
        if (!Cache.TryGetValue(descriptor.Key, out var image))
        {
            image = ActionIconFactory.Create(descriptor);
            image.Freeze();
            Cache[descriptor.Key] = image;
        }

        return image;
    }
}

public sealed record ActionIconDescriptor(string Key, string AccentColor, IconGlyph Glyph);

public enum IconGlyph
{
    Add,
    Block,
    Branch,
    CalendarAdd,
    CalendarClock,
    CalendarText,
    Camera,
    Checkbox,
    Clipboard,
    Clock,
    ComboBox,
    CounterLoop,
    CursorClick,
    Desktop,
    ElementSearch,
    FileClock,
    FileCopy,
    FileDelete,
    FileEdit,
    FileMove,
    FileRead,
    FileSearch,
    FileWrite,
    Flow,
    Focus,
    Folder,
    FolderAdd,
    FolderClean,
    FolderDelete,
    FolderPath,
    FolderSearch,
    Gear,
    Keyboard,
    KeyboardMouse,
    KeyDown,
    KeyUp,
    Link,
    Loop,
    Message,
    MouseClick,
    MouseDoubleClick,
    MouseMove,
    Placeholder,
    PowerShell,
    PowerShellFile,
    ProcessSearch,
    Python,
    Radio,
    Replace,
    Rocket,
    Script,
    Sound,
    Stop,
    Substring,
    Tabs,
    Target,
    TargetClock,
    Terminal,
    Text,
    TextCase,
    TextSearch,
    Variable
}

internal static class ActionIconFactory
{
    public static DrawingImage Create(ActionIconDescriptor descriptor)
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(WpfBrushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 64, 64))));
        group.Children.Add(new GeometryDrawing(BrushFrom("#22000000"), null, new RectangleGeometry(new Rect(8, 9, 48, 48), 14, 14)));
        group.Children.Add(new GeometryDrawing(BrushFrom(descriptor.AccentColor), null, new RectangleGeometry(new Rect(6, 6, 48, 48), 14, 14)));

        DrawGlyph(group, descriptor.Glyph);
        return new DrawingImage(group);
    }

    private static void DrawGlyph(DrawingGroup group, IconGlyph glyph)
    {
        switch (glyph)
        {
            case IconGlyph.Branch:
                Stroke(group, "#FFFFFF", 5, "M22,18 L22,45 M22,28 L42,28 M42,28 L42,18 M42,28 L42,43");
                Circle(group, "#FFFFFF", 22, 18, 5);
                Circle(group, "#FFFFFF", 42, 18, 5);
                Circle(group, "#FFFFFF", 42, 43, 5);
                break;
            case IconGlyph.Loop:
                Stroke(group, "#FFFFFF", 5, "M20,24 C25,14 43,17 45,30 M45,30 L39,24 M45,30 L50,23 M44,40 C36,50 18,47 18,34 M18,34 L13,41 M18,34 L24,40");
                break;
            case IconGlyph.CounterLoop:
                Text(group, "1", 18, 17, 19);
                Text(group, "N", 34, 17, 19);
                Stroke(group, "#FFFFFF", 4, "M19,43 L44,43 M44,43 L38,37 M44,43 L38,49");
                break;
            case IconGlyph.Variable:
                Text(group, "x", 17, 15, 24);
                Text(group, "=", 34, 17, 21);
                break;
            case IconGlyph.Link:
                Stroke(group, "#FFFFFF", 5, "M24,38 L18,38 C12,38 12,26 18,26 L28,26 M36,26 L46,26 C52,26 52,38 46,38 L40,38 M25,32 L39,32");
                break;
            case IconGlyph.Rocket:
                Fill(group, "#FFFFFF", "M31,13 C40,16 47,24 50,34 L41,31 L34,41 L31,50 C21,47 13,40 10,31 L19,28 L29,21 Z");
                Circle(group, "#1E88E5", 36, 25, 4);
                Fill(group, "#FFD54F", "M21,41 L15,49 L25,45 Z");
                break;
            case IconGlyph.Clock:
                Circle(group, "#FFFFFF", 32, 32, 18);
                Circle(group, "#1E88E5", 32, 32, 13);
                Stroke(group, "#FFFFFF", 4, "M32,23 L32,33 L40,38");
                break;
            case IconGlyph.Message:
                Fill(group, "#FFFFFF", "M15,18 L49,18 L49,40 L32,40 L23,48 L24,40 L15,40 Z");
                Stroke(group, "#1E88E5", 3, "M22,26 L42,26 M22,33 L38,33");
                break;
            case IconGlyph.ProcessSearch:
            case IconGlyph.TextSearch:
            case IconGlyph.ElementSearch:
            case IconGlyph.FileSearch:
            case IconGlyph.FolderSearch:
                DrawSearch(group, glyph);
                break;
            case IconGlyph.Stop:
                Fill(group, "#FFFFFF", "M22,16 L42,16 L48,22 L48,42 L42,48 L22,48 L16,42 L16,22 Z");
                Stroke(group, "#D84315", 4, "M24,24 L40,40 M40,24 L24,40");
                break;
            case IconGlyph.Camera:
                Fill(group, "#FFFFFF", "M14,24 L24,24 L27,19 L38,19 L41,24 L50,24 L50,45 L14,45 Z");
                Circle(group, "#1E88E5", 32, 34, 8);
                Circle(group, "#FFFFFF", 32, 34, 4);
                break;
            case IconGlyph.Clipboard:
                Fill(group, "#FFFFFF", "M20,17 L44,17 L44,49 L20,49 Z");
                Fill(group, "#1E88E5", "M26,13 L38,13 L40,20 L24,20 Z");
                Stroke(group, "#1E88E5", 3, "M25,28 L39,28 M25,35 L39,35 M25,42 L35,42");
                break;
            case IconGlyph.Sound:
                Fill(group, "#FFFFFF", "M15,29 L24,29 L35,20 L35,44 L24,35 L15,35 Z");
                Stroke(group, "#FFFFFF", 4, "M40,25 C44,29 44,35 40,39 M45,20 C53,27 53,37 45,44");
                break;
            case IconGlyph.TargetClock:
                DrawTarget(group);
                Stroke(group, "#FFFFFF", 3, "M40,35 L40,44 L47,48");
                break;
            case IconGlyph.CursorClick:
                Fill(group, "#FFFFFF", "M18,13 L45,34 L34,36 L41,49 L35,52 L28,39 L20,47 Z");
                Stroke(group, "#00ACC1", 3, "M34,36 L41,49");
                break;
            case IconGlyph.Keyboard:
                Fill(group, "#FFFFFF", "M13,23 L51,23 L51,44 L13,44 Z");
                Stroke(group, "#FB8C00", 3, "M18,29 L22,29 M27,29 L31,29 M36,29 L40,29 M45,29 L48,29 M18,36 L24,36 M29,36 L45,36");
                break;
            case IconGlyph.Focus:
            case IconGlyph.Target:
                DrawTarget(group);
                break;
            case IconGlyph.Radio:
                Circle(group, "#FFFFFF", 32, 32, 17);
                Circle(group, "#00ACC1", 32, 32, 8);
                break;
            case IconGlyph.Checkbox:
                Fill(group, "#FFFFFF", "M17,17 L47,17 L47,47 L17,47 Z");
                Stroke(group, "#00ACC1", 5, "M23,32 L30,39 L43,25");
                break;
            case IconGlyph.ComboBox:
                Fill(group, "#FFFFFF", "M15,19 L49,19 L49,43 L15,43 Z");
                Stroke(group, "#00ACC1", 3, "M22,27 L39,27 M22,35 L34,35");
                Fill(group, "#00ACC1", "M39,30 L46,30 L42.5,36 Z");
                break;
            case IconGlyph.Tabs:
                Fill(group, "#FFFFFF", "M13,22 L25,22 L28,18 L39,18 L42,22 L51,22 L51,46 L13,46 Z");
                Stroke(group, "#00ACC1", 3, "M17,29 L47,29");
                break;
            case IconGlyph.Terminal:
                Fill(group, "#FFFFFF", "M13,19 L51,19 L51,45 L13,45 Z");
                Stroke(group, "#7E57C2", 4, "M20,27 L27,32 L20,37 M31,38 L43,38");
                break;
            case IconGlyph.PowerShell:
            case IconGlyph.PowerShellFile:
                Fill(group, "#FFFFFF", "M14,19 L50,19 L50,45 L14,45 Z");
                Stroke(group, "#7E57C2", 4, "M22,27 L31,32 L22,37 M34,38 L43,38");
                if (glyph == IconGlyph.PowerShellFile) Fold(group, "#7E57C2");
                break;
            case IconGlyph.Script:
                DrawDocument(group, "#7E57C2");
                Stroke(group, "#7E57C2", 3, "M23,28 L41,28 M23,35 L41,35 M23,42 L34,42");
                break;
            case IconGlyph.Python:
                Text(group, "Py", 18, 19, 19);
                break;
            case IconGlyph.Folder:
            case IconGlyph.FolderAdd:
            case IconGlyph.FolderClean:
            case IconGlyph.FolderDelete:
            case IconGlyph.FolderPath:
                DrawFolder(group, "#43A047");
                DrawFolderOverlay(group, glyph);
                break;
            case IconGlyph.FileClock:
            case IconGlyph.FileCopy:
            case IconGlyph.FileDelete:
            case IconGlyph.FileEdit:
            case IconGlyph.FileMove:
            case IconGlyph.FileRead:
            case IconGlyph.FileWrite:
                DrawDocument(group, glyph == IconGlyph.FileDelete ? "#D84315" : "#43A047");
                DrawFileOverlay(group, glyph);
                break;
            case IconGlyph.MouseMove:
            case IconGlyph.MouseClick:
            case IconGlyph.MouseDoubleClick:
                Fill(group, "#FFFFFF", "M28,14 C40,14 46,24 46,36 C46,46 40,52 32,52 C24,52 18,46 18,36 C18,24 20,14 28,14 Z");
                Stroke(group, "#FB8C00", 3, "M32,15 L32,27");
                if (glyph == IconGlyph.MouseMove) Stroke(group, "#FFFFFF", 4, "M14,18 L22,12 M14,18 L22,24 M50,46 L42,40 M50,46 L42,52");
                if (glyph == IconGlyph.MouseClick) Stroke(group, "#FFFFFF", 4, "M43,15 L50,8 M47,25 L56,25 M40,10 L40,4");
                if (glyph == IconGlyph.MouseDoubleClick) Stroke(group, "#FFFFFF", 4, "M43,15 L50,8 M47,25 L56,25 M13,15 L20,8 M17,25 L8,25");
                break;
            case IconGlyph.KeyDown:
                Fill(group, "#FFFFFF", "M16,18 L48,18 L48,45 L16,45 Z");
                Stroke(group, "#FB8C00", 5, "M32,24 L32,39 M24,32 L32,40 L40,32");
                break;
            case IconGlyph.KeyUp:
                Fill(group, "#FFFFFF", "M16,18 L48,18 L48,45 L16,45 Z");
                Stroke(group, "#FB8C00", 5, "M32,40 L32,25 M24,32 L32,24 L40,32");
                break;
            case IconGlyph.KeyboardMouse:
                Fill(group, "#FFFFFF", "M12,29 L41,29 L41,45 L12,45 Z");
                Fill(group, "#FFFFFF", "M44,17 C51,17 55,23 55,31 C55,38 51,43 46,43 C41,43 38,38 38,31 C38,23 39,17 44,17 Z");
                Stroke(group, "#FB8C00", 2, "M46,18 L46,27");
                break;
            case IconGlyph.Text:
            case IconGlyph.TextCase:
                Text(group, "Aa", 15, 18, 21);
                break;
            case IconGlyph.Replace:
                Text(group, "A", 16, 17, 18);
                Text(group, "B", 38, 29, 18);
                Stroke(group, "#FFFFFF", 4, "M22,42 L38,42 M38,42 L33,37 M38,42 L33,47");
                break;
            case IconGlyph.Substring:
                Text(group, "abc", 11, 20, 16);
                Stroke(group, "#FFFFFF", 4, "M23,45 L41,20");
                break;
            case IconGlyph.CalendarAdd:
            case IconGlyph.CalendarClock:
            case IconGlyph.CalendarText:
                DrawCalendar(group);
                DrawCalendarOverlay(group, glyph);
                break;
            case IconGlyph.Desktop:
                Fill(group, "#FFFFFF", "M14,18 L50,18 L50,40 L14,40 Z M26,44 L38,44 L40,50 L24,50 Z");
                break;
            case IconGlyph.Block:
                Fill(group, "#FFFFFF", "M16,18 L48,18 L48,46 L16,46 Z");
                Stroke(group, "#78909C", 3, "M22,26 L42,26 M22,34 L42,34 M22,42 L36,42");
                break;
            case IconGlyph.Flow:
                Stroke(group, "#FFFFFF", 5, "M18,20 L32,20 L32,32 L46,32 M32,32 L32,45");
                Circle(group, "#FFFFFF", 18, 20, 5);
                Circle(group, "#FFFFFF", 46, 32, 5);
                Circle(group, "#FFFFFF", 32, 45, 5);
                break;
            case IconGlyph.Add:
            case IconGlyph.Placeholder:
            case IconGlyph.Gear:
            default:
                Circle(group, "#FFFFFF", 32, 32, 15);
                Stroke(group, "#78909C", 4, "M32,20 L32,44 M20,32 L44,32");
                break;
        }
    }

    private static void DrawSearch(DrawingGroup group, IconGlyph glyph)
    {
        if (glyph is IconGlyph.FileSearch)
        {
            DrawDocument(group, "#43A047");
        }
        else if (glyph is IconGlyph.FolderSearch)
        {
            DrawFolder(group, "#43A047");
        }
        else if (glyph is IconGlyph.TextSearch)
        {
            Text(group, "T", 18, 18, 20);
        }
        else if (glyph is IconGlyph.ElementSearch)
        {
            DrawTarget(group);
        }
        else
        {
            Fill(group, "#FFFFFF", "M17,20 L47,20 L47,43 L17,43 Z");
            Stroke(group, "#1E88E5", 3, "M23,28 L41,28 M23,35 L36,35");
        }

        Circle(group, "#FFFFFF", 40, 40, 8);
        Stroke(group, "#FFFFFF", 5, "M46,46 L53,53");
        Circle(group, glyph is IconGlyph.FileSearch or IconGlyph.FolderSearch ? "#43A047" : glyph is IconGlyph.ElementSearch or IconGlyph.TextSearch ? "#00ACC1" : "#1E88E5", 40, 40, 5);
    }

    private static void DrawTarget(DrawingGroup group)
    {
        Circle(group, "#FFFFFF", 32, 32, 18);
        Circle(group, "#00ACC1", 32, 32, 12);
        Circle(group, "#FFFFFF", 32, 32, 5);
        Stroke(group, "#FFFFFF", 3, "M32,11 L32,20 M32,44 L32,53 M11,32 L20,32 M44,32 L53,32");
    }

    private static void DrawDocument(DrawingGroup group, string accent)
    {
        Fill(group, "#FFFFFF", "M20,13 L39,13 L48,22 L48,51 L20,51 Z");
        Fill(group, accent, "M39,13 L48,22 L39,22 Z");
    }

    private static void DrawFolder(DrawingGroup group, string accent)
    {
        Fill(group, "#FFFFFF", "M12,22 L26,22 L30,17 L43,17 L47,22 L52,22 L52,47 L12,47 Z");
        Fill(group, BrushFrom(accent).Color.ToString(), "M12,26 L52,26 L52,47 L12,47 Z");
        Fill(group, "#FFFFFF", "M17,31 L47,31 L47,42 L17,42 Z");
    }

    private static void DrawFolderOverlay(DrawingGroup group, IconGlyph glyph)
    {
        switch (glyph)
        {
            case IconGlyph.FolderAdd:
                Stroke(group, "#FFFFFF", 4, "M32,30 L32,44 M25,37 L39,37");
                break;
            case IconGlyph.FolderClean:
                Stroke(group, "#FFFFFF", 4, "M25,33 L39,33 M28,39 L36,39");
                break;
            case IconGlyph.FolderDelete:
                Stroke(group, "#FFFFFF", 4, "M25,31 L39,45 M39,31 L25,45");
                break;
            case IconGlyph.FolderPath:
                Stroke(group, "#FFFFFF", 4, "M20,38 L30,38 L34,34 L45,34");
                break;
        }
    }

    private static void DrawFileOverlay(DrawingGroup group, IconGlyph glyph)
    {
        switch (glyph)
        {
            case IconGlyph.FileClock:
                Circle(group, "#43A047", 35, 37, 10);
                Stroke(group, "#FFFFFF", 3, "M35,31 L35,38 L40,41");
                break;
            case IconGlyph.FileCopy:
                Stroke(group, "#43A047", 3, "M25,29 L39,29 M25,36 L39,36 M25,43 L34,43");
                break;
            case IconGlyph.FileDelete:
                Stroke(group, "#D84315", 4, "M27,30 L40,43 M40,30 L27,43");
                break;
            case IconGlyph.FileEdit:
                Stroke(group, "#43A047", 4, "M27,42 L42,27 M38,23 L46,31");
                break;
            case IconGlyph.FileMove:
                Stroke(group, "#43A047", 4, "M25,37 L42,37 M42,37 L36,31 M42,37 L36,43");
                break;
            case IconGlyph.FileRead:
                Stroke(group, "#43A047", 3, "M25,29 L40,29 M25,36 L40,36 M25,43 L35,43");
                break;
            case IconGlyph.FileWrite:
                Stroke(group, "#43A047", 4, "M25,43 L39,29 M36,26 L43,33");
                break;
        }
    }

    private static void DrawCalendar(DrawingGroup group)
    {
        Fill(group, "#FFFFFF", "M16,18 L48,18 L48,48 L16,48 Z");
        Fill(group, "#F4511E", "M16,18 L48,18 L48,27 L16,27 Z");
        Stroke(group, "#FFFFFF", 4, "M24,14 L24,22 M40,14 L40,22");
    }

    private static void DrawCalendarOverlay(DrawingGroup group, IconGlyph glyph)
    {
        switch (glyph)
        {
            case IconGlyph.CalendarAdd:
                Stroke(group, "#F4511E", 4, "M32,31 L32,43 M26,37 L38,37");
                break;
            case IconGlyph.CalendarText:
                Stroke(group, "#F4511E", 3, "M23,33 L41,33 M23,40 L36,40");
                break;
            default:
                Circle(group, "#F4511E", 34, 38, 8);
                Stroke(group, "#FFFFFF", 3, "M34,33 L34,39 L39,41");
                break;
        }
    }

    private static void Fold(DrawingGroup group, string accent)
    {
        Fill(group, accent, "M39,19 L50,30 L39,30 Z");
    }

    private static void Fill(DrawingGroup group, string color, string path)
    {
        group.Children.Add(new GeometryDrawing(BrushFrom(color), null, Geometry.Parse(path)));
    }

    private static void Fill(DrawingGroup group, WpfBrush brush, string path)
    {
        group.Children.Add(new GeometryDrawing(brush, null, Geometry.Parse(path)));
    }

    private static void Stroke(DrawingGroup group, string color, double thickness, string path)
    {
        var pen = new WpfPen(BrushFrom(color), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        group.Children.Add(new GeometryDrawing(null, pen, Geometry.Parse(path)));
    }

    private static void Circle(DrawingGroup group, string color, double x, double y, double radius)
    {
        group.Children.Add(new GeometryDrawing(BrushFrom(color), null, new EllipseGeometry(new WpfPoint(x, y), radius, radius)));
    }

    private static void Text(DrawingGroup group, string text, double x, double y, double size)
    {
        var formattedText = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            WpfFlowDirection.LeftToRight,
            new Typeface(new WpfFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size,
            WpfBrushes.White,
            1.25);

        group.Children.Add(new GeometryDrawing(WpfBrushes.White, null, formattedText.BuildGeometry(new WpfPoint(x, y))));
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        var brush = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
