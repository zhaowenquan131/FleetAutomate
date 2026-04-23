using System.Text.Json.Serialization;
using System.IO;

namespace FleetAutomate.Application.Commanding;

public enum CommandExecutionMode
{
    Offline,
    UiSession
}

public sealed record CommandError(string Code, string Message);

public sealed record CommandEnvelope(
    string Command,
    IReadOnlyDictionary<string, string?> Arguments,
    string? ProjectPath,
    string RequestId);

public sealed record UiSessionDescriptor(
    string SessionId,
    int ProcessId,
    string? ProjectPath,
    DateTimeOffset LastActiveUtc,
    bool AllowsWrite,
    bool IsBusy)
{
    [JsonIgnore]
    public string PipeName => UiSessionRegistry.PipeNamePrefix + SessionId;
}

public sealed record CommandResult(
    bool Ok,
    CommandExecutionMode Mode,
    string? SessionId,
    object? Payload,
    CommandError? Error)
{
    public static CommandResult Success(CommandExecutionMode mode, object? payload, string? sessionId = null)
    {
        return new CommandResult(true, mode, sessionId, payload, null);
    }

    public static CommandResult Failure(CommandExecutionMode mode, string code, string message, string? sessionId = null, object? payload = null)
    {
        return new CommandResult(false, mode, sessionId, payload, new CommandError(code, message));
    }
}

public static class UiSessionRegistry
{
    public const string PipeNamePrefix = "FleetAutomate.UiSession.";

    public static string RegistryDirectoryPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FleetAutomate",
            "Sessions");

    public static string GetMetadataPath(string sessionId)
    {
        return Path.Combine(RegistryDirectoryPath, $"{sessionId}.json");
    }

    public static string NormalizeProjectPath(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return string.Empty;
        }

        return Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
