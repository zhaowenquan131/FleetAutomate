using System.Runtime.Serialization;
using System.IO;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Flow;
using Canvas.TestRunner.Model.Actions;
using LogAction = FleetAutomate.Model.Actions.System.LogAction;

namespace FleetAutomate.UndoRedo;

public sealed class ActionSnapshotService
{
    private static readonly Type[] KnownTypes = typeof(TestFlow).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && !t.ContainsGenericParameters && typeof(IAction).IsAssignableFrom(t))
        .Append(typeof(SetVariableAction<object>)).ToArray();

    private static IEnumerable<IAction> EnumerateTree(IAction root)
    {
        var visited = new HashSet<IAction>(ReferenceEqualityComparer.Instance);
        return Visit(root);

        IEnumerable<IAction> Visit(IAction action)
        {
            if (!visited.Add(action)) yield break;
            yield return action;
            IEnumerable<IAction> children = action switch
            {
                IfAction branch => branch.IfBlock.Concat(branch.ElseBlock).Concat(branch.ElseIfs),
                ForLoopAction loop => new[] { loop.Initialization as IAction }.OfType<IAction>()
                    .Concat(loop.Body).Concat(new[] { loop.Increment as IAction }.OfType<IAction>()),
                ICompositeAction composite => composite.GetChildActions(),
                _ => []
            };
            foreach (var child in children)
                foreach (var nested in Visit(child)) yield return nested;
        }
    }

    private static DataContractSerializer CreateSerializer(Type[] types) => new(typeof(IAction),
        new DataContractSerializerSettings { KnownTypes = types, PreserveObjectReferences = true });

    public int SnapshotCount { get; private set; }

    public ActionSnapshot Capture(IAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SnapshotCount++;

        var tree = EnumerateTree(action).ToList();
        var types = KnownTypes.Concat(tree.Select(a => a.GetType())).Distinct().ToArray();
        var bindings = tree.Select(a => a.TextVariableBindings).ToArray();
        var serializer = CreateSerializer(types);
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, action);
        return new ActionSnapshot(stream.ToArray(), types, bindings);
    }

    public sealed class ActionSnapshot
    {
        private readonly byte[] _payload;
        private readonly Type[] _types;
        private readonly IReadOnlyDictionary<string, string>[] _bindings;

        internal ActionSnapshot(byte[] payload, Type[] types, IReadOnlyDictionary<string, string>[] bindings)
        {
            _payload = payload;
            _types = types;
            _bindings = bindings;
        }

        public IAction Restore()
        {
            var serializer = CreateSerializer(_types);
            using var stream = new MemoryStream(_payload);
            var action = (IAction)serializer.ReadObject(stream)!;
            var tree = EnumerateTree(action).ToList();
            for (var index = 0; index < tree.Count; index++)
                tree[index].TextVariableBindings = _bindings[index];
            ResetRuntimeState(action);
            return action;
        }

        private static void ResetRuntimeState(IAction action)
        {
            action.State = ActionState.Ready;

            switch (action)
            {
                case IfAction ifAction:
                    foreach (var child in ifAction.IfBlock.Concat(ifAction.ElseBlock))
                    {
                        ResetRuntimeState(child);
                    }
                    ifAction.InitializeAfterDeserialization();
                    break;
                case WhileLoopAction whileLoop:
                    foreach (var child in whileLoop.Body)
                    {
                        ResetRuntimeState(child);
                    }
                    break;
                case ForLoopAction forLoop:
                    foreach (var child in forLoop.Body)
                    {
                        ResetRuntimeState(child);
                    }
                    break;
            }
        }
    }
}
