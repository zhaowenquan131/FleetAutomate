using FleetAutomate.Application.Commanding;

namespace FleetAutomate.Cli.Infrastructure;

internal interface IUiSessionDiscovery
{
    Task<IReadOnlyList<UiSessionDescriptor>> DiscoverAsync(string? projectPath, CancellationToken cancellationToken = default);
}

internal interface IUiSessionClient
{
    Task<CommandResult> SendAsync(UiSessionDescriptor session, CommandEnvelope command, CancellationToken cancellationToken = default);
}

internal interface IOfflineCommandExecutor
{
    Task<CommandResult> ExecuteAsync(CommandEnvelope command, CancellationToken cancellationToken = default);
}

internal sealed class CliCommandRouter
{
    private readonly IUiSessionClient _sessionClient;
    private readonly IUiSessionDiscovery _sessionDiscovery;
    private readonly IOfflineCommandExecutor _offlineExecutor;

    public CliCommandRouter(IUiSessionClient sessionClient, IUiSessionDiscovery sessionDiscovery, IOfflineCommandExecutor offlineExecutor)
    {
        _sessionClient = sessionClient;
        _sessionDiscovery = sessionDiscovery;
        _offlineExecutor = offlineExecutor;
    }

    public async Task<CommandResult> RouteAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
    {
        var session = await FindBestSessionAsync(command.ProjectPath, cancellationToken);
        if (session == null)
        {
            return await _offlineExecutor.ExecuteAsync(command, cancellationToken);
        }

        try
        {
            var result = await _sessionClient.SendAsync(session, command, cancellationToken);
            if (result.Ok || string.Equals(result.Error?.Code, "BUSY", StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }
        }
        catch (IOException)
        {
        }
        catch (TimeoutException)
        {
        }

        return await _offlineExecutor.ExecuteAsync(command, cancellationToken);
    }

    private async Task<UiSessionDescriptor?> FindBestSessionAsync(string? projectPath, CancellationToken cancellationToken)
    {
        var sessions = await _sessionDiscovery.DiscoverAsync(projectPath, cancellationToken);
        if (sessions.Count == 0)
        {
            return null;
        }

        var normalizedProjectPath = UiSessionRegistry.NormalizeProjectPath(projectPath);
        return sessions
            .Where(session =>
                string.IsNullOrEmpty(normalizedProjectPath) ||
                string.Equals(UiSessionRegistry.NormalizeProjectPath(session.ProjectPath), normalizedProjectPath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(session => session.LastActiveUtc)
            .FirstOrDefault();
    }
}
