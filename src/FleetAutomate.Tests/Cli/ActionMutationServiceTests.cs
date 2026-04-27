using FleetAutomate.Application.Commanding;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.DateAndTime;
using FleetAutomate.Model.Actions.FileSystem;
using FleetAutomate.Model.Actions.Scripts;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.Text;
using FleetAutomate.Model.Actions.MouseAndKeyboard;
using FleetAutomate.Model.Actions.UIAutomation;

namespace FleetAutomate.Tests.Cli;

public sealed class ActionMutationServiceTests
{
    [Fact]
    public void CreateAction_SupportsWaitDurationAction()
    {
        var service = new ActionMutationService();

        var action = service.CreateAction("WaitDurationAction");

        Assert.IsType<WaitDurationAction>(action);
    }

    public static TheoryData<string, Type> ImplementedPlaceholderActionTypes => new()
    {
        { "IfFileExistsAction", typeof(IfFileExistsAction) },
        { "If File Exists", typeof(IfFileExistsAction) },
        { "IfDirectoryExistsAction", typeof(IfDirectoryExistsAction) },
        { "If Directory Exists", typeof(IfDirectoryExistsAction) },
        { "CreateDirectoryAction", typeof(CreateDirectoryAction) },
        { "Create Directory", typeof(CreateDirectoryAction) },
        { "ClearDirectoryAction", typeof(ClearDirectoryAction) },
        { "Clear Directory", typeof(ClearDirectoryAction) },
        { "DeleteDirectoryAction", typeof(DeleteDirectoryAction) },
        { "Delete Directory", typeof(DeleteDirectoryAction) },
        { "WaitForFileAction", typeof(WaitForFileAction) },
        { "Wait for File", typeof(WaitForFileAction) },
        { "CopyFileAction", typeof(CopyFileAction) },
        { "Copy File", typeof(CopyFileAction) },
        { "MoveFileAction", typeof(MoveFileAction) },
        { "Move File", typeof(MoveFileAction) },
        { "DeleteFileAction", typeof(DeleteFileAction) },
        { "Delete File", typeof(DeleteFileAction) },
        { "RenameFileAction", typeof(RenameFileAction) },
        { "Rename File", typeof(RenameFileAction) },
        { "ReadTextFromFileAction", typeof(ReadTextFromFileAction) },
        { "Read Text from File", typeof(ReadTextFromFileAction) },
        { "WriteTextToFileAction", typeof(WriteTextToFileAction) },
        { "Write Text to File", typeof(WriteTextToFileAction) },
        { "GetDirectoryOfFileAction", typeof(GetDirectoryOfFileAction) },
        { "Get Directory of File", typeof(GetDirectoryOfFileAction) },
        { "RunCommandAction", typeof(RunCommandAction) },
        { "Run CMD", typeof(RunCommandAction) },
        { "RunPowerShellCommandAction", typeof(RunPowerShellCommandAction) },
        { "Run PowerShell Command", typeof(RunPowerShellCommandAction) },
        { "RunBatchScriptAction", typeof(RunBatchScriptAction) },
        { "Run Batch Script", typeof(RunBatchScriptAction) },
        { "RunPowerShellScriptAction", typeof(RunPowerShellScriptAction) },
        { "Run PowerShell Script", typeof(RunPowerShellScriptAction) },
        { "RunPythonScriptAction", typeof(RunPythonScriptAction) },
        { "Run Python Script", typeof(RunPythonScriptAction) },
        { "IfProcessExistsAction", typeof(IfProcessExistsAction) },
        { "If Process Exists", typeof(IfProcessExistsAction) },
        { "KillProcessAction", typeof(KillProcessAction) },
        { "Kill Process", typeof(KillProcessAction) },
        { "GetScreenshotAction", typeof(GetScreenshotAction) },
        { "Get Screenshot", typeof(GetScreenshotAction) },
        { "SetClipboardAction", typeof(SetClipboardAction) },
        { "Set Clipboard", typeof(SetClipboardAction) },
        { "PlaySoundAction", typeof(PlaySoundAction) },
        { "Play Sound", typeof(PlaySoundAction) },
        { "ChangeTextCaseAction", typeof(ChangeTextCaseAction) },
        { "Change Text Case", typeof(ChangeTextCaseAction) },
        { "ReplaceTextAction", typeof(ReplaceTextAction) },
        { "Replace Text", typeof(ReplaceTextAction) },
        { "SubstringAction", typeof(SubstringAction) },
        { "Substring", typeof(SubstringAction) },
        { "GetCurrentDateTimeAction", typeof(GetCurrentDateTimeAction) },
        { "Get Current Date/Time", typeof(GetCurrentDateTimeAction) },
        { "FormatDateTimeAction", typeof(FormatDateTimeAction) },
        { "Format Date/Time", typeof(FormatDateTimeAction) },
        { "AddDateTimeAction", typeof(AddDateTimeAction) },
        { "Add to Date/Time", typeof(AddDateTimeAction) },
        { "MoveMouseToAction", typeof(MoveMouseToAction) },
        { "Move Mouse To", typeof(MoveMouseToAction) },
        { "MouseSingleClickAction", typeof(MouseSingleClickAction) },
        { "Mouse Single Click", typeof(MouseSingleClickAction) },
        { "MouseDoubleClickAction", typeof(MouseDoubleClickAction) },
        { "Mouse Double Click", typeof(MouseDoubleClickAction) },
        { "SendKeysAction", typeof(SendKeysAction) },
        { "Send Keys", typeof(SendKeysAction) },
        { "SendKeyDownAction", typeof(SendKeyDownAction) },
        { "Send Key Down", typeof(SendKeyDownAction) },
        { "SendKeyUpAction", typeof(SendKeyUpAction) },
        { "Send Key Up", typeof(SendKeyUpAction) },
        { "IfWindowContainsElementAction", typeof(IfWindowContainsElementAction) },
        { "If Window Contains Element", typeof(IfWindowContainsElementAction) },
        { "SetFocusOnElementAction", typeof(SetFocusOnElementAction) },
        { "Set Focus on Element", typeof(SetFocusOnElementAction) },
        { "SelectRadioButtonAction", typeof(SelectRadioButtonAction) },
        { "Select Radio Button", typeof(SelectRadioButtonAction) },
        { "SetCheckBoxStateAction", typeof(SetCheckBoxStateAction) },
        { "Set CheckBox State", typeof(SetCheckBoxStateAction) },
        { "SelectComboBoxItemAction", typeof(SelectComboBoxItemAction) },
        { "Select Item in ComboBox", typeof(SelectComboBoxItemAction) },
        { "SelectTabItemAction", typeof(SelectTabItemAction) },
        { "Select Tab Item", typeof(SelectTabItemAction) }
    };

    [Theory]
    [MemberData(nameof(ImplementedPlaceholderActionTypes))]
    public void CreateAction_SupportsImplementedPlaceholderActions(string actionType, Type expectedType)
    {
        var service = new ActionMutationService();

        IAction action = service.CreateAction(actionType);

        Assert.IsType(expectedType, action);
    }
}
