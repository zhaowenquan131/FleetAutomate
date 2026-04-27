using FleetAutomate.Model.Actions.DateAndTime;

namespace FleetAutomate.Tests.Actions;

public sealed class DateAndTimeActionTests
{
    [Fact]
    public async Task GetCurrentDateTimeAction_SetsCurrentValue()
    {
        var before = DateTimeOffset.Now;
        var action = new GetCurrentDateTimeAction();

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.InRange(action.Value, before.AddSeconds(-1), DateTimeOffset.Now.AddSeconds(1));
    }

    [Fact]
    public async Task FormatDateTimeAction_FormatsInput()
    {
        var action = new FormatDateTimeAction
        {
            Value = new DateTimeOffset(2026, 4, 27, 13, 14, 15, TimeSpan.FromHours(8)),
            Format = "yyyy-MM-dd HH:mm"
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal("2026-04-27 13:14", action.Result);
    }

    [Fact]
    public async Task AddDateTimeAction_AddsSelectedUnit()
    {
        var action = new AddDateTimeAction
        {
            Value = new DateTimeOffset(2026, 4, 27, 0, 0, 0, TimeSpan.Zero),
            Amount = 2,
            Unit = DateTimeUnit.Days
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero), action.Result);
    }
}
