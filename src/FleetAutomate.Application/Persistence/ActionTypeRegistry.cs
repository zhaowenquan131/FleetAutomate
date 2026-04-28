using System.Collections.ObjectModel;
using Canvas.TestRunner.Model.Actions;
using FleetAutomate.Expressions;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Persistence;

public interface IActionDocumentMapper
{
    string TypeId { get; }

    Type ActionType { get; }

    ActionDocument ToDocument(IAction action, ActionTypeRegistry registry);

    IAction FromDocument(ActionDocument document, ActionTypeRegistry registry);
}

public sealed class ActionTypeRegistry
{
    private readonly Dictionary<Type, IActionDocumentMapper> _byType = [];
    private readonly Dictionary<string, IActionDocumentMapper> _byTypeId = [];

    public static ActionTypeRegistry CreateDefault()
    {
        var registry = new ActionTypeRegistry();
        registry.Register(new ReflectionActionMapper<WaitDurationAction>("system.waitDuration"));
        registry.Register(new ReflectionActionMapper<LaunchApplicationAction>("system.launchApplication"));
        registry.Register(new ReflectionActionMapper<ClickElementAction>("ui.clickElement"));
        registry.Register(new ReflectionActionMapper<WaitForElementAction>("ui.waitForElement"));
        registry.Register(new ReflectionActionMapper<SetTextAction>("ui.setText"));
        registry.Register(new ReflectionActionMapper<IfWindowContainsTextAction>("ui.ifWindowContainsText"));
        registry.Register(new ReflectionActionMapper<NotImplementedAction>("action.notImplemented"));
        registry.Register(new SetVariableActionMapper());
        registry.Register(new IfActionMapper());
        registry.Register(new WhileLoopActionMapper());
        registry.Register(new ForLoopActionMapper());
        return registry;
    }

    public void Register(IActionDocumentMapper mapper)
    {
        _byType[mapper.ActionType] = mapper;
        _byTypeId[mapper.TypeId] = mapper;
    }

    public ActionDocument ToDocument(IAction action)
    {
        var mapper = ResolveMapper(action.GetType());
        return mapper.ToDocument(action, this);
    }

    public IAction FromDocument(ActionDocument document)
    {
        if (!_byTypeId.TryGetValue(document.TypeId, out var mapper))
        {
            throw new InvalidOperationException($"Unknown action typeId '{document.TypeId}'.");
        }

        return mapper.FromDocument(document, this);
    }

    private IActionDocumentMapper ResolveMapper(Type actionType)
    {
        if (_byType.TryGetValue(actionType, out var mapper))
        {
            return mapper;
        }

        var genericDefinition = actionType.IsGenericType ? actionType.GetGenericTypeDefinition() : null;
        if (genericDefinition != null && _byType.TryGetValue(genericDefinition, out mapper))
        {
            return mapper;
        }

        throw new InvalidOperationException($"No action document mapper registered for '{actionType.FullName}'.");
    }
}

internal class ReflectionActionMapper<TAction> : IActionDocumentMapper
    where TAction : class, IAction, new()
{
    public ReflectionActionMapper(string typeId)
    {
        TypeId = typeId;
    }

    public string TypeId { get; }

    public Type ActionType => typeof(TAction);

    public virtual ActionDocument ToDocument(IAction action, ActionTypeRegistry registry)
    {
        return new ActionDocument
        {
            TypeId = TypeId,
            Version = 1,
            Properties = ActionPropertyReflection.GetPersistedProperties(action)
                .Select(p => ValueSerializer.ToProperty(p.Name, p.GetValue(action), p.PropertyType))
                .ToList()
        };
    }

    public virtual IAction FromDocument(ActionDocument document, ActionTypeRegistry registry)
    {
        var action = new TAction();
        ActionPropertyReflection.SetPersistedProperties(action, document.Properties);
        return action;
    }
}

internal sealed class SetVariableActionMapper : IActionDocumentMapper
{
    public string TypeId => "logic.setVariable";

    public Type ActionType => typeof(SetVariableAction<>);

    public ActionDocument ToDocument(IAction action, ActionTypeRegistry registry)
    {
        dynamic dynamicAction = action;
        Variable variable = dynamicAction.Variable;
        return new ActionDocument
        {
            TypeId = TypeId,
            Version = 1,
            Properties =
            [
                ValueSerializer.ToProperty(nameof(Variable.Name), variable.Name, typeof(string)),
                ValueSerializer.ToProperty(nameof(Variable.Value), variable.Value, variable.Type),
                ValueSerializer.ToProperty(nameof(Variable.TypeName), TypeIds.FromType(variable.Type), typeof(string)),
                ValueSerializer.ToProperty(nameof(SetVariableAction<object>.ValueMode), dynamicAction.ValueMode, typeof(string)),
                ValueSerializer.ToProperty(nameof(SetVariableAction<object>.ExpressionText), dynamicAction.ExpressionText, typeof(string)),
                ValueSerializer.ToProperty(nameof(SetVariableAction<object>.Description), dynamicAction.Description, typeof(string)),
                ValueSerializer.ToProperty(nameof(SetVariableAction<object>.IsEnabled), dynamicAction.IsEnabled, typeof(bool))
            ]
        };
    }

    public IAction FromDocument(ActionDocument document, ActionTypeRegistry registry)
    {
        var properties = document.Properties.ToDictionary(p => p.Name, StringComparer.Ordinal);
        var targetType = TypeIds.ToType(GetString(properties, nameof(Variable.TypeName)) ?? TypeIds.Object);
        var actionType = typeof(SetVariableAction<>).MakeGenericType(targetType);
        var action = (IAction)Activator.CreateInstance(actionType)!;
        dynamic dynamicAction = action;
        dynamicAction.Variable = new Variable
        {
            Name = GetString(properties, nameof(Variable.Name)) ?? string.Empty,
            Value = properties.TryGetValue(nameof(Variable.Value), out var valueProperty)
                ? ValueSerializer.FromString(valueProperty.Value, targetType)
                : null,
            Type = targetType
        };
        dynamicAction.ValueMode = Enum.Parse<SetVariableValueMode>(GetString(properties, nameof(SetVariableAction<object>.ValueMode)) ?? nameof(SetVariableValueMode.Literal));
        dynamicAction.ExpressionText = GetString(properties, nameof(SetVariableAction<object>.ExpressionText)) ?? string.Empty;
        dynamicAction.Description = GetString(properties, nameof(SetVariableAction<object>.Description)) ?? "Leave your comment here.";
        dynamicAction.IsEnabled = properties.TryGetValue(nameof(SetVariableAction<object>.IsEnabled), out var enabled)
            ? (bool)ValueSerializer.FromProperty(enabled)!
            : true;
        dynamicAction.Environment = new FleetAutomate.Model.Actions.Logic.Environment();
        return action;
    }

    private static string? GetString(Dictionary<string, PropertyDocument> properties, string name)
    {
        return properties.TryGetValue(name, out var property) ? property.Value : null;
    }
}

internal sealed class IfActionMapper : ReflectionActionMapper<IfAction>
{
    public IfActionMapper() : base("logic.if")
    {
    }

    public override ActionDocument ToDocument(IAction action, ActionTypeRegistry registry)
    {
        var document = base.ToDocument(action, registry);
        var ifAction = (IfAction)action;
        document.Children =
        [
            ToChild("then", ifAction.IfBlock, registry),
            ToChild("else", ifAction.ElseBlock, registry),
            ToChild("elseIf", new ObservableCollection<IAction>(ifAction.ElseIfs.Cast<IAction>().ToList()), registry)
        ];
        return document;
    }

    public override IAction FromDocument(ActionDocument document, ActionTypeRegistry registry)
    {
        var action = (IfAction)base.FromDocument(document, registry);
        foreach (var child in document.Children)
        {
            var target = child.Name switch
            {
                "then" => action.IfBlock,
                "else" => action.ElseBlock,
                _ => null
            };

            if (target != null)
            {
                target.Clear();
                foreach (var childAction in child.Actions.Select(registry.FromDocument))
                {
                    target.Add(childAction);
                }
            }
            else if (child.Name == "elseIf")
            {
                action.ElseIfs.Clear();
                foreach (var elseIf in child.Actions.Select(registry.FromDocument).OfType<IfAction>())
                {
                    action.ElseIfs.Add(elseIf);
                }
            }
        }

        action.InitializeAfterDeserialization();
        return action;
    }

    private static ActionChildCollectionDocument ToChild(string name, IEnumerable<IAction> actions, ActionTypeRegistry registry)
    {
        return new ActionChildCollectionDocument
        {
            Name = name,
            Actions = actions.Select(registry.ToDocument).ToList()
        };
    }
}

internal sealed class WhileLoopActionMapper : ReflectionActionMapper<WhileLoopAction>
{
    public WhileLoopActionMapper() : base("logic.while")
    {
    }

    public override ActionDocument ToDocument(IAction action, ActionTypeRegistry registry)
    {
        var document = base.ToDocument(action, registry);
        var loop = (WhileLoopAction)action;
        document.Children = [new ActionChildCollectionDocument { Name = "body", Actions = loop.Body.Select(registry.ToDocument).ToList() }];
        return document;
    }

    public override IAction FromDocument(ActionDocument document, ActionTypeRegistry registry)
    {
        var loop = (WhileLoopAction)base.FromDocument(document, registry);
        var body = document.Children.FirstOrDefault(c => c.Name == "body");
        if (body != null)
        {
            loop.Body.Clear();
            foreach (var action in body.Actions.Select(registry.FromDocument))
            {
                loop.Body.Add(action);
            }
        }

        return loop;
    }
}

internal sealed class ForLoopActionMapper : ReflectionActionMapper<ForLoopAction>
{
    public ForLoopActionMapper() : base("logic.for")
    {
    }

    public override ActionDocument ToDocument(IAction action, ActionTypeRegistry registry)
    {
        var document = base.ToDocument(action, registry);
        var loop = (ForLoopAction)action;
        document.Children = [new ActionChildCollectionDocument { Name = "body", Actions = loop.Body.Select(registry.ToDocument).ToList() }];
        return document;
    }

    public override IAction FromDocument(ActionDocument document, ActionTypeRegistry registry)
    {
        var loop = (ForLoopAction)base.FromDocument(document, registry);
        var body = document.Children.FirstOrDefault(c => c.Name == "body");
        if (body != null)
        {
            loop.Body.Clear();
            foreach (var action in body.Actions.Select(registry.FromDocument))
            {
                loop.Body.Add(action);
            }
        }

        return loop;
    }
}
