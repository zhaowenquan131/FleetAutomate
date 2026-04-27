using FleetAutomate.Model.Actions.Text;

namespace FleetAutomate.Tests.Actions;

public sealed class TextActionTests
{
    [Fact]
    public async Task ChangeTextCaseAction_ConvertsText()
    {
        var action = new ChangeTextCaseAction
        {
            Text = "Fleet Automate",
            Case = TextCase.Upper
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal("FLEET AUTOMATE", action.Result);
    }

    [Fact]
    public async Task ReplaceTextAction_ReplacesAllMatches()
    {
        var action = new ReplaceTextAction
        {
            Text = "one two one",
            SearchText = "one",
            ReplacementText = "three"
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal("three two three", action.Result);
    }

    [Fact]
    public async Task SubstringAction_ReturnsRequestedRange()
    {
        var action = new SubstringAction
        {
            Text = "automation",
            StartIndex = 4,
            Length = 3
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal("mat", action.Result);
    }
}
