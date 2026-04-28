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
        var context = new ExpressionContext(
            new FleetAutomate.Model.Actions.Logic.Environment(),
            new FakeExpressionUiQueryService
            {
                PropertyResult = "calculator window",
                CountResult = 1
            });

        var result = await engine.EvaluateAsync(
            "(getUiProperty(\"Window/Pane/Button\", \"Name\").ContainsText(\"window\")) && uiCount(\"Window\") >= 1",
            context,
            CancellationToken.None);

        Assert.True((bool)result.Value!);
    }

    [Fact]
    public async Task EvaluateAsync_SupportsEscapedQuotesInStringLiterals()
    {
        var engine = new SimpleExpressionEngine();

        var result = await engine.EvaluateAsync(
            "uiContainsText(\"//Pane[@Name=\\\"桌面 1\\\"]//Window[@Name=\\\"计算器\\\"]//Window[@Name=\\\"计算器\\\"]\", \"标准\")",
            ExpressionContext.Empty,
            CancellationToken.None);

        Assert.False((bool)result.Value!);
    }

    [Fact]
    public async Task EvaluateAsync_SupportsSingleQuotedXpathWithDoubleQuotedPredicates()
    {
        var engine = new SimpleExpressionEngine();

        var result = await engine.EvaluateAsync(
            "uiContainsText('//Pane[@Name=\"桌面 1\"]//Window[@Name=\"计算器\"]//Window[@Name=\"计算器\"]', \"标准\")",
            ExpressionContext.Empty,
            CancellationToken.None);

        Assert.False((bool)result.Value!);
    }

    [Fact]
    public async Task EvaluateAsync_UiContainsTextUsesUiQueryService()
    {
        var engine = new SimpleExpressionEngine();
        var uiQuery = new FakeExpressionUiQueryService
        {
            ContainsTextResult = true
        };
        var context = new ExpressionContext(new FleetAutomate.Model.Actions.Logic.Environment(), uiQuery);

        var result = await engine.EvaluateAsync(
            "uiContainsText('//Window[@Name=\"Calculator\"]', \"Standard\")",
            context,
            CancellationToken.None);

        Assert.True((bool)result.Value!);
        Assert.Equal("//Window[@Name=\"Calculator\"]", uiQuery.LastElementPath);
        Assert.Equal("Standard", uiQuery.LastText);
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
    public async Task SetVariableAction_ExpressionRightValueInfersResultType()
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
        Assert.Equal(12d, variable.Value);
        Assert.Equal(typeof(double), variable.Type);
    }

    private sealed class FakeExpressionUiQueryService : IExpressionUiQueryService
    {
        public bool ContainsTextResult { get; set; }

        public string? PropertyResult { get; set; }

        public int CountResult { get; set; }

        public string? LastElementPath { get; private set; }

        public string? LastText { get; private set; }

        public bool Exists(string elementPath) => false;

        public bool ContainsText(string elementPath, string text)
        {
            LastElementPath = elementPath;
            LastText = text;
            return ContainsTextResult;
        }

        public string? GetProperty(string elementPath, string propertyName) => PropertyResult;

        public int Count(string elementPath) => CountResult;
    }
}
