using System.Collections.ObjectModel;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.ViewModel;

namespace FleetAutomate.UndoRedo;

public sealed record ActionCollectionRef(ActionCollectionKind Kind, int[] ParentPath)
{
    public static ActionCollectionRef Root { get; } = new(ActionCollectionKind.Root, []);

    public static ActionCollectionRef ForIfBlock(int[] parentPath) => new(ActionCollectionKind.IfBlock, parentPath);

    public static ActionCollectionRef ForElseBlock(int[] parentPath) => new(ActionCollectionKind.ElseBlock, parentPath);

    public static ActionCollectionRef ForLoopBody(int[] parentPath) => new(ActionCollectionKind.LoopBody, parentPath);

    public ObservableCollection<IAction> Resolve(ObservableFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (Kind == ActionCollectionKind.Root)
        {
            return flow.Actions;
        }

        var parent = ResolveAction(flow, ParentPath);
        return Kind switch
        {
            ActionCollectionKind.IfBlock when parent is IfAction ifAction => ifAction.IfBlock,
            ActionCollectionKind.ElseBlock when parent is IfAction ifAction => ifAction.ElseBlock,
            ActionCollectionKind.LoopBody when parent is WhileLoopAction whileLoop => whileLoop.Body,
            ActionCollectionKind.LoopBody when parent is ForLoopAction forLoop => forLoop.Body,
            _ => throw new InvalidOperationException($"Action at path '{string.Join(".", ParentPath)}' does not expose {Kind}.")
        };
    }

    public IAction ResolveAction(ObservableFlow flow, IReadOnlyList<int> path)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(path);

        ObservableCollection<IAction> collection = flow.Actions;
        IAction? current = null;

        foreach (var index in path)
        {
            if (index < 0 || index >= collection.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(path), $"Action path index {index} is outside collection bounds.");
            }

            current = collection[index];
            collection = GetDefaultChildCollection(current) ?? [];
        }

        return current ?? throw new InvalidOperationException("Root does not identify an action.");
    }

    private static ObservableCollection<IAction>? GetDefaultChildCollection(IAction action)
    {
        return action switch
        {
            IfAction ifAction => ifAction.IfBlock,
            WhileLoopAction whileLoop => whileLoop.Body,
            ForLoopAction forLoop => forLoop.Body,
            _ => null
        };
    }
}
