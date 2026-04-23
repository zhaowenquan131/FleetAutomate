using FleetAutomate.Cli.Output;
using FleetAutomate.Application.Commanding;

namespace FleetAutomate.Cli.Infrastructure;

internal static class CliProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        var parser = new CliArgumentParser(args);
        if (parser.IsHelpRequested || parser.Positionals.Count < 2)
        {
            Console.WriteLine(HelpText.Value);
            return 0;
        }

        var format = parser.GetOption("format")?.Equals("json", StringComparison.OrdinalIgnoreCase) == true
            ? OutputFormat.Json
            : OutputFormat.Table;

        var writer = new CliOutputWriter(format);

        try
        {
            var projectPath = parser.GetRequiredOption("project");
            var resource = parser.Positionals[0].ToLowerInvariant();
            var verb = parser.Positionals[1].ToLowerInvariant();
            var command = CliCommandDispatcher.BuildEnvelope(resource, verb, projectPath, parser);
            var router = new CliCommandRouter(
                new NamedPipeUiSessionClient(),
                new FileSystemUiSessionDiscovery(),
                new OfflineCliCommandExecutor());
            var result = await router.RouteAsync(command);
            writer.WriteCommandResult(result);
            return result.Ok ? 0 : 1;
        }
        catch (CliUsageException ex)
        {
            writer.WriteError("USAGE_ERROR", ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            writer.WriteError("UNEXPECTED_ERROR", ex.Message);
            return 1;
        }
    }
}

internal sealed class CliUsageException : Exception
{
    public CliUsageException(string message) : base(message)
    {
    }
}

