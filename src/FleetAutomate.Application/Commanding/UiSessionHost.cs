using FleetAutomate.ViewModel;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Security.Principal;
using System.Text.Json;

namespace FleetAutomate.Application.Commanding;

public sealed class UiSessionHost : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly CancellationTokenSource _cts = new();
    private readonly MainViewModel _viewModel;
    private readonly UiSessionCommandExecutor _executor;
    private readonly string _metadataPath;
    private Task? _serverLoopTask;

    public UiSessionHost(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        SessionId = Guid.NewGuid().ToString("N");
        _executor = new UiSessionCommandExecutor(viewModel, SessionId);
        _metadataPath = UiSessionRegistry.GetMetadataPath(SessionId);
    }

    public string SessionId { get; }

    public void Start()
    {
        Directory.CreateDirectory(UiSessionRegistry.RegistryDirectoryPath);
        WriteMetadata();
        _serverLoopTask = Task.Run(() => RunServerLoopAsync(_cts.Token));
        _viewModel.ProjectManager.OnProjectPathChanged += OnProjectPathChanged;
        _viewModel.ProjectManager.OnUnsavedChangesChanged += OnUnsavedChangesChanged;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _viewModel.ProjectManager.OnProjectPathChanged -= OnProjectPathChanged;
        _viewModel.ProjectManager.OnUnsavedChangesChanged -= OnUnsavedChangesChanged;

        if (_serverLoopTask != null)
        {
            try
            {
                await _serverLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (File.Exists(_metadataPath))
        {
            File.Delete(_metadataPath);
        }

        _cts.Dispose();
    }

    private async Task RunServerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                UiSessionRegistry.PipeNamePrefix + SessionId,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            await server.WaitForConnectionAsync(cancellationToken);

            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true)
            {
                AutoFlush = true
            };

            var requestJson = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                continue;
            }

            var command = JsonSerializer.Deserialize<CommandEnvelope>(requestJson, JsonOptions);
            if (command == null)
            {
                continue;
            }

            var result = await _executor.ExecuteAsync(command, cancellationToken);
            WriteMetadata();
            var responseJson = JsonSerializer.Serialize(result, JsonOptions);
            await writer.WriteLineAsync(responseJson);
        }
    }

    private void OnProjectPathChanged(string? _)
    {
        WriteMetadata();
    }

    private void OnUnsavedChangesChanged(bool _)
    {
        WriteMetadata();
    }

    private void WriteMetadata()
    {
        Directory.CreateDirectory(UiSessionRegistry.RegistryDirectoryPath);
        var descriptor = new UiSessionDescriptor(
            SessionId,
            Environment.ProcessId,
            _viewModel.CurrentProjectPath,
            DateTimeOffset.UtcNow,
            AllowsWrite: true,
            IsBusy: _viewModel.IsTestFlowRunning);
        File.WriteAllText(_metadataPath, JsonSerializer.Serialize(descriptor, JsonOptions));
    }
}
