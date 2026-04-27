using System.Diagnostics;
using System.Runtime.Serialization;

namespace FleetAutomate.Model.Actions.Scripts;

[DataContract]
public class RunCommandAction : ActionBase
{
    private string _command = string.Empty;
    private string _arguments = string.Empty;
    private string _workingDirectory = string.Empty;
    private int _timeoutMilliseconds = 30000;
    private Process? _process;

    public override string Name => "Run CMD";
    protected override string DefaultDescription => "Execute command";

    [DataMember]
    public string Command
    {
        get => _command;
        set => SetProperty(ref _command, value);
    }

    [DataMember]
    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    [DataMember]
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetProperty(ref _workingDirectory, value);
    }

    [DataMember]
    public int TimeoutMilliseconds
    {
        get => _timeoutMilliseconds;
        set => SetProperty(ref _timeoutMilliseconds, Math.Max(1, value));
    }

    [IgnoreDataMember]
    public int? ExitCode { get; private set; }

    [IgnoreDataMember]
    public string StandardOutput { get; private set; } = string.Empty;

    [IgnoreDataMember]
    public string StandardError { get; private set; } = string.Empty;

    public override void Cancel()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    protected override async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Command))
        {
            throw new InvalidOperationException("Command cannot be empty.");
        }

        var info = new ProcessStartInfo
        {
            FileName = Command,
            Arguments = Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? Environment.CurrentDirectory : WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _process = Process.Start(info) ?? throw new InvalidOperationException($"Failed to start '{Command}'.");
        using var timeout = new CancellationTokenSource(TimeoutMilliseconds);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        var stdoutTask = _process.StandardOutput.ReadToEndAsync(linked.Token);
        var stderrTask = _process.StandardError.ReadToEndAsync(linked.Token);
        await _process.WaitForExitAsync(linked.Token);
        StandardOutput = await stdoutTask;
        StandardError = await stderrTask;
        ExitCode = _process.ExitCode;

        if (ExitCode != 0)
        {
            throw new InvalidOperationException($"Command exited with code {ExitCode}.");
        }
    }
}

[DataContract]
public sealed class RunPowerShellCommandAction : RunCommandAction
{
    private string _script = string.Empty;

    public override string Name => "Run PowerShell Command";

    [DataMember]
    public string Script
    {
        get => _script;
        set
        {
            if (SetProperty(ref _script, value))
            {
                Command = "powershell.exe";
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{value.Replace("\"", "\\\"")}\"";
            }
        }
    }
}

[DataContract]
public sealed class RunBatchScriptAction : RunCommandAction
{
    private string _scriptPath = string.Empty;

    public override string Name => "Run Batch Script";

    [DataMember]
    public string ScriptPath
    {
        get => _scriptPath;
        set
        {
            if (SetProperty(ref _scriptPath, value))
            {
                Command = "cmd.exe";
                Arguments = $"/c \"{value}\"";
            }
        }
    }
}

[DataContract]
public sealed class RunPowerShellScriptAction : RunCommandAction
{
    private string _scriptPath = string.Empty;

    public override string Name => "Run PowerShell Script";

    [DataMember]
    public string ScriptPath
    {
        get => _scriptPath;
        set
        {
            if (SetProperty(ref _scriptPath, value))
            {
                Command = "powershell.exe";
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{value}\"";
            }
        }
    }
}

[DataContract]
public sealed class RunPythonScriptAction : RunCommandAction
{
    private string _scriptPath = string.Empty;
    private string _pythonExecutable = "python";

    public override string Name => "Run Python Script";

    [DataMember]
    public string ScriptPath
    {
        get => _scriptPath;
        set
        {
            if (SetProperty(ref _scriptPath, value))
            {
                Command = PythonExecutable;
                Arguments = $"\"{value}\"";
            }
        }
    }

    [DataMember]
    public string PythonExecutable
    {
        get => _pythonExecutable;
        set
        {
            if (SetProperty(ref _pythonExecutable, string.IsNullOrWhiteSpace(value) ? "python" : value))
            {
                Command = _pythonExecutable;
                Arguments = string.IsNullOrWhiteSpace(ScriptPath) ? Arguments : $"\"{ScriptPath}\"";
            }
        }
    }
}
