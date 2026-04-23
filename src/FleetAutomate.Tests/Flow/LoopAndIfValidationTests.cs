using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Expression;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Tests.Flow;

public sealed class LoopAndIfValidationTests
{
    [Fact]
    public void ValidateFlow_IfActionWithNullCondition_ReturnsCriticalError()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new IfAction
        {
            Condition = null!,
            Environment = flow.Environment
        });

        var error = Assert.Single(flow.ValidateSyntax());

        Assert.Equal(SyntaxErrorSeverity.Critical, error.Severity);
        Assert.Equal("Condition", error.PropertyName);
        Assert.Equal("Flow.Actions[0]", error.ActionPath);
        Assert.Equal("If condition cannot be null", error.Message);
    }

    [Fact]
    public void ValidateFlow_IfActionWithInvalidConditionType_ReturnsCriticalError()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new IfAction
        {
            Condition = 123,
            Environment = flow.Environment
        });

        var error = Assert.Single(flow.ValidateSyntax());

        Assert.Equal(SyntaxErrorSeverity.Critical, error.Severity);
        Assert.Equal("Condition", error.PropertyName);
        Assert.Equal("Flow.Actions[0]", error.ActionPath);
        Assert.Equal("Int32", error.Context);
        Assert.Equal("If condition must be a boolean value or Expression<bool>", error.Message);
    }

    [Fact]
    public void ValidateFlow_IfActionWithBooleanCondition_DoesNotReturnConditionErrors()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new IfAction
        {
            Condition = true,
            Environment = flow.Environment
        });

        var errors = flow.ValidateSyntax().ToList();

        Assert.DoesNotContain(errors, error => error.PropertyName == "Condition");
    }

    [Fact]
    public void ValidateFlow_IfActionWithExpressionCondition_DoesNotReturnConditionErrors()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new IfAction
        {
            Condition = new LiteralExpression<bool>(true),
            Environment = flow.Environment
        });

        var errors = flow.ValidateSyntax().ToList();

        Assert.DoesNotContain(errors, error => error.PropertyName == "Condition");
    }

    [Fact]
    public void ValidateFlow_WhileLoopWithNullCondition_ReturnsCriticalError()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new WhileLoopAction
        {
            Condition = null!,
            Environment = flow.Environment,
            Name = "while",
            Description = "desc"
        });

        var errors = flow.ValidateSyntax().ToList();

        Assert.Contains(errors, error =>
            error.Severity == SyntaxErrorSeverity.Critical &&
            error.PropertyName == "Condition" &&
            error.ActionPath == "Flow.Actions[0]" &&
            error.Message == "While loop condition cannot be null");
    }

    [Fact]
    public void ValidateFlow_WhileLoopWithInvalidConditionType_ReturnsCriticalError()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new WhileLoopAction
        {
            Condition = "bad",
            Environment = flow.Environment,
            Name = "while",
            Description = "desc"
        });

        var errors = flow.ValidateSyntax().ToList();

        Assert.Contains(errors, error =>
            error.Severity == SyntaxErrorSeverity.Critical &&
            error.PropertyName == "Condition" &&
            error.ActionPath == "Flow.Actions[0]" &&
            Equals(error.Context, typeof(string)) &&
            error.Message.Contains("Expression<bool>"));
    }

    [Fact]
    public void ValidateFlow_WhileLoopWithEmptyBody_ReturnsWarning()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new WhileLoopAction
        {
            Condition = true,
            Environment = flow.Environment,
            Name = "while",
            Description = "desc"
        });

        var error = Assert.Single(flow.ValidateSyntax());

        Assert.Equal(SyntaxErrorSeverity.Warning, error.Severity);
        Assert.Equal("Body", error.PropertyName);
        Assert.Equal("Flow.Actions[0]", error.ActionPath);
        Assert.Contains("body is empty", error.Message);
    }

    [Fact]
    public void ValidateFlow_WhileLoopWithExpressionConditionAndBody_DoesNotReturnLoopErrors()
    {
        var flow = CreateFlow();
        var loop = new WhileLoopAction
        {
            Condition = new LiteralExpression<bool>(true),
            Environment = flow.Environment,
            Name = "while",
            Description = "desc"
        };
        loop.Body.Add(new RecordingAction("step"));
        flow.Actions.Add(loop);

        var errors = flow.ValidateSyntax().ToList();

        Assert.DoesNotContain(errors, error => error.ActionPath == "Flow.Actions[0]");
    }

    [Fact]
    public void ValidateFlow_ForLoopWithNullCondition_ReturnsCriticalError()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new ForLoopAction
        {
            Condition = null!,
            Environment = flow.Environment
        });

        var errors = flow.ValidateSyntax().ToList();

        Assert.Contains(errors, error =>
            error.PropertyName == "Condition" &&
            error.Severity == SyntaxErrorSeverity.Critical &&
            error.ActionPath == "Flow.Actions[0]");
    }

    [Fact]
    public void ValidateFlow_ForLoopWithInvalidConditionType_ReturnsCriticalError()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new ForLoopAction
        {
            Condition = "bad",
            Environment = flow.Environment
        });

        var errors = flow.ValidateSyntax().ToList();

        Assert.Contains(errors, error =>
            error.PropertyName == "Condition" &&
            error.Severity == SyntaxErrorSeverity.Critical &&
            error.ActionPath == "Flow.Actions[0]" &&
            Equals(error.Context, typeof(string)));
    }

    [Fact]
    public void ValidateFlow_ForLoopWithInvalidInitialization_ReturnsError()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new ForLoopAction
        {
            Condition = false,
            Initialization = 123,
            Environment = flow.Environment
        });

        var errors = flow.ValidateSyntax().ToList();

        Assert.Contains(errors, error =>
            error.PropertyName == "Initialization" &&
            error.Severity == SyntaxErrorSeverity.Error &&
            error.ActionPath == "Flow.Actions[0]" &&
            Equals(error.Context, typeof(int)));
    }

    [Fact]
    public void ValidateFlow_ForLoopWithInvalidIncrement_ReturnsError()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new ForLoopAction
        {
            Condition = false,
            Increment = 123,
            Environment = flow.Environment
        });

        var errors = flow.ValidateSyntax().ToList();

        Assert.Contains(errors, error =>
            error.PropertyName == "Increment" &&
            error.Severity == SyntaxErrorSeverity.Error &&
            error.ActionPath == "Flow.Actions[0]" &&
            Equals(error.Context, typeof(int)));
    }

    [Fact]
    public void ValidateFlow_ForLoopWithEmptyBody_ReturnsWarning()
    {
        var flow = CreateFlow();
        flow.Actions.Add(new ForLoopAction
        {
            Condition = false,
            Environment = flow.Environment
        });

        var errors = flow.ValidateSyntax().ToList();

        Assert.Contains(errors, error =>
            error.PropertyName == "Body" &&
            error.Severity == SyntaxErrorSeverity.Warning &&
            error.ActionPath == "Flow.Actions[0]");
    }

    [Fact]
    public void ValidateFlow_ForLoopWithValidConfiguration_DoesNotReturnLoopErrors()
    {
        var flow = CreateFlow();
        var loop = new ForLoopAction
        {
            Condition = new LiteralExpression<bool>(false),
            Initialization = new RecordingAction("init"),
            Increment = new RecordingAction("inc"),
            Environment = flow.Environment
        };
        loop.Body.Add(new RecordingAction("body"));
        flow.Actions.Add(loop);

        var errors = flow.ValidateSyntax().ToList();

        Assert.DoesNotContain(errors, error => error.ActionPath == "Flow.Actions[0]");
    }

    private static TestFlow CreateFlow()
    {
        return new TestFlow
        {
            Name = "flow",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };
    }
}
