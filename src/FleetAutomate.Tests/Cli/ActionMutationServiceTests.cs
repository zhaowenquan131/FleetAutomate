using FleetAutomate.Application.Commanding;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.FileSystem;
using FleetAutomate.Model.Actions.Scripts;
using FleetAutomate.Model.Actions.System;

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
        { "Play Sound", typeof(PlaySoundAction) }
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
