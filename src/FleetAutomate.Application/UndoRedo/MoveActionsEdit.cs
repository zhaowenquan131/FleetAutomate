using System.Collections.ObjectModel;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.ViewModel;

namespace FleetAutomate.UndoRedo;

/// <summary>Moves a selection as one edit, retaining its order and original locations.</summary>
public sealed class MoveActionsEdit : IUndoableFlowEdit
{
    private sealed record Location(IAction Action, ObservableCollection<IAction> Collection, int Index, Location? Parent);

    private readonly List<Location> _sources;
    private readonly ObservableCollection<IAction> _target;
    private readonly int _targetIndex;

    private MoveActionsEdit(List<Location> sources, ObservableCollection<IAction> target, int targetIndex)
    {
        _sources = sources;
        _target = target;
        _targetIndex = targetIndex;
    }

    public string DisplayName => _sources.Count == 1 ? "Move action" : "Move actions";

    public static MoveActionsEdit? Create(ObservableFlow flow, IEnumerable<IAction> actions,
        IAction? insertionTarget, ActionInsertionMode mode)
    {
        var locations = new List<Location>();
        CollectLocations(flow.Actions, null, locations, new HashSet<ObservableCollection<IAction>>());
        var selected = actions.ToHashSet();
        if (selected.Count == 0 || selected.Any(action => !locations.Any(location => ReferenceEquals(location.Action, action))))
        {
            return null;
        }

        // A selected parent's descendants travel with it, rather than being moved twice.
        var sources = locations.Where(location => selected.Contains(location.Action) && !HasSelectedAncestor(location.Parent, selected)).ToList();
        var validationTarget = insertionTarget is ActionBlock block ? block.ParentIfAction : insertionTarget;
        var targetLocation = locations.FirstOrDefault(location => ReferenceEquals(location.Action, validationTarget));
        if (validationTarget != null && (targetLocation == null || selected.Contains(validationTarget) || HasSelectedAncestor(targetLocation.Parent, selected)))
        {
            return null;
        }

        ObservableCollection<IAction> target;
        int index;
        if (insertionTarget is ActionBlock actionBlock)
        {
            target = actionBlock.ManagedCollection;
            index = CountActions(target);
        }
        else if (mode == ActionInsertionMode.Into && insertionTarget is ICompositeAction composite)
        {
            target = composite.GetChildActions();
            index = CountActions(target);
        }
        else if (targetLocation != null)
        {
            target = targetLocation.Collection;
            index = targetLocation.Index + (mode == ActionInsertionMode.Before ? 0 : 1);
        }
        else
        {
            target = flow.Actions;
            index = CountActions(target);
        }

        index -= sources.Count(source => ReferenceEquals(source.Collection, target) && source.Index < index);
        var current = target.Where(action => action is not ActionBlock).ToList();
        var result = current.Where(action => !sources.Any(source => ReferenceEquals(source.Action, action))).ToList();
        result.InsertRange(index, sources.Select(source => source.Action));
        if (sources.All(source => ReferenceEquals(source.Collection, target)) && current.SequenceEqual(result))
        {
            return null;
        }

        return new MoveActionsEdit(sources, target, index);
    }

    public void Apply(ObservableFlow flow)
    {
        foreach (var source in _sources.AsEnumerable().Reverse())
        {
            source.Collection.Remove(source.Action);
        }

        for (var i = 0; i < _sources.Count; i++)
        {
            InsertAction(_target, _targetIndex + i, _sources[i].Action);
        }
    }

    public void Unapply(ObservableFlow flow)
    {
        for (var i = _sources.Count - 1; i >= 0; i--)
        {
            _target.Remove(_sources[i].Action);
        }

        foreach (var source in _sources)
        {
            InsertAction(source.Collection, source.Index, source.Action);
        }
    }

    private static int CountActions(ObservableCollection<IAction> collection) => collection.Count(action => action is not ActionBlock);

    private static void InsertAction(ObservableCollection<IAction> collection, int index, IAction action)
    {
        // Else blocks are generated sibling rows. They must stay next to their If action.
        var logicalIndex = 0;
        for (var i = 0; i < collection.Count; i++)
        {
            if (collection[i] is not ActionBlock && logicalIndex++ == index)
            {
                collection.Insert(i, action);
                return;
            }
        }

        collection.Add(action);
    }

    private static bool HasSelectedAncestor(Location? parent, HashSet<IAction> selected)
    {
        for (; parent != null; parent = parent.Parent)
        {
            if (selected.Contains(parent.Action))
            {
                return true;
            }
        }

        return false;
    }

    private static void CollectLocations(ObservableCollection<IAction> collection, Location? parent,
        List<Location> locations, HashSet<ObservableCollection<IAction>> visited)
    {
        if (!visited.Add(collection))
        {
            return;
        }

        var index = 0;
        foreach (var action in collection)
        {
            if (action is ActionBlock)
            {
                continue;
            }

            var location = new Location(action, collection, index++, parent);
            locations.Add(location);
            if (location.Action is IfAction ifAction)
            {
                CollectLocations(ifAction.IfBlock, location, locations, visited);
                CollectLocations(ifAction.ElseBlock, location, locations, visited);
            }
            else if (location.Action is ICompositeAction composite)
            {
                CollectLocations(composite.GetChildActions(), location, locations, visited);
            }
        }
    }
}
