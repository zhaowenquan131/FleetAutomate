using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;

namespace FleetAutomate.Cli.Services;

internal sealed class ActionPathResolver
{
    public IReadOnlyList<ActionNode> Flatten(TestFlow flow)
    {
        var nodes = new List<ActionNode>();
        Walk(nodes, flow.Actions, string.Empty, depth: 0, container: "root");
        return nodes;
    }

    public ActionNode Resolve(TestFlow flow, string path)
    {
        return Flatten(flow).FirstOrDefault(node => node.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Action path '{path}' does not exist in flow '{flow.Name}'.");
    }

    public IDictionary<string, object?> ExtractConfig(IAction action)
    {
        var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        var properties = action.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                property.CanRead &&
                property.GetIndexParameters().Length == 0 &&
                property.Name is not nameof(IAction.Name) and not nameof(IAction.Description) and not nameof(IAction.State) and not nameof(IAction.IsEnabled));

        foreach (var property in properties)
        {
            if (property.GetCustomAttribute<IgnoreDataMemberAttribute>() != null)
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(action);
            }
            catch
            {
                continue;
            }

            result[property.Name] = SimplifyValue(value);
        }

        return result;
    }

    private void Walk(List<ActionNode> nodes, IEnumerable<IAction> actions, string prefix, int depth, string container)
    {
        var index = 0;
        foreach (var action in actions)
        {
            var path = string.IsNullOrEmpty(prefix) ? index.ToString() : $"{prefix}.{index}";
            var node = new ActionNode(
                path,
                depth,
                container,
                action.GetType().Name,
                action.Name,
                action.Description,
                action.IsEnabled,
                action.State.ToString(),
                action);
            nodes.Add(node);

            switch (action)
            {
                case IfAction ifAction:
                    Walk(nodes, ifAction.IfBlock, $"{path}.if", depth + 1, "if");
                    Walk(nodes, ifAction.ElseBlock, $"{path}.else", depth + 1, "else");
                    Walk(nodes, ifAction.ElseIfs, $"{path}.elseif", depth + 1, "elseif");
                    break;
                case ICompositeAction compositeAction:
                    Walk(nodes, compositeAction.GetChildActions(), $"{path}.children", depth + 1, "children");
                    break;
            }

            index++;
        }
    }

    private static object? SimplifyValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is string or bool or int or long or double or float or decimal)
        {
            return value;
        }

        if (value.GetType().IsEnum)
        {
            return value.ToString();
        }

        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                result[entry.Key?.ToString() ?? string.Empty] = SimplifyValue(entry.Value);
            }

            return result;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var items = new List<object?>();
            foreach (var item in enumerable)
            {
                items.Add(SimplifyValue(item));
            }

            return items;
        }

        var dataMembers = value.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                property.CanRead &&
                property.GetIndexParameters().Length == 0 &&
                property.GetCustomAttribute<IgnoreDataMemberAttribute>() == null &&
                (property.GetCustomAttribute<DataMemberAttribute>() != null ||
                 property.PropertyType.IsPrimitive ||
                 property.PropertyType == typeof(string) ||
                 property.PropertyType.IsEnum));

        var snapshot = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in dataMembers)
        {
            snapshot[property.Name] = SimplifyValue(property.GetValue(value));
        }

        return snapshot.Count > 0 ? snapshot : value.ToString();
    }
}

internal sealed record ActionNode(
    string Path,
    int Depth,
    string Container,
    string Type,
    string Name,
    string Description,
    bool IsEnabled,
    string State,
    IAction Action);
