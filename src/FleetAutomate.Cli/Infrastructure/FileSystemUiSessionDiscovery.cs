using FleetAutomate.Application.Commanding;
using System.Text.Json;

namespace FleetAutomate.Cli.Infrastructure;

internal sealed class FileSystemUiSessionDiscovery : IUiSessionDiscovery
{
    public Task<IReadOnlyList<UiSessionDescriptor>> DiscoverAsync(string? projectPath, CancellationToken cancellationToken = default)
    {
        var sessions = new List<UiSessionDescriptor>();
        var directory = UiSessionRegistry.RegistryDirectoryPath;
        if (!Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<UiSessionDescriptor>>(sessions);
        }

        foreach (var filePath in Directory.EnumerateFiles(directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var json = File.ReadAllText(filePath);
                var descriptor = JsonSerializer.Deserialize<UiSessionDescriptor>(json);
                if (descriptor != null)
                {
                    sessions.Add(descriptor);
                }
            }
            catch
            {
            }
        }

        return Task.FromResult<IReadOnlyList<UiSessionDescriptor>>(sessions);
    }
}
