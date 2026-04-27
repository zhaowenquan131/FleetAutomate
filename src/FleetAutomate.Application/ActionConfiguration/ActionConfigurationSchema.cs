using System.Reflection;
using FleetAutomate.Model;

namespace FleetAutomate.Application.ActionConfiguration;

public enum ActionConfigurationFieldKind
{
    Text,
    MultilineText,
    Integer,
    Double,
    Boolean,
    Enum,
    FilePath,
    DirectoryPath,
    DateTimeOffset
}

public sealed record ActionConfigurationField(
    string PropertyName,
    string Label,
    ActionConfigurationFieldKind Kind,
    bool IsRequired = false,
    Type? ValueType = null,
    string? HelpText = null,
    IReadOnlyList<string>? Options = null)
{
    public static ActionConfigurationField Text(string propertyName, string label, bool isRequired = false, bool multiline = false)
    {
        return new ActionConfigurationField(
            propertyName,
            label,
            multiline ? ActionConfigurationFieldKind.MultilineText : ActionConfigurationFieldKind.Text,
            isRequired,
            typeof(string));
    }

    public static ActionConfigurationField File(string propertyName, string label, bool isRequired = false)
    {
        return new ActionConfigurationField(propertyName, label, ActionConfigurationFieldKind.FilePath, isRequired, typeof(string));
    }

    public static ActionConfigurationField Directory(string propertyName, string label, bool isRequired = false)
    {
        return new ActionConfigurationField(propertyName, label, ActionConfigurationFieldKind.DirectoryPath, isRequired, typeof(string));
    }

    public static ActionConfigurationField Integer(string propertyName, string label, bool isRequired = false)
    {
        return new ActionConfigurationField(propertyName, label, ActionConfigurationFieldKind.Integer, isRequired, typeof(int));
    }

    public static ActionConfigurationField Double(string propertyName, string label, bool isRequired = false)
    {
        return new ActionConfigurationField(propertyName, label, ActionConfigurationFieldKind.Double, isRequired, typeof(double));
    }

    public static ActionConfigurationField Boolean(string propertyName, string label)
    {
        return new ActionConfigurationField(propertyName, label, ActionConfigurationFieldKind.Boolean, false, typeof(bool));
    }

    public static ActionConfigurationField Enum<TEnum>(string propertyName, string label)
        where TEnum : struct, Enum
    {
        return new ActionConfigurationField(
            propertyName,
            label,
            ActionConfigurationFieldKind.Enum,
            false,
            typeof(TEnum),
            Options: global::System.Enum.GetNames<TEnum>());
    }

    public static ActionConfigurationField DateTimeOffset(string propertyName, string label, bool isRequired = false)
    {
        return new ActionConfigurationField(propertyName, label, ActionConfigurationFieldKind.DateTimeOffset, isRequired, typeof(global::System.DateTimeOffset));
    }
}

public sealed record ActionConfigurationSchema(
    Type ActionType,
    string Title,
    IReadOnlyList<ActionConfigurationField> Fields);

public sealed record ActionConfigurationValue(string PropertyName, object? Value);

public static class ActionConfigurationValueApplier
{
    public static void ApplyValues(IAction action, IEnumerable<ActionConfigurationValue> values)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(values);

        foreach (var value in values)
        {
            var property = action.GetType().GetProperty(value.PropertyName, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException($"Action property '{value.PropertyName}' was not found.");

            if (!property.CanWrite)
            {
                throw new InvalidOperationException($"Action property '{value.PropertyName}' is read-only.");
            }

            property.SetValue(action, CoerceValue(value.Value, property.PropertyType));
        }
    }

    private static object? CoerceValue(object? value, Type targetType)
    {
        if (value == null)
        {
            return null;
        }

        var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (nonNullableType.IsInstanceOfType(value))
        {
            return value;
        }

        if (nonNullableType.IsEnum)
        {
            return value is string text
                ? Enum.Parse(nonNullableType, text, ignoreCase: true)
                : Enum.ToObject(nonNullableType, value);
        }

        if (nonNullableType == typeof(DateTimeOffset))
        {
            return value is DateTimeOffset dateTimeOffset
                ? dateTimeOffset
                : DateTimeOffset.Parse(Convert.ToString(value, global::System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty, global::System.Globalization.CultureInfo.CurrentCulture);
        }

        return Convert.ChangeType(value, nonNullableType, global::System.Globalization.CultureInfo.CurrentCulture);
    }
}
