using System.Collections.ObjectModel;

using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Project;

namespace FleetAutomate.Tests.Flow;

public sealed class FlowValidatorScopeTests
{
    [Fact]
    public void CreateChildContext_PreservesSharedScopeState()
    {
        var environment = new FleetAutomate.Model.Actions.Logic.Environment();
        var options = new SyntaxValidationOptions
        {
            ValidateNested = false,
            MaxDepth = 12
        };
        var project = new TestProject
        {
            Name = "project"
        };
        var flow = new TestFlow
        {
            Name = "flow",
            Environment = environment,
            ParentProject = project
        };
        project.TestFlows!.Add(flow);

        var parent = new CapturingValidatorAction("parent");
        var root = new SyntaxValidationContext
        {
            Environment = environment,
            CurrentPath = "Flow",
            Parent = flow,
            Options = options,
            TestProject = project,
            CurrentFlow = flow
        };

        var child = root.CreateChildContext("Actions[0]", parent);

        Assert.Same(environment, child.Environment);
        Assert.Same(options, child.Options);
        Assert.Same(project, child.TestProject);
        Assert.Same(flow, child.CurrentFlow);
        Assert.Same(parent, child.Parent);
        Assert.Equal("Flow.Actions[0]", child.CurrentPath);
    }

    [Fact]
    public void ValidateFlow_PropagatesDistinctChildScopesToNestedActions()
    {
        var flow = CreateFlowWithProject();

        var loop = new FleetAutomate.Model.Actions.Logic.Loops.ForLoopAction
        {
            Condition = false,
            Environment = flow.Environment,
            Initialization = null!,
            Increment = null!
        };
        var nestedChild = new CapturingValidatorAction("body-child");
        var topLevelChild = new CapturingValidatorAction("top-level");
        loop.Body.Add(nestedChild);
        flow.Actions.Add(loop);
        flow.Actions.Add(topLevelChild);

        var errors = FlowValidator.ValidateFlow(flow).ToList();

        Assert.Empty(errors);

        var nestedScope = Assert.Single(nestedChild.CapturedScopes);
        Assert.Equal("Flow.Actions[0].Body[0]", nestedScope.Path);
        Assert.Same(loop, nestedScope.Parent);
        Assert.Same(flow.Environment, nestedScope.Environment);
        Assert.Same(flow.ParentProject, nestedScope.Project);
        Assert.Same(flow, nestedScope.CurrentFlow);

        var topLevelScope = Assert.Single(topLevelChild.CapturedScopes);
        Assert.Equal("Flow.Actions[1]", topLevelScope.Path);
        Assert.Same(flow, topLevelScope.Parent);
        Assert.Same(flow.Environment, topLevelScope.Environment);
        Assert.Same(flow.ParentProject, topLevelScope.Project);
        Assert.Same(flow, topLevelScope.CurrentFlow);
    }

    [Fact]
    public void ValidateFlow_WhenNestedValidationDisabled_DoesNotTraverseNestedScopes()
    {
        var flow = CreateFlowWithProject();

        var loop = new FleetAutomate.Model.Actions.Logic.Loops.ForLoopAction
        {
            Condition = false,
            Environment = flow.Environment,
            Initialization = null!,
            Increment = null!
        };
        var child = new CapturingValidatorAction("nested");
        loop.Body.Add(child);
        flow.Actions.Add(loop);

        var errors = FlowValidator.ValidateFlow(flow, new SyntaxValidationOptions
        {
            ValidateNested = false
        }).ToList();

        Assert.Empty(errors);
        Assert.Empty(child.CapturedScopes);
    }

    [Fact]
    public void ValidateFlow_SetsActionPathFromNestedScopeOnReturnedErrors()
    {
        var flow = CreateFlowWithProject();

        var loop = new FleetAutomate.Model.Actions.Logic.Loops.ForLoopAction
        {
            Condition = false,
            Environment = flow.Environment,
            Initialization = null!,
            Increment = null!
        };
        loop.Body.Add(new ErrorProducingValidatorAction("nested failure"));
        flow.Actions.Add(loop);

        var error = Assert.Single(FlowValidator.ValidateFlow(flow));

        Assert.Equal("nested failure", error.Message);
        Assert.Equal("Flow.Actions[0].Body[0]", error.ActionPath);
    }

    [Fact]
    public void ValidateFlow_DoesNotTraverseIgnoredDisplayCollectionsTwice()
    {
        var flow = CreateFlowWithProject();
        var ifAction = new IfAction
        {
            Condition = true,
            Environment = flow.Environment
        };
        var child = new CapturingValidatorAction("nested");
        ifAction.IfBlock.Add(child);
        flow.Actions.Add(ifAction);

        var errors = FlowValidator.ValidateFlow(flow).ToList();

        Assert.Empty(errors);
        var scope = Assert.Single(child.CapturedScopes);
        Assert.Equal("Flow.Actions[0].IfBlock[0]", scope.Path);
    }

    private static TestFlow CreateFlowWithProject()
    {
        var project = new TestProject
        {
            Name = "project"
        };
        var flow = new TestFlow
        {
            Name = "flow",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment(),
            ParentProject = project
        };
        project.TestFlows!.Add(flow);
        return flow;
    }

    private sealed record CapturedScope(
        string Path,
        IAction Parent,
        FleetAutomate.Model.Actions.Logic.Environment Environment,
        TestProject Project,
        TestFlow CurrentFlow);

    private sealed class CapturingValidatorAction : IAction, ISyntaxValidator
    {
        public CapturingValidatorAction(string name)
        {
            Name = name;
            Description = name;
        }

        public string Name { get; }

        public string Description { get; }

        public ActionState State { get; set; } = ActionState.Ready;

        public bool IsEnabled => true;

        public List<CapturedScope> CapturedScopes { get; } = [];

        public void Cancel()
        {
        }

        public Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            State = ActionState.Completed;
            return Task.FromResult(true);
        }

        public IEnumerable<SyntaxError> ValidateSyntax(SyntaxValidationContext context)
        {
            CapturedScopes.Add(new CapturedScope(
                context.CurrentPath,
                context.Parent,
                context.Environment,
                context.TestProject,
                context.CurrentFlow));
            return [];
        }
    }

    private sealed class ErrorProducingValidatorAction : IAction, ISyntaxValidator
    {
        private readonly string _message;

        public ErrorProducingValidatorAction(string message)
        {
            _message = message;
        }

        public string Name => "error";

        public string Description => "error";

        public ActionState State { get; set; } = ActionState.Ready;

        public bool IsEnabled => true;

        public void Cancel()
        {
        }

        public Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            State = ActionState.Completed;
            return Task.FromResult(true);
        }

        public IEnumerable<SyntaxError> ValidateSyntax(SyntaxValidationContext context)
        {
            yield return new SyntaxError(this, _message, SyntaxErrorSeverity.Error);
        }
    }
}
