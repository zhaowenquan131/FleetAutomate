using System.Reflection;
using System.Text.Json;

namespace FleetAutomate.Cli.Output;

internal enum OutputFormat
{
    Table,
    Json
}

internal sealed class CliOutputWriter
{
    private readonly OutputFormat _format;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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
            Console.WriteLine(JsonSerializer.Serialize(new { ok = false, errorCode = code, message }, JsonOptions));
        }
        else
        {
            Console.Error.WriteLine($"{code}: {message}");
        }
    }
}
