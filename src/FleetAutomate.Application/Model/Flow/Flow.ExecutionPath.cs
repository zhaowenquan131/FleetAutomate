using FleetAutomate.Model.Actions.Logic;

namespace FleetAutomate.Model.Flow;

public partial class TestFlow
{
    private IReadOnlyList<IAction> GetExecutionActions(IAction startAction, string parameterName)
    {
        var remaining = new List<IAction>();
        if (!TryCollectExecutionActions(Actions, startAction, remaining, new HashSet<IAction>(ReferenceEqualityComparer.Instance)))
        {
            throw new ArgumentException("The specified action is not part of this TestFlow.", parameterName);
        }

        return remaining;
    }

    private static bool TryCollectExecutionActions(IEnumerable<IAction> actions, IAction target, List<IAction> remaining, HashSet<IAction> visited)
    {
        var found = false;
        foreach (var action in actions)
        {
            // Else block rows are editor nodes, not executable actions.
            if (action is ActionBlock)
            {
                continue;
            }

            if (found || ReferenceEquals(action, target))
            {
                found = true;
                remaining.Add(action);
                continue;
            }

            if (!visited.Add(action))
            {
                continue;
            }

            if (action is IfAction ifAction)
            {
                // Follow only the branch containing the entry point. Do not re-evaluate
                // the enclosing If or run its alternative branch when entering a child.
                found = TryCollectExecutionActions(ifAction.IfBlock, target, remaining, visited) ||
                    TryCollectExecutionActions(ifAction.ElseBlock, target, remaining, visited) ||
                    TryCollectExecutionActions(ifAction.ElseIfs, target, remaining, visited);
            }
            else if (action is ICompositeAction composite)
            {
                found = TryCollectExecutionActions(composite.GetChildActions(), target, remaining, visited);
            }
        }

        return found;
    }
}
