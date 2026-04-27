using FleetAutomate.Model;
using FleetAutomate.Model.Actions.DateAndTime;
using FleetAutomate.Model.Actions.FileSystem;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.MouseAndKeyboard;
using FleetAutomate.Model.Actions.Scripts;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.Text;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Flow;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;

namespace FleetAutomate.Application.Commanding;

public class ActionMutationService
{
    private static readonly Dictionary<string, Func<IAction>> ActionFactories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LaunchApplicationAction"] = () => new LaunchApplicationAction(),
        ["WaitDurationAction"] = () => new WaitDurationAction(),
        ["WaitAction"] = () => new WaitDurationAction(),
        ["WaitForElementAction"] = () => new WaitForElementAction(),
        ["ClickElementAction"] = () => new ClickElementAction(),
        ["SetTextAction"] = () => new SetTextAction(),
        ["LogAction"] = () => new LogAction(),
        ["IfFileExistsAction"] = () => new IfFileExistsAction(),
        ["If File Exists"] = () => new IfFileExistsAction(),
        ["IfDirectoryExistsAction"] = () => new IfDirectoryExistsAction(),
        ["If Directory Exists"] = () => new IfDirectoryExistsAction(),
        ["CreateDirectoryAction"] = () => new CreateDirectoryAction(),
        ["Create Directory"] = () => new CreateDirectoryAction(),
        ["ClearDirectoryAction"] = () => new ClearDirectoryAction(),
        ["Clear Directory"] = () => new ClearDirectoryAction(),
        ["DeleteDirectoryAction"] = () => new DeleteDirectoryAction(),
        ["Delete Directory"] = () => new DeleteDirectoryAction(),
        ["WaitForFileAction"] = () => new WaitForFileAction(),
        ["Wait for File"] = () => new WaitForFileAction(),
        ["CopyFileAction"] = () => new CopyFileAction(),
        ["Copy File"] = () => new CopyFileAction(),
        ["MoveFileAction"] = () => new MoveFileAction(),
        ["Move File"] = () => new MoveFileAction(),
        ["DeleteFileAction"] = () => new DeleteFileAction(),
        ["Delete File"] = () => new DeleteFileAction(),
        ["RenameFileAction"] = () => new RenameFileAction(),
        ["Rename File"] = () => new RenameFileAction(),
        ["ReadTextFromFileAction"] = () => new ReadTextFromFileAction(),
        ["Read Text from File"] = () => new ReadTextFromFileAction(),
        ["WriteTextToFileAction"] = () => new WriteTextToFileAction(),
        ["Write Text to File"] = () => new WriteTextToFileAction(),
        ["GetDirectoryOfFileAction"] = () => new GetDirectoryOfFileAction(),
        ["Get Directory of File"] = () => new GetDirectoryOfFileAction(),
        ["RunCommandAction"] = () => new RunCommandAction(),
        ["Run CMD"] = () => new RunCommandAction(),
        ["RunPowerShellCommandAction"] = () => new RunPowerShellCommandAction(),
        ["Run PowerShell Command"] = () => new RunPowerShellCommandAction(),
        ["RunBatchScriptAction"] = () => new RunBatchScriptAction(),
        ["Run Batch Script"] = () => new RunBatchScriptAction(),
        ["RunPowerShellScriptAction"] = () => new RunPowerShellScriptAction(),
        ["Run PowerShell Script"] = () => new RunPowerShellScriptAction(),
        ["RunPythonScriptAction"] = () => new RunPythonScriptAction(),
        ["Run Python Script"] = () => new RunPythonScriptAction(),
        ["IfProcessExistsAction"] = () => new IfProcessExistsAction(),
        ["If Process Exists"] = () => new IfProcessExistsAction(),
        ["KillProcessAction"] = () => new KillProcessAction(),
        ["Kill Process"] = () => new KillProcessAction(),
        ["GetScreenshotAction"] = () => new GetScreenshotAction(),
        ["Get Screenshot"] = () => new GetScreenshotAction(),
        ["SetClipboardAction"] = () => new SetClipboardAction(),
        ["Set Clipboard"] = () => new SetClipboardAction(),
        ["PlaySoundAction"] = () => new PlaySoundAction(),
        ["Play Sound"] = () => new PlaySoundAction(),
        ["ChangeTextCaseAction"] = () => new ChangeTextCaseAction(),
        ["Change Text Case"] = () => new ChangeTextCaseAction(),
        ["ReplaceTextAction"] = () => new ReplaceTextAction(),
        ["Replace Text"] = () => new ReplaceTextAction(),
        ["SubstringAction"] = () => new SubstringAction(),
        ["Substring"] = () => new SubstringAction(),
        ["GetCurrentDateTimeAction"] = () => new GetCurrentDateTimeAction(),
        ["Get Current Date/Time"] = () => new GetCurrentDateTimeAction(),
        ["FormatDateTimeAction"] = () => new FormatDateTimeAction(),
        ["Format Date/Time"] = () => new FormatDateTimeAction(),
        ["AddDateTimeAction"] = () => new AddDateTimeAction(),
        ["Add to Date/Time"] = () => new AddDateTimeAction(),
        ["MoveMouseToAction"] = () => new MoveMouseToAction(),
        ["Move Mouse To"] = () => new MoveMouseToAction(),
        ["MouseSingleClickAction"] = () => new MouseSingleClickAction(),
        ["Mouse Single Click"] = () => new MouseSingleClickAction(),
        ["MouseDoubleClickAction"] = () => new MouseDoubleClickAction(),
        ["Mouse Double Click"] = () => new MouseDoubleClickAction(),
        ["SendKeysAction"] = () => new SendKeysAction(),
        ["Send Keys"] = () => new SendKeysAction(),
        ["SendKeyDownAction"] = () => new SendKeyDownAction(),
        ["Send Key Down"] = () => new SendKeyDownAction(),
        ["SendKeyUpAction"] = () => new SendKeyUpAction(),
        ["Send Key Up"] = () => new SendKeyUpAction(),
        ["IfAction"] = () => new IfAction
        {
            Environment = new FleetAutomate.Model.Actions.Logic.Environment(),
            Condition = true
        }
    };

    private readonly ActionPathResolver _pathResolver = new();

    public IAction CreateAction(string type)
    {
        if (!ActionFactories.TryGetValue(type, out var factory))
        {
            var supported = string.Join(", ", ActionFactories.Keys.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase));
            throw new InvalidOperationException($"Unsupported action type '{type}'. Supported types: {supported}");
        }

        return factory();
    }

    public string AddAction(TestFlow flow, IAction action, string? parentPath, string? containerName, int? index)
    {
        var target = ResolveTargetCollection(flow, parentPath, containerName);
        var insertIndex = index ?? target.Actions.Count;
        if (insertIndex < 0 || insertIndex > target.Actions.Count)
        {
            throw new InvalidOperationException($"Insert index {insertIndex} is out of range for container '{target.ContainerName}'.");
        }

        target.Actions.Insert(insertIndex, action);
        return GetPathForInsertedAction(target, insertIndex);
    }

    public void RemoveAction(TestFlow flow, string path)
    {
        var location = ResolveExistingActionLocation(flow, path);
        location.Actions.RemoveAt(location.Index);
    }

    public void SetProperty(TestFlow flow, string path, string propertyName, string value)
    {
        var node = _pathResolver.Resolve(flow, path);
        var property = node.Action.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property == null)
        {
            throw new InvalidOperationException($"Property '{propertyName}' does not exist on action type '{node.Action.GetType().Name}'.");
        }

        if (!property.CanWrite || property.SetMethod == null || !property.SetMethod.IsPublic)
        {
            throw new InvalidOperationException($"Property '{property.Name}' on action type '{node.Action.GetType().Name}' is not writable.");
        }

        if (property.GetCustomAttribute<IgnoreDataMemberAttribute>() != null)
        {
            throw new InvalidOperationException($"Property '{property.Name}' cannot be set because it is runtime-only.");
        }

        var converted = ConvertValue(value, property.PropertyType);
        property.SetValue(node.Action, converted);
    }

    private TargetCollection ResolveTargetCollection(TestFlow flow, string? parentPath, string? containerName)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            var rootContainer = string.IsNullOrWhiteSpace(containerName) ? "root" : containerName;
            if (!rootContainer.Equals("root", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Container can only be 'root' when parent-path is omitted.");
            }

            return new TargetCollection(flow.Actions, "root", null);
        }

        var parent = _pathResolver.Resolve(flow, parentPath);
        var container = string.IsNullOrWhiteSpace(containerName) ? "children" : containerName;

        return parent.Action switch
        {
            IfAction ifAction => container.ToLowerInvariant() switch
            {
                "if" => new TargetCollection(ifAction.IfBlock, "if", parent.Path),
                "else" => new TargetCollection(ifAction.ElseBlock, "else", parent.Path),
                "children" => new TargetCollection(ifAction.GetChildActions(), "children", parent.Path),
                _ => throw new InvalidOperationException($"Container '{container}' is not supported for IfAction. Use if, else, or children.")
            },
            ICompositeAction compositeAction when container.Equals("children", StringComparison.OrdinalIgnoreCase)
                => new TargetCollection(compositeAction.GetChildActions(), "children", parent.Path),
            ICompositeAction
                => throw new InvalidOperationException($"Container '{container}' is not supported for composite action '{parent.Type}'. Use children."),
            _ => throw new InvalidOperationException($"Action '{parent.Path}' of type '{parent.Type}' does not contain child action collections.")
        };
    }

    private ActionLocation ResolveExistingActionLocation(TestFlow flow, string path)
    {
        var lastSeparator = path.LastIndexOf('.');
        if (lastSeparator < 0)
        {
            if (!int.TryParse(path, NumberStyles.None, CultureInfo.InvariantCulture, out var rootIndex))
            {
                throw new InvalidOperationException($"Action path '{path}' is invalid.");
            }

            return new ActionLocation(flow.Actions, rootIndex);
        }

        var parentSegment = path[..lastSeparator];
        var indexSegment = path[(lastSeparator + 1)..];
        if (!int.TryParse(indexSegment, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            throw new InvalidOperationException($"Action path '{path}' is invalid.");
        }

        var markerSeparator = parentSegment.LastIndexOf('.');
        if (markerSeparator < 0)
        {
            throw new InvalidOperationException($"Action path '{path}' is invalid.");
        }

        var parentPath = parentSegment[..markerSeparator];
        var container = parentSegment[(markerSeparator + 1)..];
        var target = ResolveTargetCollection(flow, parentPath, container);
        if (index < 0 || index >= target.Actions.Count)
        {
            throw new InvalidOperationException($"Action path '{path}' does not exist.");
        }

        return new ActionLocation(target.Actions, index);
    }

    private static string GetPathForInsertedAction(TargetCollection target, int insertIndex)
    {
        if (string.IsNullOrEmpty(target.ParentPath))
        {
            return insertIndex.ToString(CultureInfo.InvariantCulture);
        }

        var prefix = target.ContainerName.Equals("children", StringComparison.OrdinalIgnoreCase)
            ? $"{target.ParentPath}.children"
            : $"{target.ParentPath}.{target.ContainerName}";

        return $"{prefix}.{insertIndex.ToString(CultureInfo.InvariantCulture)}";
    }

    public object? ConvertPropertyValue(string rawValue, Type propertyType)
    {
        return ConvertValue(rawValue, propertyType);
    }

    private static object? ConvertValue(string rawValue, Type propertyType)
    {
        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (targetType == typeof(string))
        {
            return rawValue;
        }

        if (targetType == typeof(bool))
        {
            if (bool.TryParse(rawValue, out var boolValue))
            {
                return boolValue;
            }

            throw new InvalidOperationException($"'{rawValue}' is not a valid boolean value.");
        }

        if (targetType == typeof(int))
        {
            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return intValue;
            }

            throw new InvalidOperationException($"'{rawValue}' is not a valid integer value.");
        }

        if (targetType == typeof(long))
        {
            if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                return longValue;
            }

            throw new InvalidOperationException($"'{rawValue}' is not a valid long value.");
        }

        if (targetType == typeof(double))
        {
            if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return doubleValue;
            }

            throw new InvalidOperationException($"'{rawValue}' is not a valid double value.");
        }

        if (targetType == typeof(float))
        {
            if (float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue))
            {
                return floatValue;
            }

            throw new InvalidOperationException($"'{rawValue}' is not a valid float value.");
        }

        if (targetType == typeof(decimal))
        {
            if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
            {
                return decimalValue;
            }

            throw new InvalidOperationException($"'{rawValue}' is not a valid decimal value.");
        }

        if (targetType.IsEnum)
        {
            try
            {
                return Enum.Parse(targetType, rawValue, ignoreCase: true);
            }
            catch (Exception)
            {
                var values = string.Join(", ", Enum.GetNames(targetType));
                throw new InvalidOperationException($"'{rawValue}' is not valid for enum '{targetType.Name}'. Allowed values: {values}");
            }
        }

        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromInvariantString(rawValue);
        }

        throw new InvalidOperationException($"Property type '{targetType.Name}' is not supported by the setter.");
    }

    private sealed record TargetCollection(IList<IAction> Actions, string ContainerName, string? ParentPath);

    private sealed record ActionLocation(IList<IAction> Actions, int Index);
}
