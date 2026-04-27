using FleetAutomate.Model.Actions.System;
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
}
