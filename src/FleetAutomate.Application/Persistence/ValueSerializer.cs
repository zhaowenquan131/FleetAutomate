using System.Globalization;
using FleetAutomate.Expressions;

namespace FleetAutomate.Persistence;

internal static class ValueSerializer
{
    public static PropertyDocument ToProperty(string name, object? value, Type? declaredType = null)
    {
        var type = declaredType ?? value?.GetType() ?? typeof(object);
        return new PropertyDocument
        {
            Name = name,
            TypeId = TypeIds.FromType(type),
            Value = ToString(value, type)
        };
    }

    public static object? FromProperty(PropertyDocument property)
    {
        return FromString(property.Value, TypeIds.ToType(property.TypeId));
    }

    public static VariableDocument ToVariable(string name, object? value, Type type)
    {
        return new VariableDocument
        {
            Name = name,
            TypeId = TypeIds.FromType(type),
            Value = ToString(value, type)
        };
    }

    public static object? FromVariable(VariableDocument variable)
    {
        return FromString(variable.Value, TypeIds.ToType(variable.TypeId));
    }

    public static string? ToString(object? value, Type type)
    {
        if (value == null)
        {
            return null;
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    public static object? FromString(string? value, Type type)
    {
        if (value == null)
        {
            return null;
        }

        if (type == typeof(string) || type == typeof(object))
        {
            return value;
        }

        if (type == typeof(bool))
        {
            return bool.Parse(value);
        }

        if (type == typeof(int))
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        if (type == typeof(double))
        {
            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        if (type == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
        }

        if (type.IsEnum)
        {
            return Enum.Parse(type, value);
        }

        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }
}
