using System.Reflection;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Persistence;

internal static class ActionPropertyReflection
{
    private static readonly HashSet<string> ExcludedNames =
    [
        nameof(IAction.Name),
        nameof(IAction.State),
        nameof(IAction.IsEnabled),
        "Result",
        "Environment",
        "ElementDictionary",
        "ChildActions",
        "IfBlockArray",
        "ElseBlockArray",
        "ElseIfsArray",
        "BodyArray"
    ];

    public static IEnumerable<PropertyInfo> GetPersistedProperties(object action)
    {
        return action.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => !ExcludedNames.Contains(p.Name))
            .Where(p => IsSupported(p.PropertyType))
            .OrderBy(p => p.Name, StringComparer.Ordinal);
    }

    public static void SetPersistedProperties(object action, IEnumerable<PropertyDocument> properties)
    {
        var propertyMap = properties.ToDictionary(p => p.Name, StringComparer.Ordinal);
        foreach (var property in GetPersistedProperties(action))
        {
            if (!propertyMap.TryGetValue(property.Name, out var document))
            {
                continue;
            }

            var value = property.PropertyType.IsEnum
                ? Enum.Parse(property.PropertyType, document.Value ?? string.Empty)
                : ValueSerializer.FromString(document.Value, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
            property.SetValue(action, value);
        }
    }

    private static bool IsSupported(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(string) ||
               underlying == typeof(bool) ||
               underlying == typeof(int) ||
               underlying == typeof(double) ||
               underlying == typeof(float) ||
               underlying == typeof(decimal) ||
               underlying == typeof(DateTimeOffset) ||
               underlying == typeof(DateTime) ||
               underlying.IsEnum;
    }
}
