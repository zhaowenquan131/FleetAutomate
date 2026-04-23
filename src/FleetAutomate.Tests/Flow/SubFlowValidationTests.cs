using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Project;

namespace FleetAutomate.Tests.Flow;

public sealed class SubFlowValidationTests
{
    [Fact]
    public void ValidateSyntax_WithoutProjectInContext_ReturnsWarning()
    {
        var action = new SubFlowAction
        {
            TargetFlowName = "child",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };
        var flow = new TestFlow
        {
            Name = "root",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };

        var errors = action.ValidateSyntax(new SyntaxValidationContext
        {
            Environment = flow.Environment,
            CurrentPath = "Flow.Actions[0]",
            Parent = flow,
            CurrentFlow = flow
        }).ToList();

        var error = Assert.Single(errors);
        Assert.Equal(SyntaxErrorSeverity.Warning, error.Severity);
        Assert.Equal("TargetFlowName", error.PropertyName);
        Assert.Contains("TestProject not available", error.Message);
    }

    [Fact]
    public void ValidateSyntax_WhenTargetFlowMissing_ReturnsCriticalError()
    {
        var (project, flow) = CreateProjectWithFlow("root");
        var action = new SubFlowAction
        {
            TargetFlowName = "missing",
            Environment = flow.Environment
        };

        var errors = action.ValidateSyntax(CreateContext(project, flow)).ToList();

        var error = Assert.Single(errors);
        Assert.Equal(SyntaxErrorSeverity.Critical, error.Severity);
        Assert.Equal("TargetFlowName", error.PropertyName);
        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public void ValidateSyntax_WhenTargetFlowDisabled_ReturnsWarning()
    {
        var (project, root) = CreateProjectWithFlow("root");
        var disabledTarget = new TestFlow
        {
            Name = "child",
            IsEnabled = false,
            Environment = new FleetAutomate.Model.Actions.Logic.Environment(),
            ParentProject = project
        };
        project.TestFlows!.Add(disabledTarget);

        var action = new SubFlowAction
        {
            TargetFlowName = "child",
            Environment = root.Environment
        };

        var errors = action.ValidateSyntax(CreateContext(project, root)).ToList();

        var error = Assert.Single(errors);
        Assert.Equal(SyntaxErrorSeverity.Warning, error.Severity);
        Assert.Equal("TargetFlowName", error.PropertyName);
        Assert.Contains("disabled", error.Message);
    }

    [Fact]
    public void ValidateSyntax_WhenCircularReferenceExists_ReturnsCriticalError()
    {
        var project = new TestProject
        {
            Name = "project"
        };
        var flowA = new TestFlow
        {
            Name = "A",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment(),
            ParentProject = project
        };
        var flowB = new TestFlow
        {
            Name = "B",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment(),
            ParentProject = project
        };
        project.TestFlows!.Add(flowA);
        project.TestFlows.Add(flowB);

        flowA.Actions.Add(new SubFlowAction
        {
            TargetFlowName = "B",
            Environment = flowA.Environment
        });
        flowB.Actions.Add(new SubFlowAction
        {
            TargetFlowName = "A",
            Environment = flowB.Environment
        });

        var errors = FlowValidator.ValidateFlow(flowA).ToList();

        var error = Assert.Single(errors);
        Assert.Equal(SyntaxErrorSeverity.Critical, error.Severity);
        Assert.Equal("Flow.Actions[0]", error.ActionPath);
        Assert.Contains("Circular reference detected", error.Message);
    }

    [Fact]
    public void ValidateSyntax_WithValidTarget_ReturnsNoErrors()
    {
        var (project, root) = CreateProjectWithFlow("root");
        var child = new TestFlow
        {
            Name = "child",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment(),
            ParentProject = project
        };
        project.TestFlows!.Add(child);

        var action = new SubFlowAction
        {
            TargetFlowName = "child",
            Environment = root.Environment
        };

        var errors = action.ValidateSyntax(CreateContext(project, root)).ToList();

        Assert.Empty(errors);
    }

    private static (TestProject Project, TestFlow Flow) CreateProjectWithFlow(string flowName)
    {
        var project = new TestProject
        {
            Name = "project"
        };
        var flow = new TestFlow
        {
            Name = flowName,
            Environment = new FleetAutomate.Model.Actions.Logic.Environment(),
            ParentProject = project
        };
        project.TestFlows!.Add(flow);
        return (project, flow);
    }

    private static SyntaxValidationContext CreateContext(TestProject project, TestFlow flow)
    {
        return new SyntaxValidationContext
        {
            Environment = flow.Environment,
            CurrentPath = "Flow.Actions[0]",
            Parent = flow,
            TestProject = project,
            CurrentFlow = flow
        };
    }
}
