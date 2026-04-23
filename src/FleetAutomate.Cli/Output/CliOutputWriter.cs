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
