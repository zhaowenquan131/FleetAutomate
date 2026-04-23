using FleetAutomate.Application.Commanding;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace FleetAutomate.Cli.Infrastructure;

internal sealed class NamedPipeUiSessionClient : IUiSessionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<CommandResult> SendAsync(UiSessionDescriptor session, CommandEnvelope command, CancellationToken cancellationToken = default)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            session.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));
        await pipe.ConnectAsync(timeoutCts.Token);

        var requestJson = JsonSerializer.Serialize(command, JsonOptions);
        var requestBytes = Encoding.UTF8.GetBytes(requestJson + Environment.NewLine);
        await pipe.WriteAsync(requestBytes, timeoutCts.Token);
        await pipe.FlushAsync(timeoutCts.Token);

        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        var responseJson = await reader.ReadLineAsync(timeoutCts.Token)
            ?? throw new IOException("UI session returned an empty response.");

        var response = JsonSerializer.Deserialize<CommandResult>(responseJson, JsonOptions);
        return response ?? throw new IOException("Failed to deserialize UI session response.");
    }
}
