using FleetAutomate.Expressions;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Tests.Actions;

public sealed class SystemActionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"FleetAutomateSystemTests_{Guid.NewGuid():N}");

    public SystemActionTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task IfProcessExistsAction_DetectsCurrentProcess()
    {
        var processName = global::System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        var action = new IfProcessExistsAction { ProcessName = processName };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.True(action.Exists);
        Assert.Equal(ActionState.Completed, action.State);
    }

    [Fact]
    public async Task KillProcessAction_FailsWhenProcessIsMissingAndConfiguredToFail()
    {
        var action = new KillProcessAction
        {
            ProcessName = $"missing_{Guid.NewGuid():N}",
            SucceedIfNotFound = false
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.False(result);
        Assert.Equal(ActionState.Failed, action.State);
    }

    [Fact]
    public async Task GetScreenshotAction_WritesImageFile()
    {
        var path = Path.Combine(_root, "screen.png");
        var action = new GetScreenshotAction { FilePath = path };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
        Assert.Equal(ActionState.Completed, action.State);
    }

    [Fact]
    public async Task SetClipboardAction_SetsTextOnStaThread()
    {
        var action = new SetClipboardAction { Text = $"clipboard-{Guid.NewGuid():N}" };
        bool result = false;
        string clipboardText = string.Empty;
        Exception? threadFailure = null;
        bool clipboardUnavailable = false;

        var thread = new Thread(() =>
        {
            try
            {
                if (!CanAccessClipboard())
                {
                    clipboardUnavailable = true;
                    return;
                }

                result = action.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();
                clipboardText = ReadClipboardTextWithRetry();
            }
            catch (Exception ex)
            {
                threadFailure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (clipboardUnavailable)
        {
            return;
        }

        Assert.True(result);
        Assert.Equal(ActionState.Completed, action.State);
        if (threadFailure != null)
        {
            Assert.IsType<InvalidOperationException>(threadFailure);
            Assert.IsType<System.Runtime.InteropServices.ExternalException>(threadFailure.InnerException);
            return;
        }

        Assert.Equal(action.Text, clipboardText);
    }

    [Fact]
    public async Task LogAction_LiteralModeResolvesBraceVariables()
    {
        var action = new LogAction
        {
            Message = "Count: {count}",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment
            {
                Variables = [new Variable("count", 3, typeof(int))]
            },
            UiQueryService = new FakeExpressionUiQueryService { PropertyResult = "window button" }
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal("Count: 3", action.LastResolvedMessage);
        Assert.Equal(ActionState.Completed, action.State);
    }

    [Fact]
    public async Task LogAction_ExpressionModeInterpolatesExpressions()
    {
        var action = new LogAction
        {
            MessageMode = LogMessageMode.Expression,
            Message = "window contains target text: {getUiProperty(\"Window/Pane/Button\", \"Name\").ContainsText(\"window\")}, count: {count + 2}",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment
            {
                Variables = [new Variable("count", 3, typeof(int))]
            },
            UiQueryService = new FakeExpressionUiQueryService { PropertyResult = "window button" }
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal("window contains target text: True, count: 5", action.LastResolvedMessage);
        Assert.Equal(ActionState.Completed, action.State);
    }

    [Fact]
    public async Task LogAction_ExpressionModeEvaluatesWholeExpressionWhenNoInterpolationMarkers()
    {
        var action = new LogAction
        {
            MessageMode = LogMessageMode.Expression,
            Message = "getUiProperty(\"Window/Pane/Button\", \"Name\").ContainsText(\"window\")",
            UiQueryService = new FakeExpressionUiQueryService { PropertyResult = "window button" }
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal("True", action.LastResolvedMessage);
        Assert.Equal(ActionState.Completed, action.State);
    }

    private static bool CanAccessClipboard()
    {
        try
        {
            var probe = $"probe-{Guid.NewGuid():N}";
            System.Windows.Forms.Clipboard.SetText(probe);
            return ReadClipboardTextWithRetry() == probe;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return false;
        }
        catch (InvalidOperationException ex) when (ex.InnerException is System.Runtime.InteropServices.ExternalException)
        {
            return false;
        }
    }

    private static string ReadClipboardTextWithRetry()
    {
        Exception? lastFailure = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                return System.Windows.Forms.Clipboard.GetText();
            }
            catch (System.Runtime.InteropServices.ExternalException ex)
            {
                lastFailure = ex;
                Thread.Sleep(50);
            }
        }

        throw new InvalidOperationException("Clipboard text could not be read after retries.", lastFailure);
    }

    private sealed class FakeExpressionUiQueryService : IExpressionUiQueryService
    {
        public string? PropertyResult { get; set; }

        public bool Exists(string elementPath) => false;

        public bool Exists(string elementPath, string identifierType, int retryTimes) => false;

        public bool ContainsText(string elementPath, string text) => false;

        public string? GetProperty(string elementPath, string propertyName) => PropertyResult;

        public int Count(string elementPath) => 0;
    }
}
