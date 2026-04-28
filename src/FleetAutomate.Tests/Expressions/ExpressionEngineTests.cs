using FleetAutomate.Expressions;
using FleetAutomate.Model.Actions.Logic;

namespace FleetAutomate.Tests.Expressions;

public class ExpressionEngineTests
{
    [Fact]
    public async Task EvaluateAsync_CalculatesArithmeticAndVariableExpressions()
    {
        var context = new ExpressionContext(new FleetAutomate.Model.Actions.Logic.Environment
        {
            Variables = [new Variable("x", 4, typeof(int))]
        });
        var engine = new SimpleExpressionEngine();

        var result = await engine.EvaluateAsync("x * 2 + 3", context, CancellationToken.None);

        Assert.Equal(11d, result.Value);
        Assert.Equal(typeof(double), result.ResultType);
    }

    [Fact]
    public async Task EvaluateAsync_CalculatesLogicalExpressions()
    {
        var context = new ExpressionContext(new FleetAutomate.Model.Actions.Logic.Environment
        {
            Variables =
            [
                new Variable("count", 3, typeof(int)),
                new Variable("ready", true, typeof(bool))
            ]
        });
        var engine = new SimpleExpressionEngine();

        var result = await engine.EvaluateAsync("ready && count >= 3", context, CancellationToken.None);

        Assert.True((bool)result.Value!);
        Assert.Equal(typeof(bool), result.ResultType);
    }

    [Fact]
    public void Validate_ReportsSyntaxErrors()
    {
        var engine = new SimpleExpressionEngine();

        var validation = engine.Validate("(1 +", ExpressionContext.Empty);

        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Errors);
    }

    [Fact]
    public async Task EvaluateAsync_SupportsParenthesesAndStringMethodChains()
    {
        var engine = new SimpleExpressionEngine();

        var result = await engine.EvaluateAsync(
            "(getUiProperty(\"Window/Pane/Button\", \"Name\").ContainsText(\"window\")) && uiCount(\"Window\") >= 1",
            ExpressionContext.Empty,
            CancellationToken.None);

        Assert.True((bool)result.Value!);
    }

    [Fact]
    public async Task EvaluateAsync_SupportsDateTimeComparisonFunctions()
    {
        var engine = new SimpleExpressionEngine();

        var later = await engine.EvaluateAsync("isNowLaterThan(\"2000-01-01T00:00:00Z\")", ExpressionContext.Empty, CancellationToken.None);
        var earlier = await engine.EvaluateAsync("isNowEarlierThan(\"2999-01-01T00:00:00Z\")", ExpressionContext.Empty, CancellationToken.None);

        Assert.True((bool)later.Value!);
        Assert.True((bool)earlier.Value!);
    }

    [Fact]
    public async Task SetVariableAction_UsesExpressionRightValue()
    {
        var environment = new FleetAutomate.Model.Actions.Logic.Environment
        {
            Variables = [new Variable("x", 5, typeof(int))]
        };
        var action = new SetVariableAction<int>("result", 0)
        {
            Environment = environment,
            ValueMode = SetVariableValueMode.Expression,
            ExpressionText = "x + 7"
        };

        var ok = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(ok);
        var variable = Assert.Single(environment.Variables, v => v.Name == "result");
        Assert.Equal(12, variable.Value);
        Assert.Equal(typeof(int), variable.Type);
    }
}
