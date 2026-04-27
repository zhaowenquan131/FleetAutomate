using FleetAutomate.Model.Actions.Scripts;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Tests.Actions;

public sealed class ScriptActionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"FleetAutomateScriptTests_{Guid.NewGuid():N}");

    public ScriptActionTests()
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
    public async Task RunCommandAction_CapturesStdoutAndExitCode()
    {
        var action = new RunCommandAction
        {
            Command = "cmd.exe",
            Arguments = "/c echo hello",
            WorkingDirectory = _root,
            TimeoutMilliseconds = 5000
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(0, action.ExitCode);
        Assert.Contains("hello", action.StandardOutput);
        Assert.Equal(ActionState.Completed, action.State);
    }

    [Fact]
    public async Task RunBatchScriptAction_ExecutesScriptFile()
    {
        var output = Path.Combine(_root, "out.txt");
        var script = Path.Combine(_root, "write.bat");
        await File.WriteAllTextAsync(script, $"@echo off{Environment.NewLine}echo batch>{output}");
        var action = new RunBatchScriptAction
        {
            ScriptPath = script,
            TimeoutMilliseconds = 5000
        };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal("batch", (await File.ReadAllTextAsync(output)).Trim());
        Assert.Equal(ActionState.Completed, action.State);
    }
}
