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
    private static readonly Type[] KnownTypes =
    [
        typeof(SetVariableAction<object>),
        typeof(WhileLoopAction),
        typeof(ForLoopAction),
        typeof(IfAction),
        typeof(SubFlowAction),
        typeof(LaunchApplicationAction),
        typeof(WaitForElementAction),
        typeof(ClickElementAction),
        typeof(SetTextAction),
        typeof(IfWindowContainsTextAction),
        typeof(NotImplementedAction),
        typeof(LogAction)
    ];

    public int SnapshotCount { get; private set; }

    public ActionSnapshot Capture(IAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SnapshotCount++;

        var serializer = new DataContractSerializer(typeof(IAction), KnownTypes);
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, action);
        return new ActionSnapshot(stream.ToArray());
    }

    public sealed class ActionSnapshot
    {
        private readonly byte[] _payload;

        internal ActionSnapshot(byte[] payload)
        {
            _payload = payload;
        }

        public IAction Restore()
        {
            var serializer = new DataContractSerializer(typeof(IAction), KnownTypes);
            using var stream = new MemoryStream(_payload);
            var action = (IAction)serializer.ReadObject(stream)!;
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
