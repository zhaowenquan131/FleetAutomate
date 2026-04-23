using FleetAutomate.Cli.Infrastructure;
using FleetAutomate.Application.Commanding;

namespace FleetAutomate.Tests.Cli;

public sealed class CliCommandRouterTests
{
    [Fact]
    public async Task RouteAsync_UsesUiSession_WhenMatchingWritableSessionExists()
    {
        var uiResult = CommandResult.Success(
            mode: CommandExecutionMode.UiSession,
            payload: new Dictionary<string, object?>
            {
                ["flowName"] = "calculator_flow"
            },
            sessionId: "session-123");

        var session = new UiSessionDescriptor(
            SessionId: "session-123",
            ProcessId: 1234,
            ProjectPath: @"D:\demo\sample.testproj",
            LastActiveUtc: DateTimeOffset.UtcNow,
            AllowsWrite: true,
            IsBusy: false);

        var router = new CliCommandRouter(
            new StubSessionClient(uiResult),
            new StubSessionDiscovery(session),
            new StubOfflineExecutor(CommandResult.Success(CommandExecutionMode.Offline, new Dictionary<string, object?>())));

        var result = await router.RouteAsync(new CommandEnvelope(
            Command: "testflow.create",
            Arguments: new Dictionary<string, string?>
            {
                ["name"] = "calculator_flow"
            },
            ProjectPath: @"D:\demo\sample.testproj",
            RequestId: "req-1"));

        Assert.True(result.Ok);
        Assert.Equal(CommandExecutionMode.UiSession, result.Mode);
        Assert.Equal("session-123", result.SessionId);
    }

    [Fact]
    public async Task RouteAsync_FallsBackToOffline_WhenNoMatchingSessionExists()
    {
        var offlineResult = CommandResult.Success(
            mode: CommandExecutionMode.Offline,
            payload: new Dictionary<string, object?>
            {
                ["projectPath"] = @"D:\demo\sample.testproj"
            });

        var router = new CliCommandRouter(
            new StubSessionClient(CommandResult.Success(CommandExecutionMode.UiSession, new Dictionary<string, object?>(), "unused")),
            new StubSessionDiscovery(),
            new StubOfflineExecutor(offlineResult));

        var result = await router.RouteAsync(new CommandEnvelope(
            Command: "project.save",
            Arguments: new Dictionary<string, string?>(),
            ProjectPath: @"D:\demo\sample.testproj",
            RequestId: "req-2"));

        Assert.True(result.Ok);
        Assert.Equal(CommandExecutionMode.Offline, result.Mode);
        Assert.Null(result.SessionId);
    }

    [Fact]
    public async Task RouteAsync_FallsBackToOffline_WhenSessionClientThrows()
    {
        var offlineResult = CommandResult.Success(
            mode: CommandExecutionMode.Offline,
            payload: new Dictionary<string, object?>
            {
                ["projectPath"] = @"D:\demo\sample.testproj"
            });

        var session = new UiSessionDescriptor(
            SessionId: "session-456",
            ProcessId: 4567,
            ProjectPath: @"D:\demo\sample.testproj",
            LastActiveUtc: DateTimeOffset.UtcNow,
            AllowsWrite: true,
            IsBusy: false);

        var router = new CliCommandRouter(
            new ThrowingSessionClient(),
            new StubSessionDiscovery(session),
            new StubOfflineExecutor(offlineResult));

        var result = await router.RouteAsync(new CommandEnvelope(
            Command: "project.save",
            Arguments: new Dictionary<string, string?>(),
            ProjectPath: @"D:\demo\sample.testproj",
            RequestId: "req-3"));

        Assert.True(result.Ok);
        Assert.Equal(CommandExecutionMode.Offline, result.Mode);
    }

    [Fact]
    public async Task RouteAsync_ReturnsBusyError_WhenMatchingSessionRejectsWriteBecauseBusy()
    {
        var session = new UiSessionDescriptor(
            SessionId: "session-busy",
            ProcessId: 9999,
            ProjectPath: @"D:\demo\sample.testproj",
            LastActiveUtc: DateTimeOffset.UtcNow,
            AllowsWrite: true,
            IsBusy: true);

        var router = new CliCommandRouter(
            new StubSessionClient(CommandResult.Failure(
                mode: CommandExecutionMode.UiSession,
                code: "BUSY",
                message: "execution in progress",
                sessionId: "session-busy")),
            new StubSessionDiscovery(session),
            new StubOfflineExecutor(CommandResult.Success(CommandExecutionMode.Offline, new Dictionary<string, object?>())));

        var result = await router.RouteAsync(new CommandEnvelope(
            Command: "action.add",
            Arguments: new Dictionary<string, string?>
            {
                ["flow"] = "calculator_flow",
                ["type"] = "LaunchApplicationAction"
            },
            ProjectPath: @"D:\demo\sample.testproj",
            RequestId: "req-4"));

        Assert.False(result.Ok);
        Assert.Equal(CommandExecutionMode.UiSession, result.Mode);
        Assert.Equal("BUSY", result.Error?.Code);
        Assert.Equal("session-busy", result.SessionId);
    }

    private sealed class StubSessionDiscovery : IUiSessionDiscovery
    {
        private readonly IReadOnlyList<UiSessionDescriptor> _sessions;

        public StubSessionDiscovery(params UiSessionDescriptor[] sessions)
        {
            _sessions = sessions;
        }

        public Task<IReadOnlyList<UiSessionDescriptor>> DiscoverAsync(string? projectPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_sessions);
        }
    }

    private sealed class StubSessionClient : IUiSessionClient
    {
        private readonly CommandResult _result;

        public StubSessionClient(CommandResult result)
        {
            _result = result;
        }

        public Task<CommandResult> SendAsync(UiSessionDescriptor session, CommandEnvelope command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingSessionClient : IUiSessionClient
    {
        public Task<CommandResult> SendAsync(UiSessionDescriptor session, CommandEnvelope command, CancellationToken cancellationToken = default)
        {
            throw new IOException("pipe unavailable");
        }
    }

    private sealed class StubOfflineExecutor : IOfflineCommandExecutor
    {
        private readonly CommandResult _result;

        public StubOfflineExecutor(CommandResult result)
        {
            _result = result;
        }

        public Task<CommandResult> ExecuteAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }
}
