using System;
using System.Collections.Generic;
using Canvas.TestRunner.Model.Actions;
using FleetAutomate.Model;
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
using Wpf.Ui.Controls;

namespace FleetAutomate.Icons;

public static class ActionSymbolRegistry
{
    public const SymbolRegular FallbackSymbol = SymbolRegular.Document20;

    private static readonly Dictionary<Type, SymbolRegular> ActionSymbols = new()
    {
        [typeof(IfAction)] = SymbolRegular.Filter20,
        [typeof(WhileLoopAction)] = SymbolRegular.ArrowRight20,
        [typeof(ForLoopAction)] = SymbolRegular.ArrowRight20,
        [typeof(SetVariableAction<object>)] = SymbolRegular.DocumentAdd20,
        [typeof(SubFlowAction)] = SymbolRegular.Link20,

        [typeof(LaunchApplicationAction)] = SymbolRegular.Rocket20,
        [typeof(WaitDurationAction)] = SymbolRegular.Clock20,
        [typeof(LogAction)] = SymbolRegular.DocumentText20,
        [typeof(IfProcessExistsAction)] = SymbolRegular.Search20,
        [typeof(KillProcessAction)] = SymbolRegular.Stop20,
        [typeof(GetScreenshotAction)] = SymbolRegular.Copy20,
        [typeof(SetClipboardAction)] = SymbolRegular.Copy20,
        [typeof(PlaySoundAction)] = SymbolRegular.Play20,

        [typeof(WaitForElementAction)] = SymbolRegular.Timer20,
        [typeof(ClickElementAction)] = SymbolRegular.CursorClick20,
        [typeof(IfWindowContainsTextAction)] = SymbolRegular.Search20,
        [typeof(IfWindowContainsElementAction)] = SymbolRegular.Target20,
        [typeof(SetTextAction)] = SymbolRegular.Keyboard20,
        [typeof(SetFocusOnElementAction)] = SymbolRegular.Target20,
        [typeof(SelectRadioButtonAction)] = SymbolRegular.Target20,
        [typeof(SetCheckBoxStateAction)] = SymbolRegular.Filter20,
        [typeof(SelectComboBoxItemAction)] = SymbolRegular.DocumentEdit20,
        [typeof(SelectTabItemAction)] = SymbolRegular.DocumentSplitHint20,

        [typeof(RunCommandAction)] = SymbolRegular.DocumentText20,
        [typeof(RunPowerShellCommandAction)] = SymbolRegular.DocumentText20,
        [typeof(RunBatchScriptAction)] = SymbolRegular.DocumentText20,
        [typeof(RunPowerShellScriptAction)] = SymbolRegular.DocumentText20,
        [typeof(RunPythonScriptAction)] = SymbolRegular.DocumentText20,

        [typeof(IfFileExistsAction)] = SymbolRegular.Search20,
        [typeof(IfDirectoryExistsAction)] = SymbolRegular.Search20,
        [typeof(CreateDirectoryAction)] = SymbolRegular.DocumentAdd20,
        [typeof(ClearDirectoryAction)] = SymbolRegular.Delete20,
        [typeof(DeleteDirectoryAction)] = SymbolRegular.Delete20,
        [typeof(WaitForFileAction)] = SymbolRegular.Timer20,
        [typeof(CopyFileAction)] = SymbolRegular.Copy20,
        [typeof(MoveFileAction)] = SymbolRegular.ArrowRight20,
        [typeof(DeleteFileAction)] = SymbolRegular.Delete20,
        [typeof(RenameFileAction)] = SymbolRegular.DocumentEdit20,
        [typeof(ReadTextFromFileAction)] = SymbolRegular.DocumentText20,
        [typeof(WriteTextToFileAction)] = SymbolRegular.Save20,
        [typeof(GetDirectoryOfFileAction)] = SymbolRegular.FolderOpen20,

        [typeof(MoveMouseToAction)] = SymbolRegular.Cursor20,
        [typeof(MouseSingleClickAction)] = SymbolRegular.CursorClick20,
        [typeof(MouseDoubleClickAction)] = SymbolRegular.CursorClick20,
        [typeof(SendKeysAction)] = SymbolRegular.Keyboard20,
        [typeof(SendKeyDownAction)] = SymbolRegular.ArrowDown20,
        [typeof(SendKeyUpAction)] = SymbolRegular.ArrowUp20,

        [typeof(ChangeTextCaseAction)] = SymbolRegular.DocumentText20,
        [typeof(ReplaceTextAction)] = SymbolRegular.ArrowRight20,
        [typeof(SubstringAction)] = SymbolRegular.DocumentEdit20,

        [typeof(GetCurrentDateTimeAction)] = SymbolRegular.Clock20,
        [typeof(FormatDateTimeAction)] = SymbolRegular.Clock20,
        [typeof(AddDateTimeAction)] = SymbolRegular.DocumentAdd20,

        [typeof(NotImplementedAction)] = SymbolRegular.Wrench20,
        [typeof(ActionBlock)] = SymbolRegular.Document20,
        [typeof(TestFlow)] = SymbolRegular.DocumentText20,
    };

    private static readonly Dictionary<string, SymbolRegular> CategorySymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LogicAndFlow"] = SymbolRegular.Filter20,
        ["Logic & Flow"] = SymbolRegular.Filter20,
        ["System"] = SymbolRegular.Desktop20,
        ["UIAutomation"] = SymbolRegular.Target20,
        ["UI Automation"] = SymbolRegular.Target20,
        ["Scripts"] = SymbolRegular.DocumentText20,
        ["FileSystem"] = SymbolRegular.Folder20,
        ["File System"] = SymbolRegular.Folder20,
        ["MouseAndKeyboard"] = SymbolRegular.Keyboard20,
        ["Mouse & Keyboard"] = SymbolRegular.Keyboard20,
        ["Text"] = SymbolRegular.DocumentText20,
        ["DateAndTime"] = SymbolRegular.Clock20,
        ["Date & Time"] = SymbolRegular.Clock20,
    };

    public static IReadOnlyCollection<Type> RegisteredActionTypes => ActionSymbols.Keys;

    public static SymbolRegular GetSymbol(object? value)
    {
        return value switch
        {
            ActionTemplate template => GetSymbol(template.ActionType),
            ActionCategory category => GetCategorySymbol(category.Name),
            Type type => GetSymbol(type),
            null => FallbackSymbol,
            _ => GetSymbol(value.GetType())
        };
    }

    public static SymbolRegular GetSymbol(Type? actionType)
    {
        if (actionType == null)
        {
            return FallbackSymbol;
        }

        if (ActionSymbols.TryGetValue(actionType, out var symbol))
        {
            return symbol;
        }

        if (actionType.IsGenericType && actionType.GetGenericTypeDefinition() == typeof(SetVariableAction<>))
        {
            return ActionSymbols[typeof(SetVariableAction<object>)];
        }

        return FallbackSymbol;
    }

    public static SymbolRegular GetCategorySymbol(string? categoryName)
    {
        return categoryName != null && CategorySymbols.TryGetValue(categoryName, out var symbol)
            ? symbol
            : FallbackSymbol;
    }
}
