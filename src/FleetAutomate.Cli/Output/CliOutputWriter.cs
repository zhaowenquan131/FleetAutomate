using System.Reflection;
using System.Text.Json;
using FleetAutomate.Application.Commanding;

namespace FleetAutomate.Cli.Output;

internal enum OutputFormat
{
    Table,
    Json
}

internal sealed class CliOutputWriter
{
    private readonly OutputFormat _format;
    private bool _isTestFlowRunStreaming;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public CliOutputWriter(OutputFormat format)
    {
        _format = format;
    }

    public void WriteObject(object value)
    {
        if (_format == OutputFormat.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
            return;
        }

        foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            Console.WriteLine($"{property.Name}: {property.GetValue(value)}");
        }
    }

    public void WriteCommandResult(CommandResult result)
    {
        if (_format == OutputFormat.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = result.Ok,
                mode = result.Mode == CommandExecutionMode.UiSession ? "ui-session" : "offline",
                sessionId = result.SessionId,
                payload = result.Payload,
                error = result.Error == null ? null : new
                {
                    code = result.Error.Code,
                    message = result.Error.Message
                }
            }, JsonOptions));
            return;
        }

        if (result.Payload is TestFlowRunResult runResult)
        {
            WriteTestFlowRunResult(result, runResult, _isTestFlowRunStreaming);
            _isTestFlowRunStreaming = false;
            return;
        }

        Console.WriteLine($"ok: {result.Ok}");
        Console.WriteLine($"mode: {(result.Mode == CommandExecutionMode.UiSession ? "ui-session" : "offline")}");
        if (!string.IsNullOrWhiteSpace(result.SessionId))
        {
            Console.WriteLine($"sessionId: {result.SessionId}");
        }

        if (result.Payload != null)
        {
            WriteObject(result.Payload);
        }

        if (result.Error != null)
        {
            Console.Error.WriteLine($"{result.Error.Code}: {result.Error.Message}");
        }
    }

    public void WriteTestFlowStarted(string projectPath, string flowName)
    {
        if (_format == OutputFormat.Json)
        {
            return;
        }

        _isTestFlowRunStreaming = true;
        WriteColoredLine("=== TESTFLOW RUNNING ===", ConsoleColor.Cyan);
        Console.WriteLine($"projectPath: {projectPath}");
        Console.WriteLine($"flowName: {flowName}");
    }

    public void WriteActionExecutionStarted(string path, string name)
    {
        if (_format != OutputFormat.Json)
        {
            WriteColoredLine($"  [RUN ] {path} {name}", ConsoleColor.Cyan);
        }
    }

    public void WriteActionExecutionCompleted(ActionExecutionRecord action)
    {
        if (_format == OutputFormat.Json)
        {
            return;
        }

        var (label, color) = action.State switch
        {
            "Completed" => ("PASS", ConsoleColor.Green),
            "Failed" => ("FAIL", ConsoleColor.Red),
            "Paused" => ("PAUSED", ConsoleColor.Yellow),
            _ => (action.State.ToUpperInvariant(), ConsoleColor.Gray)
        };
        WriteColoredLine(
            $"  [{label}] {action.Path} {action.Name} ({action.DurationMilliseconds} ms)",
            color);
    }

    private void WriteTestFlowRunResult(CommandResult result, TestFlowRunResult runResult, bool actionLogWasStreamed)
    {
        WriteColoredLine(
            result.Ok ? "=== TESTFLOW SUCCEEDED ===" : "=== TESTFLOW FAILED ===",
            result.Ok ? ConsoleColor.Green : ConsoleColor.Red);
        Console.WriteLine($"mode: {(result.Mode == CommandExecutionMode.UiSession ? "ui-session" : "offline")}");
        Console.WriteLine($"projectPath: {runResult.ProjectPath}");
        Console.WriteLine($"flowName: {runResult.FlowName}");
        Console.WriteLine($"state: {runResult.State}");
        Console.WriteLine($"breakReason: {runResult.BreakReason}");
        Console.WriteLine($"failedAction: {runResult.FailedAction}");
        if (!actionLogWasStreamed)
        {
            Console.WriteLine("actions:");
            if (runResult.Actions.Count == 0)
            {
                Console.WriteLine("  (no actions)");
            }
            else
            {
                foreach (var action in runResult.Actions)
                {
                    WriteActionExecutionCompleted(action);
                }
            }
        }

        if (result.Error != null)
        {
            WriteColoredLine($"{result.Error.Code}: {result.Error.Message}", ConsoleColor.Red, Console.Error);
        }
    }

    private static void WriteColoredLine(string value, ConsoleColor color, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        if (Console.IsOutputRedirected || !ReferenceEquals(writer, Console.Out))
        {
            writer.WriteLine(value);
            return;
        }

        var previousColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            writer.WriteLine(value);
        }
        finally
        {
            Console.ForegroundColor = previousColor;
        }
    }

    public void WriteRows(IEnumerable<object> rows)
    {
        var materialized = rows.ToList();
        if (_format == OutputFormat.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(materialized, JsonOptions));
            return;
        }

        if (materialized.Count == 0)
        {
            Console.WriteLine("(empty)");
            return;
        }

        var properties = materialized[0].GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var headers = properties.Select(property => property.Name).ToArray();
        var data = materialized
            .Select(row => properties.Select(property => property.GetValue(row)?.ToString() ?? string.Empty).ToArray())
            .ToList();

        var widths = new int[headers.Length];
        for (var i = 0; i < headers.Length; i++)
        {
            widths[i] = Math.Max(headers[i].Length, data.Max(row => row[i].Length));
        }

        Console.WriteLine(string.Join("  ", headers.Select((header, i) => header.PadRight(widths[i]))));
        Console.WriteLine(string.Join("  ", widths.Select(width => new string('-', width))));

        foreach (var row in data)
        {
            Console.WriteLine(string.Join("  ", row.Select((value, i) => value.PadRight(widths[i]))));
        }
    }

    public void WriteError(string code, string message)
    {
        if (_format == OutputFormat.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = new { code, message } }, JsonOptions));
        }
        else
        {
            Console.Error.WriteLine($"{code}: {message}");
        }
    }
}
