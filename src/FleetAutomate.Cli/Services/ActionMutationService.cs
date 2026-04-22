using FleetAutomate.Cli.Infrastructure;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Flow;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;

namespace FleetAutomate.Cli.Services;

internal sealed class ActionMutationService
{
    private static readonly Dictionary<string, Func<IAction>> ActionFactories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LaunchApplicationAction"] = () => new LaunchApplicationAction(),
        ["WaitForElementAction"] = () => new WaitForElementAction(),
        ["ClickElementAction"] = () => new ClickElementAction(),
        ["SetTextAction"] = () => new SetTextAction(),
        ["LogAction"] = () => new LogAction(),
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
            throw new CliUsageException($"Unsupported action type '{type}'. Supported types: {supported}");
        }

        return factory();
    }

    public string AddAction(TestFlow flow, IAction action, string? parentPath, string? containerName, int? index)
    {
        var target = ResolveTargetCollection(flow, parentPath, containerName);
        var insertIndex = index ?? target.Actions.Count;
        if (insertIndex < 0 || insertIndex > target.Actions.Count)
        {
            throw new CliUsageException($"Insert index {insertIndex} is out of range for container '{target.ContainerName}'.");
        }

        target.Actions.Insert(insertIndex, action);
        return GetPathForInsertedAction(flow, target, insertIndex);
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
            throw new CliUsageException($"Property '{propertyName}' does not exist on action type '{node.Action.GetType().Name}'.");
        }

        if (!property.CanWrite || property.SetMethod == null || !property.SetMethod.IsPublic)
        {
            throw new CliUsageException($"Property '{property.Name}' on action type '{node.Action.GetType().Name}' is not writable.");
        }

        if (property.GetCustomAttribute<IgnoreDataMemberAttribute>() != null)
        {
            throw new CliUsageException($"Property '{property.Name}' cannot be set through the CLI because it is runtime-only.");
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
                throw new CliUsageException("Container can only be 'root' when parent-path is omitted.");
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
                _ => throw new CliUsageException($"Container '{container}' is not supported for IfAction. Use if, else, or children.")
            },
            ICompositeAction compositeAction when container.Equals("children", StringComparison.OrdinalIgnoreCase)
                => new TargetCollection(compositeAction.GetChildActions(), "children", parent.Path),
            ICompositeAction
                => throw new CliUsageException($"Container '{container}' is not supported for composite action '{parent.Type}'. Use children."),
            _ => throw new CliUsageException($"Action '{parent.Path}' of type '{parent.Type}' does not contain child action collections.")
        };
    }

    private ActionLocation ResolveExistingActionLocation(TestFlow flow, string path)
    {
        var lastSeparator = path.LastIndexOf('.');
        if (lastSeparator < 0)
        {
            if (!int.TryParse(path, NumberStyles.None, CultureInfo.InvariantCulture, out var rootIndex))
            {
                throw new CliUsageException($"Action path '{path}' is invalid.");
            }

            return new ActionLocation(flow.Actions, rootIndex);
        }

        var parentSegment = path[..lastSeparator];
        var indexSegment = path[(lastSeparator + 1)..];
        if (!int.TryParse(indexSegment, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            throw new CliUsageException($"Action path '{path}' is invalid.");
        }

        var markerSeparator = parentSegment.LastIndexOf('.');
        if (markerSeparator < 0)
        {
            throw new CliUsageException($"Action path '{path}' is invalid.");
        }

        var parentPath = parentSegment[..markerSeparator];
        var container = parentSegment[(markerSeparator + 1)..];
        var target = ResolveTargetCollection(flow, parentPath, container);
        if (index < 0 || index >= target.Actions.Count)
        {
            throw new CliUsageException($"Action path '{path}' does not exist.");
        }

        return new ActionLocation(target.Actions, index);
    }

    private string GetPathForInsertedAction(TestFlow flow, TargetCollection target, int insertIndex)
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

            throw new CliUsageException($"'{rawValue}' is not a valid boolean value.");
        }

        if (targetType == typeof(int))
        {
            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return intValue;
            }

            throw new CliUsageException($"'{rawValue}' is not a valid integer value.");
        }

        if (targetType == typeof(long))
        {
            if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                return longValue;
            }

            throw new CliUsageException($"'{rawValue}' is not a valid long value.");
        }

        if (targetType == typeof(double))
        {
            if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return doubleValue;
            }

            throw new CliUsageException($"'{rawValue}' is not a valid double value.");
        }

        if (targetType == typeof(float))
        {
            if (float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue))
            {
                return floatValue;
            }

            throw new CliUsageException($"'{rawValue}' is not a valid float value.");
        }

        if (targetType == typeof(decimal))
        {
            if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
            {
                return decimalValue;
            }

            throw new CliUsageException($"'{rawValue}' is not a valid decimal value.");
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
                throw new CliUsageException($"'{rawValue}' is not valid for enum '{targetType.Name}'. Allowed values: {values}");
            }
        }

        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromInvariantString(rawValue);
        }

        throw new CliUsageException($"Property type '{targetType.Name}' is not supported by the CLI setter.");
    }

    private sealed record TargetCollection(IList<IAction> Actions, string ContainerName, string? ParentPath);

    private sealed record ActionLocation(IList<IAction> Actions, int Index);
}
