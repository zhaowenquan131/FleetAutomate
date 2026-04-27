using System.Diagnostics;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Tests.Actions;

public sealed class WaitDurationActionTests
{
    [Fact]
    public async Task ExecuteAsync_WaitsWithoutBlockingCallerThread()
    {
        var action = new WaitDurationAction
        {
            Duration = 1,
            Unit = WaitDurationUnit.Seconds
        };

        var stopwatch = Stopwatch.StartNew();
        var execution = action.ExecuteAsync(CancellationToken.None);

        Assert.False(execution.IsCompleted);
        Assert.True(stopwatch.ElapsedMilliseconds < 500);

        var result = await execution;

        Assert.True(result);
        Assert.Equal(ActionState.Completed, action.State);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(900));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationSetsPausedState()
    {
        var action = new WaitDurationAction
        {
            Duration = 1,
            Unit = WaitDurationUnit.Minutes
        };
        using var cancellation = new CancellationTokenSource();

        var execution = action.ExecuteAsync(cancellation.Token);
        cancellation.CancelAfter(50);

        var result = await execution;

        Assert.False(result);
        Assert.Equal(ActionState.Paused, action.State);
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesRemainingTimeWhileRunning()
    {
        var action = new WaitDurationAction
        {
            Duration = 1,
            Unit = WaitDurationUnit.Seconds
        };
        var remainingUpdates = new List<TimeSpan>();
        action.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WaitDurationAction.RemainingTime))
            {
                remainingUpdates.Add(action.RemainingTime);
            }
        };

        var execution = action.ExecuteAsync(CancellationToken.None);
        await Task.Delay(250);

        Assert.Equal(ActionState.Running, action.State);
        Assert.NotEmpty(remainingUpdates);
        Assert.InRange(action.RemainingTime, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        Assert.Contains("remaining", action.RemainingText, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(action.Progress, 0.01, 0.99);

        Assert.True(await execution);
        Assert.Equal(TimeSpan.Zero, action.RemainingTime);
        Assert.Equal(1, action.Progress);
    }

    [Fact]
    public async Task ExecuteAsync_AfterPause_ContinuesForRemainingDuration()
    {
        var action = new WaitDurationAction
        {
            Duration = 1,
            Unit = WaitDurationUnit.Seconds
        };
        using var cancellation = new CancellationTokenSource();

        var firstRun = action.ExecuteAsync(cancellation.Token);
        await Task.Delay(350);
        cancellation.Cancel();

        Assert.False(await firstRun);
        var pausedRemaining = action.RemainingTime;
        Assert.Equal(ActionState.Paused, action.State);
        Assert.InRange(pausedRemaining, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(900));

        var stopwatch = Stopwatch.StartNew();
        var result = await action.ExecuteAsync(CancellationToken.None);
        stopwatch.Stop();

        Assert.True(result);
        Assert.Equal(ActionState.Completed, action.State);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(900));
        Assert.True(stopwatch.Elapsed >= pausedRemaining - TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void WaitDurationAction_IsCooperativelyPauseAware()
    {
        var action = new WaitDurationAction();

        var pauseAware = Assert.IsAssignableFrom<IPauseAwareAction>(action);
        Assert.Equal(ActionPauseBehavior.Cooperative, pauseAware.PauseBehavior);
    }

    [Fact]
    public void Description_ReflectsDurationAndUnit()
    {
        var action = new WaitDurationAction
        {
            Duration = 2,
            Unit = WaitDurationUnit.Minutes
        };

        Assert.Equal("Wait for 2 minutes", action.Description);
    }
}
