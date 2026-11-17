using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using VariableEnvironment = FleetAutomate.Model.Actions.Logic.Environment;

namespace FleetAutomate.Model;

public static class ActionVariableScope
{
    public static IReadOnlyList<Variable> GetAvailableVariables(IEnumerable<IAction> actions, VariableEnvironment environment,
        IAction? target = null, bool afterTarget = false, bool insideTarget = false)
    {
        var roots = actions.ToList();
        var declared = new HashSet<string>(StringComparer.Ordinal);
        CollectDeclarations(roots, declared);
        var visible = environment.Variables.Where(v => !declared.Contains(v.Name))
            .GroupBy(v => v.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        Find(roots, target, afterTarget, insideTarget, visible, out var result);
        return result.Values.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool Find(IEnumerable<IAction> actions, IAction? target, bool after, bool inside,
        Dictionary<string, Variable> visible, out Dictionary<string, Variable> result)
    {
        result = visible;
        foreach (var action in actions.Where(a => a is not ActionBlock))
        {
            if (ReferenceEquals(action, target))
            {
                if (inside && action is ICompositeAction composite)
                {
                    var children = new Dictionary<string, Variable>(visible, StringComparer.Ordinal);
                    if (action is ForLoopAction { Initialization: IAction init }) AddDeclaration(init, children);
                    Find(composite.GetChildActions(), null, false, false, children, out result);
                }
                else if (after) AddDeclaration(action, visible);
                return true;
            }

            if (action is IfAction ifAction && target is ActionBlock block && ReferenceEquals(block.ParentIfAction, ifAction))
            {
                Find(block.ManagedCollection, null, false, false, new(visible, StringComparer.Ordinal), out result);
                return true;
            }

            if (target != null)
            {
                foreach (var children in ChildCollections(action))
                {
                    var branch = new Dictionary<string, Variable>(visible, StringComparer.Ordinal);
                    if (action.IsEnabled && action is ForLoopAction { Initialization: IAction init } &&
                        !children.Any(child => ReferenceEquals(child, init))) AddDeclaration(init, branch);
                    if (Find(children, target, after, inside, branch, out var nested))
                    {
                        result = nested;
                        return true;
                    }
                }
            }
            AddDeclaration(action, visible);
        }
        return target == null;
    }

    private static void AddDeclaration(IAction action, Dictionary<string, Variable> variables)
    {
        if (action.IsEnabled && GetDeclaration(action) is { } variable && !string.IsNullOrWhiteSpace(variable.Name))
            variables[variable.Name] = variable;
    }

    private static Variable? GetDeclaration(IAction action) =>
        action.GetType().IsGenericType && action.GetType().GetGenericTypeDefinition() == typeof(SetVariableAction<>)
            ? action.GetType().GetProperty(nameof(SetVariableAction<object>.Variable))?.GetValue(action) as Variable : null;

    private static void CollectDeclarations(IEnumerable<IAction> actions, HashSet<string> names)
    {
        foreach (var action in actions.Where(a => a is not ActionBlock))
        {
            if (GetDeclaration(action) is { } variable) names.Add(variable.Name);
            foreach (var children in ChildCollections(action)) CollectDeclarations(children, names);
        }
    }

    private static IEnumerable<IEnumerable<IAction>> ChildCollections(IAction action)
    {
        if (action is ForLoopAction loop)
        {
            if (loop.Initialization is IAction init) yield return new[] { init };
            yield return loop.Body;
            if (loop.Increment is IAction increment) yield return new[] { increment };
        }
        else if (action is ICompositeAction composite) yield return composite.GetChildActions();
        if (action is IfAction ifAction)
        {
            yield return ifAction.ElseBlock;
            yield return ifAction.ElseIfs;
        }
    }
}
