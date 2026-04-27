using FleetAutomate.Model.Actions.DateAndTime;
using FleetAutomate.Model.Actions.FileSystem;
using FleetAutomate.Model.Actions.MouseAndKeyboard;
using FleetAutomate.Model.Actions.Scripts;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.Text;
using FleetAutomate.Model.Actions.UIAutomation;

namespace FleetAutomate.Application.ActionConfiguration;

public static class ActionConfigurationSchemaProvider
{
    private static readonly Dictionary<Type, ActionConfigurationSchema> Schemas = BuildSchemas();

    public static ActionConfigurationSchema? GetSchema(Type actionType)
    {
        ArgumentNullException.ThrowIfNull(actionType);
        return Schemas.GetValueOrDefault(actionType);
    }

    public static bool HasSchema(Type actionType)
    {
        return GetSchema(actionType) != null;
    }

    private static Dictionary<Type, ActionConfigurationSchema> BuildSchemas()
    {
        var schemas = new Dictionary<Type, ActionConfigurationSchema>();

        Add<IfProcessExistsAction>(schemas, "Configure Process Check", [
            Text(nameof(IfProcessExistsAction.ProcessName), "Process name", required: true)
        ]);
        Add<KillProcessAction>(schemas, "Configure Kill Process", [
            Text(nameof(KillProcessAction.ProcessName), "Process name", required: true),
            Bool(nameof(KillProcessAction.SucceedIfNotFound), "Succeed if process is not found")
        ]);
        Add<GetScreenshotAction>(schemas, "Configure Screenshot", [
            File(nameof(GetScreenshotAction.FilePath), "Output file", required: true)
        ]);
        Add<SetClipboardAction>(schemas, "Configure Clipboard Text", [
            Multiline(nameof(SetClipboardAction.Text), "Text")
        ]);
        Add<PlaySoundAction>(schemas, "Configure Sound", [
            File(nameof(PlaySoundAction.FilePath), "Audio file", required: true)
        ]);

        Add<IfFileExistsAction>(schemas, "Configure File Check", [File(nameof(IfFileExistsAction.FilePath), "File path", required: true)]);
        Add<IfDirectoryExistsAction>(schemas, "Configure Directory Check", [Directory(nameof(IfDirectoryExistsAction.DirectoryPath), "Directory path", required: true)]);
        Add<CreateDirectoryAction>(schemas, "Configure Create Directory", [Directory(nameof(CreateDirectoryAction.DirectoryPath), "Directory path", required: true)]);
        Add<ClearDirectoryAction>(schemas, "Configure Clear Directory", [Directory(nameof(ClearDirectoryAction.DirectoryPath), "Directory path", required: true)]);
        Add<DeleteDirectoryAction>(schemas, "Configure Delete Directory", [
            Directory(nameof(DeleteDirectoryAction.DirectoryPath), "Directory path", required: true),
            Bool(nameof(DeleteDirectoryAction.Recursive), "Delete recursively")
        ]);
        Add<WaitForFileAction>(schemas, "Configure Wait for File", [
            File(nameof(WaitForFileAction.FilePath), "File path", required: true),
            Int(nameof(WaitForFileAction.TimeoutMilliseconds), "Timeout (ms)", required: true),
            Int(nameof(WaitForFileAction.PollingIntervalMilliseconds), "Polling interval (ms)", required: true)
        ]);
        Add<CopyFileAction>(schemas, "Configure Copy File", [
            File(nameof(CopyFileAction.SourcePath), "Source file", required: true),
            File(nameof(CopyFileAction.DestinationPath), "Destination file", required: true),
            Bool(nameof(CopyFileAction.Overwrite), "Overwrite destination")
        ]);
        Add<MoveFileAction>(schemas, "Configure Move File", [
            File(nameof(MoveFileAction.SourcePath), "Source file", required: true),
            File(nameof(MoveFileAction.DestinationPath), "Destination file", required: true),
            Bool(nameof(MoveFileAction.Overwrite), "Overwrite destination")
        ]);
        Add<DeleteFileAction>(schemas, "Configure Delete File", [File(nameof(DeleteFileAction.FilePath), "File path", required: true)]);
        Add<RenameFileAction>(schemas, "Configure Rename File", [
            File(nameof(RenameFileAction.FilePath), "File path", required: true),
            Text(nameof(RenameFileAction.NewName), "New file name", required: true),
            Bool(nameof(RenameFileAction.Overwrite), "Overwrite destination")
        ]);
        Add<ReadTextFromFileAction>(schemas, "Configure Read Text", [File(nameof(ReadTextFromFileAction.FilePath), "File path", required: true)]);
        Add<WriteTextToFileAction>(schemas, "Configure Write Text", [
            File(nameof(WriteTextToFileAction.FilePath), "File path", required: true),
            Multiline(nameof(WriteTextToFileAction.Text), "Text"),
            Bool(nameof(WriteTextToFileAction.Append), "Append to existing file")
        ]);
        Add<GetDirectoryOfFileAction>(schemas, "Configure Get Directory", [File(nameof(GetDirectoryOfFileAction.FilePath), "File path", required: true)]);

        Add<RunCommandAction>(schemas, "Configure Command", [
            Text(nameof(RunCommandAction.Command), "Command", required: true),
            Text(nameof(RunCommandAction.Arguments), "Arguments"),
            Directory(nameof(RunCommandAction.WorkingDirectory), "Working directory"),
            Int(nameof(RunCommandAction.TimeoutMilliseconds), "Timeout (ms)", required: true)
        ]);
        Add<RunPowerShellCommandAction>(schemas, "Configure PowerShell Command", [
            Multiline(nameof(RunPowerShellCommandAction.Script), "Script", required: true),
            Directory(nameof(RunPowerShellCommandAction.WorkingDirectory), "Working directory"),
            Int(nameof(RunPowerShellCommandAction.TimeoutMilliseconds), "Timeout (ms)", required: true)
        ]);
        Add<RunBatchScriptAction>(schemas, "Configure Batch Script", [
            File(nameof(RunBatchScriptAction.ScriptPath), "Script file", required: true),
            Directory(nameof(RunBatchScriptAction.WorkingDirectory), "Working directory"),
            Int(nameof(RunBatchScriptAction.TimeoutMilliseconds), "Timeout (ms)", required: true)
        ]);
        Add<RunPowerShellScriptAction>(schemas, "Configure PowerShell Script", [
            File(nameof(RunPowerShellScriptAction.ScriptPath), "Script file", required: true),
            Directory(nameof(RunPowerShellScriptAction.WorkingDirectory), "Working directory"),
            Int(nameof(RunPowerShellScriptAction.TimeoutMilliseconds), "Timeout (ms)", required: true)
        ]);
        Add<RunPythonScriptAction>(schemas, "Configure Python Script", [
            File(nameof(RunPythonScriptAction.ScriptPath), "Script file", required: true),
            Text(nameof(RunPythonScriptAction.PythonExecutable), "Python executable", required: true),
            Directory(nameof(RunPythonScriptAction.WorkingDirectory), "Working directory"),
            Int(nameof(RunPythonScriptAction.TimeoutMilliseconds), "Timeout (ms)", required: true)
        ]);

        Add<ChangeTextCaseAction>(schemas, "Configure Text Case", [
            Multiline(nameof(ChangeTextCaseAction.Text), "Text"),
            Enum<TextCase>(nameof(ChangeTextCaseAction.Case), "Case")
        ]);
        Add<ReplaceTextAction>(schemas, "Configure Replace Text", [
            Multiline(nameof(ReplaceTextAction.Text), "Text"),
            Text(nameof(ReplaceTextAction.SearchText), "Search text", required: true),
            Text(nameof(ReplaceTextAction.ReplacementText), "Replacement text"),
            Bool(nameof(ReplaceTextAction.IgnoreCase), "Ignore case")
        ]);
        Add<SubstringAction>(schemas, "Configure Substring", [
            Multiline(nameof(SubstringAction.Text), "Text"),
            Int(nameof(SubstringAction.StartIndex), "Start index", required: true),
            Int(nameof(SubstringAction.Length), "Length", required: true)
        ]);

        Add<FormatDateTimeAction>(schemas, "Configure Format Date/Time", [
            DateTime(nameof(FormatDateTimeAction.Value), "Value", required: true),
            Text(nameof(FormatDateTimeAction.Format), "Format", required: true),
            Text(nameof(FormatDateTimeAction.CultureName), "Culture name")
        ]);
        Add<AddDateTimeAction>(schemas, "Configure Add Date/Time", [
            DateTime(nameof(AddDateTimeAction.Value), "Value", required: true),
            Double(nameof(AddDateTimeAction.Amount), "Amount", required: true),
            Enum<DateTimeUnit>(nameof(AddDateTimeAction.Unit), "Unit")
        ]);

        Add<MoveMouseToAction>(schemas, "Configure Mouse Move", [
            Int(nameof(MoveMouseToAction.X), "X", required: true),
            Int(nameof(MoveMouseToAction.Y), "Y", required: true)
        ]);
        Add<MouseSingleClickAction>(schemas, "Configure Mouse Click", PointerFields());
        Add<MouseDoubleClickAction>(schemas, "Configure Mouse Double Click", PointerFields());
        Add<SendKeysAction>(schemas, "Configure Send Keys", [
            Text(nameof(SendKeysAction.Text), "Text", required: true),
            Bool(nameof(SendKeysAction.Wait), "Wait until sent")
        ]);
        Add<SendKeyDownAction>(schemas, "Configure Key Down", [Text(nameof(SendKeyDownAction.Key), "Key", required: true)]);
        Add<SendKeyUpAction>(schemas, "Configure Key Up", [Text(nameof(SendKeyUpAction.Key), "Key", required: true)]);

        Add<IfWindowContainsElementAction>(schemas, "Configure Element Check", UiElementFields());
        Add<SetFocusOnElementAction>(schemas, "Configure Focus Element", UiElementFields());
        Add<SelectRadioButtonAction>(schemas, "Configure Radio Button", UiElementFields());
        Add<SetCheckBoxStateAction>(schemas, "Configure CheckBox", [.. UiElementFields(), Bool(nameof(SetCheckBoxStateAction.IsChecked), "Checked")]);
        Add<SelectComboBoxItemAction>(schemas, "Configure ComboBox", [.. UiElementFields(), Text(nameof(SelectComboBoxItemAction.ItemText), "Item text", required: true)]);
        Add<SelectTabItemAction>(schemas, "Configure Tab Item", UiElementFields());

        return schemas;
    }

    private static IReadOnlyList<ActionConfigurationField> UiElementFields()
    {
        return
        [
            EnumText(nameof(UiElementActionBase.IdentifierType), "Identifier type", ["XPath", "AutomationId", "Name", "ClassName"]),
            Text(nameof(UiElementActionBase.ElementIdentifier), "Element identifier", required: true),
            Text(nameof(UiElementActionBase.SearchScope), "Search scope key"),
            Bool(nameof(UiElementActionBase.AddToGlobalDictionary), "Add found element to global dictionary"),
            Text(nameof(UiElementActionBase.GlobalDictionaryKey), "Global dictionary key")
        ];
    }

    private static IReadOnlyList<ActionConfigurationField> PointerFields()
    {
        return
        [
            Int(nameof(PointerClickActionBase.X), "X", required: true),
            Int(nameof(PointerClickActionBase.Y), "Y", required: true),
            Enum<MouseButton>(nameof(PointerClickActionBase.Button), "Button")
        ];
    }

    private static void Add<TAction>(Dictionary<Type, ActionConfigurationSchema> schemas, string title, IReadOnlyList<ActionConfigurationField> fields)
    {
        schemas[typeof(TAction)] = new ActionConfigurationSchema(typeof(TAction), title, fields);
    }

    private static ActionConfigurationField Text(string propertyName, string label, bool required = false) =>
        ActionConfigurationField.Text(propertyName, label, required);

    private static ActionConfigurationField Multiline(string propertyName, string label, bool required = false) =>
        ActionConfigurationField.Text(propertyName, label, required, multiline: true);

    private static ActionConfigurationField File(string propertyName, string label, bool required = false) =>
        ActionConfigurationField.File(propertyName, label, required);

    private static ActionConfigurationField Directory(string propertyName, string label, bool required = false) =>
        ActionConfigurationField.Directory(propertyName, label, required);

    private static ActionConfigurationField Int(string propertyName, string label, bool required = false) =>
        ActionConfigurationField.Integer(propertyName, label, required);

    private static ActionConfigurationField Double(string propertyName, string label, bool required = false) =>
        ActionConfigurationField.Double(propertyName, label, required);

    private static ActionConfigurationField Bool(string propertyName, string label) =>
        ActionConfigurationField.Boolean(propertyName, label);

    private static ActionConfigurationField Enum<TEnum>(string propertyName, string label)
        where TEnum : struct, Enum =>
        ActionConfigurationField.Enum<TEnum>(propertyName, label);

    private static ActionConfigurationField EnumText(string propertyName, string label, string[] options) =>
        new(propertyName, label, ActionConfigurationFieldKind.Enum, false, typeof(string), Options: options);

    private static ActionConfigurationField DateTime(string propertyName, string label, bool required = false) =>
        ActionConfigurationField.DateTimeOffset(propertyName, label, required);
}
