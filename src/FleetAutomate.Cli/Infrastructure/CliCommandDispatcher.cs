using FleetAutomate.Cli.Output;
using FleetAutomate.Application.Commanding;

namespace FleetAutomate.Cli.Infrastructure;

internal sealed class CliCommandDispatcher
{
    private readonly CliOutputWriter _writer;
    private readonly OfflineCliCommandExecutor _offlineExecutor = new();

    public CliCommandDispatcher(CliOutputWriter writer)
    {
        _writer = writer;
    }

    public async Task<int> DispatchAsync(string resource, string verb, string projectPath, CliArgumentParser parser)
    {
        var command = BuildEnvelope(resource, verb, projectPath, parser);
        var result = await _offlineExecutor.ExecuteAsync(command);
        _writer.WriteCommandResult(result);
        return result.Ok ? 0 : 1;
    }

    internal static CommandEnvelope BuildEnvelope(string resource, string verb, string projectPath, CliArgumentParser parser)
    {
        var arguments = parser.GetAllOptions();
        return new CommandEnvelope(
            Command: $"{resource}.{verb}",
            Arguments: arguments,
            ProjectPath: projectPath,
            RequestId: Guid.NewGuid().ToString("N"));
    }
}
