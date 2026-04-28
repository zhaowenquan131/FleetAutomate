using FleetAutomate.Expressions;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Persistence;

public sealed class TestFlowDocumentMapper
{
    private readonly ActionTypeRegistry _registry;

    public TestFlowDocumentMapper(ActionTypeRegistry? registry = null)
    {
        _registry = registry ?? ActionTypeRegistry.CreateDefault();
    }

    public TestFlowDocument ToDocument(TestFlow flow)
    {
        return new TestFlowDocument
        {
            Name = flow.Name ?? string.Empty,
            Description = flow.Description ?? string.Empty,
            IsEnabled = flow.IsEnabled,
            Actions = flow.Actions.Select(_registry.ToDocument).ToList(),
            Environment = flow.Environment.Variables
                .Where(v => !string.IsNullOrWhiteSpace(v.Name))
                .Select(v => ValueSerializer.ToVariable(v.Name, v.Value, v.Type))
                .ToList(),
            GlobalElements = flow.GlobalElementDictionary.Keys
                .Select(k => new ElementDocument { Key = k })
                .ToList()
        };
    }

    public TestFlow FromDocument(TestFlowDocument document)
    {
        var flow = new TestFlow
        {
            Name = document.Name,
            Description = document.Description,
            IsEnabled = document.IsEnabled,
            Environment = new FleetAutomate.Model.Actions.Logic.Environment
            {
                Variables = document.Environment
                    .Select(v => new Variable(v.Name, ValueSerializer.FromVariable(v), TypeIds.ToType(v.TypeId)))
                    .ToList()
            },
            GlobalElementDictionary = new GlobalElementDictionary()
        };

        foreach (var element in document.GlobalElements)
        {
            flow.GlobalElementDictionary.RegisterKey(element.Key);
        }

        foreach (var action in document.Actions.Select(_registry.FromDocument))
        {
            flow.Actions.Add(action);
        }

        flow.InitializeAfterDeserialization();
        InjectEnvironment(flow.Actions, flow.Environment);
        return flow;
    }

    private static void InjectEnvironment(IEnumerable<IAction> actions, FleetAutomate.Model.Actions.Logic.Environment environment)
    {
        foreach (var action in actions)
        {
            if (action is ILogicAction logicAction)
            {
                logicAction.Environment = environment;
            }

            if (action is IfAction ifAction)
            {
                InjectEnvironment(ifAction.IfBlock, environment);
                InjectEnvironment(ifAction.ElseBlock, environment);
                InjectEnvironment(ifAction.ElseIfs, environment);
            }
            else if (action is ICompositeAction compositeAction)
            {
                InjectEnvironment(compositeAction.GetChildActions(), environment);
            }
        }
    }
}
