using FleetAutomate.Application.ActionConfiguration;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.DateAndTime;
using FleetAutomate.Model.Actions.FileSystem;
using FleetAutomate.Model.Actions.MouseAndKeyboard;
using FleetAutomate.Model.Actions.Scripts;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.Text;
using FleetAutomate.Model.Actions.UIAutomation;

namespace FleetAutomate.Tests.View;

public sealed class ActionConfigurationSchemaTests
{
    public static TheoryData<Type, string[]> ConfigurableActions => new()
    {
        { typeof(IfProcessExistsAction), [nameof(IfProcessExistsAction.ProcessName)] },
        { typeof(KillProcessAction), [nameof(KillProcessAction.ProcessName), nameof(KillProcessAction.SucceedIfNotFound)] },
        { typeof(GetScreenshotAction), [nameof(GetScreenshotAction.FilePath)] },
        { typeof(SetClipboardAction), [nameof(SetClipboardAction.Text)] },
        { typeof(PlaySoundAction), [nameof(PlaySoundAction.FilePath)] },
        { typeof(IfFileExistsAction), [nameof(IfFileExistsAction.FilePath)] },
        { typeof(IfDirectoryExistsAction), [nameof(IfDirectoryExistsAction.DirectoryPath)] },
        { typeof(CreateDirectoryAction), [nameof(CreateDirectoryAction.DirectoryPath)] },
        { typeof(ClearDirectoryAction), [nameof(ClearDirectoryAction.DirectoryPath)] },
        { typeof(DeleteDirectoryAction), [nameof(DeleteDirectoryAction.DirectoryPath), nameof(DeleteDirectoryAction.Recursive)] },
        { typeof(WaitForFileAction), [nameof(WaitForFileAction.FilePath), nameof(WaitForFileAction.TimeoutMilliseconds), nameof(WaitForFileAction.PollingIntervalMilliseconds)] },
        { typeof(CopyFileAction), [nameof(CopyFileAction.SourcePath), nameof(CopyFileAction.DestinationPath), nameof(CopyFileAction.Overwrite)] },
        { typeof(MoveFileAction), [nameof(MoveFileAction.SourcePath), nameof(MoveFileAction.DestinationPath), nameof(MoveFileAction.Overwrite)] },
        { typeof(DeleteFileAction), [nameof(DeleteFileAction.FilePath)] },
        { typeof(RenameFileAction), [nameof(RenameFileAction.FilePath), nameof(RenameFileAction.NewName), nameof(RenameFileAction.Overwrite)] },
        { typeof(ReadTextFromFileAction), [nameof(ReadTextFromFileAction.FilePath)] },
        { typeof(WriteTextToFileAction), [nameof(WriteTextToFileAction.FilePath), nameof(WriteTextToFileAction.Text), nameof(WriteTextToFileAction.Append)] },
        { typeof(GetDirectoryOfFileAction), [nameof(GetDirectoryOfFileAction.FilePath)] },
        { typeof(RunCommandAction), [nameof(RunCommandAction.Command), nameof(RunCommandAction.Arguments), nameof(RunCommandAction.WorkingDirectory), nameof(RunCommandAction.TimeoutMilliseconds)] },
        { typeof(RunPowerShellCommandAction), [nameof(RunPowerShellCommandAction.Script), nameof(RunPowerShellCommandAction.WorkingDirectory), nameof(RunPowerShellCommandAction.TimeoutMilliseconds)] },
        { typeof(RunBatchScriptAction), [nameof(RunBatchScriptAction.ScriptPath), nameof(RunBatchScriptAction.WorkingDirectory), nameof(RunBatchScriptAction.TimeoutMilliseconds)] },
        { typeof(RunPowerShellScriptAction), [nameof(RunPowerShellScriptAction.ScriptPath), nameof(RunPowerShellScriptAction.WorkingDirectory), nameof(RunPowerShellScriptAction.TimeoutMilliseconds)] },
        { typeof(RunPythonScriptAction), [nameof(RunPythonScriptAction.ScriptPath), nameof(RunPythonScriptAction.PythonExecutable), nameof(RunPythonScriptAction.WorkingDirectory), nameof(RunPythonScriptAction.TimeoutMilliseconds)] },
        { typeof(ChangeTextCaseAction), [nameof(ChangeTextCaseAction.Text), nameof(ChangeTextCaseAction.Case)] },
        { typeof(ReplaceTextAction), [nameof(ReplaceTextAction.Text), nameof(ReplaceTextAction.SearchText), nameof(ReplaceTextAction.ReplacementText), nameof(ReplaceTextAction.IgnoreCase)] },
        { typeof(SubstringAction), [nameof(SubstringAction.Text), nameof(SubstringAction.StartIndex), nameof(SubstringAction.Length)] },
        { typeof(FormatDateTimeAction), [nameof(FormatDateTimeAction.Value), nameof(FormatDateTimeAction.Format), nameof(FormatDateTimeAction.CultureName)] },
        { typeof(AddDateTimeAction), [nameof(AddDateTimeAction.Value), nameof(AddDateTimeAction.Amount), nameof(AddDateTimeAction.Unit)] },
        { typeof(MoveMouseToAction), [nameof(MoveMouseToAction.X), nameof(MoveMouseToAction.Y)] },
        { typeof(MouseSingleClickAction), [nameof(MouseSingleClickAction.X), nameof(MouseSingleClickAction.Y), nameof(MouseSingleClickAction.Button)] },
        { typeof(MouseDoubleClickAction), [nameof(MouseDoubleClickAction.X), nameof(MouseDoubleClickAction.Y), nameof(MouseDoubleClickAction.Button)] },
        { typeof(SendKeysAction), [nameof(SendKeysAction.Text), nameof(SendKeysAction.Wait)] },
        { typeof(SendKeyDownAction), [nameof(SendKeyDownAction.Key)] },
        { typeof(SendKeyUpAction), [nameof(SendKeyUpAction.Key)] },
        { typeof(IfWindowContainsElementAction), [nameof(UiElementActionBase.IdentifierType), nameof(UiElementActionBase.ElementIdentifier), nameof(UiElementActionBase.SearchScope), nameof(UiElementActionBase.AddToGlobalDictionary), nameof(UiElementActionBase.GlobalDictionaryKey)] },
        { typeof(SetFocusOnElementAction), [nameof(UiElementActionBase.IdentifierType), nameof(UiElementActionBase.ElementIdentifier), nameof(UiElementActionBase.SearchScope), nameof(UiElementActionBase.AddToGlobalDictionary), nameof(UiElementActionBase.GlobalDictionaryKey)] },
        { typeof(SelectRadioButtonAction), [nameof(UiElementActionBase.IdentifierType), nameof(UiElementActionBase.ElementIdentifier), nameof(UiElementActionBase.SearchScope), nameof(UiElementActionBase.AddToGlobalDictionary), nameof(UiElementActionBase.GlobalDictionaryKey)] },
        { typeof(SetCheckBoxStateAction), [nameof(UiElementActionBase.IdentifierType), nameof(UiElementActionBase.ElementIdentifier), nameof(SetCheckBoxStateAction.IsChecked)] },
        { typeof(SelectComboBoxItemAction), [nameof(UiElementActionBase.IdentifierType), nameof(UiElementActionBase.ElementIdentifier), nameof(SelectComboBoxItemAction.ItemText)] },
        { typeof(SelectTabItemAction), [nameof(UiElementActionBase.IdentifierType), nameof(UiElementActionBase.ElementIdentifier)] }
    };

    [Theory]
    [MemberData(nameof(ConfigurableActions))]
    public void SchemaProvider_ReturnsExpectedFieldsForConfigurableActions(Type actionType, string[] expectedProperties)
    {
        var schema = ActionConfigurationSchemaProvider.GetSchema(actionType);

        Assert.NotNull(schema);
        Assert.All(expectedProperties, propertyName =>
            Assert.Contains(schema.Fields, field => field.PropertyName == propertyName));
    }

    [Fact]
    public void SchemaProvider_DoesNotRequireConfigurationForNoInputAction()
    {
        Assert.Null(ActionConfigurationSchemaProvider.GetSchema(typeof(GetCurrentDateTimeAction)));
    }

    [Fact]
    public void ApplyValues_SetsTypedPropertiesWithoutExecutingAction()
    {
        IAction action = new MouseSingleClickAction();

        ActionConfigurationValueApplier.ApplyValues(action, [
            new ActionConfigurationValue(nameof(MouseSingleClickAction.X), 120),
            new ActionConfigurationValue(nameof(MouseSingleClickAction.Y), 240),
            new ActionConfigurationValue(nameof(MouseSingleClickAction.Button), MouseButton.Right)
        ]);

        var click = Assert.IsType<MouseSingleClickAction>(action);
        Assert.Equal(120, click.X);
        Assert.Equal(240, click.Y);
        Assert.Equal(MouseButton.Right, click.Button);
    }
}
