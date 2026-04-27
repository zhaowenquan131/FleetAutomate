using System.Runtime.Serialization;
using static FleetAutomate.Model.Actions.FileSystem.FileSystemActionValidation;
using Directory = System.IO.Directory;
using DirectoryNotFoundException = System.IO.DirectoryNotFoundException;
using File = System.IO.File;
using FileNotFoundException = System.IO.FileNotFoundException;
using Path = System.IO.Path;

namespace FleetAutomate.Model.Actions.FileSystem;

[DataContract]
public sealed class IfFileExistsAction : ActionBase
{
    private string _filePath = string.Empty;

    public override string Name => "If File Exists";
    protected override string DefaultDescription => "Check if file exists";

    [DataMember]
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    [IgnoreDataMember]
    public bool Exists { get; private set; }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Exists = File.Exists(FilePath);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class IfDirectoryExistsAction : ActionBase
{
    private string _directoryPath = string.Empty;

    public override string Name => "If Directory Exists";
    protected override string DefaultDescription => "Check if directory exists";

    [DataMember]
    public string DirectoryPath
    {
        get => _directoryPath;
        set => SetProperty(ref _directoryPath, value);
    }

    [IgnoreDataMember]
    public bool Exists { get; private set; }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Exists = Directory.Exists(DirectoryPath);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class CreateDirectoryAction : ActionBase
{
    private string _directoryPath = string.Empty;

    public override string Name => "Create Directory";
    protected override string DefaultDescription => "Create directory";

    [DataMember]
    public string DirectoryPath
    {
        get => _directoryPath;
        set => SetProperty(ref _directoryPath, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RequirePath(DirectoryPath, nameof(DirectoryPath)));
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class ClearDirectoryAction : ActionBase
{
    private string _directoryPath = string.Empty;

    public override string Name => "Clear Directory";
    protected override string DefaultDescription => "Delete all files and subdirectories in directory";

    [DataMember]
    public string DirectoryPath
    {
        get => _directoryPath;
        set => SetProperty(ref _directoryPath, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        var directory = RequireExistingDirectory(DirectoryPath);
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            File.Delete(file);
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(directory))
        {
            Directory.Delete(childDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class DeleteDirectoryAction : ActionBase
{
    private string _directoryPath = string.Empty;
    private bool _recursive = true;

    public override string Name => "Delete Directory";
    protected override string DefaultDescription => "Delete directory";

    [DataMember]
    public string DirectoryPath
    {
        get => _directoryPath;
        set => SetProperty(ref _directoryPath, value);
    }

    [DataMember]
    public bool Recursive
    {
        get => _recursive;
        set => SetProperty(ref _recursive, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Directory.Delete(RequireExistingDirectory(DirectoryPath), Recursive);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class WaitForFileAction : ActionBase
{
    private string _filePath = string.Empty;
    private int _timeoutMilliseconds = 30000;
    private int _pollingIntervalMilliseconds = 250;

    public override string Name => "Wait for File";
    protected override string DefaultDescription => "Wait until file exists";

    [DataMember]
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    [DataMember]
    public int TimeoutMilliseconds
    {
        get => _timeoutMilliseconds;
        set => SetProperty(ref _timeoutMilliseconds, Math.Max(1, value));
    }

    [DataMember]
    public int PollingIntervalMilliseconds
    {
        get => _pollingIntervalMilliseconds;
        set => SetProperty(ref _pollingIntervalMilliseconds, Math.Max(1, value));
    }

    protected override async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(TimeoutMilliseconds);
        while (!File.Exists(FilePath))
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException($"File '{FilePath}' did not exist within {TimeoutMilliseconds} ms.");
            }

            await Task.Delay(PollingIntervalMilliseconds, cancellationToken);
        }
    }
}

[DataContract]
public sealed class CopyFileAction : ActionBase
{
    private string _sourcePath = string.Empty;
    private string _destinationPath = string.Empty;
    private bool _overwrite = true;

    public override string Name => "Copy File";
    protected override string DefaultDescription => "Copy file to destination";

    [DataMember]
    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    [DataMember]
    public string DestinationPath
    {
        get => _destinationPath;
        set => SetProperty(ref _destinationPath, value);
    }

    [DataMember]
    public bool Overwrite
    {
        get => _overwrite;
        set => SetProperty(ref _overwrite, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        File.Copy(RequireExistingFile(SourcePath), RequirePath(DestinationPath, nameof(DestinationPath)), Overwrite);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class MoveFileAction : ActionBase
{
    private string _sourcePath = string.Empty;
    private string _destinationPath = string.Empty;
    private bool _overwrite = true;

    public override string Name => "Move File";
    protected override string DefaultDescription => "Move file to destination";

    [DataMember]
    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    [DataMember]
    public string DestinationPath
    {
        get => _destinationPath;
        set => SetProperty(ref _destinationPath, value);
    }

    [DataMember]
    public bool Overwrite
    {
        get => _overwrite;
        set => SetProperty(ref _overwrite, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        File.Move(RequireExistingFile(SourcePath), RequirePath(DestinationPath, nameof(DestinationPath)), Overwrite);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class DeleteFileAction : ActionBase
{
    private string _filePath = string.Empty;

    public override string Name => "Delete File";
    protected override string DefaultDescription => "Delete file";

    [DataMember]
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        File.Delete(RequireExistingFile(FilePath));
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class RenameFileAction : ActionBase
{
    private string _filePath = string.Empty;
    private string _newName = string.Empty;
    private bool _overwrite = true;

    public override string Name => "Rename File";
    protected override string DefaultDescription => "Rename file";

    [DataMember]
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    [DataMember]
    public string NewName
    {
        get => _newName;
        set => SetProperty(ref _newName, value);
    }

    [DataMember]
    public bool Overwrite
    {
        get => _overwrite;
        set => SetProperty(ref _overwrite, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        var source = RequireExistingFile(FilePath);
        var directory = Path.GetDirectoryName(source) ?? string.Empty;
        File.Move(source, Path.Combine(directory, RequirePath(NewName, nameof(NewName))), Overwrite);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class ReadTextFromFileAction : ActionBase
{
    private string _filePath = string.Empty;

    public override string Name => "Read Text from File";
    protected override string DefaultDescription => "Read file content";

    [DataMember]
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    [IgnoreDataMember]
    public string Text { get; private set; } = string.Empty;

    protected override async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Text = await File.ReadAllTextAsync(RequireExistingFile(FilePath), cancellationToken);
    }
}

[DataContract]
public sealed class WriteTextToFileAction : ActionBase
{
    private string _filePath = string.Empty;
    private string _text = string.Empty;
    private bool _append;

    public override string Name => "Write Text to File";
    protected override string DefaultDescription => "Write text to file";

    [DataMember]
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    [DataMember]
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    [DataMember]
    public bool Append
    {
        get => _append;
        set => SetProperty(ref _append, value);
    }

    protected override async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        var path = RequirePath(FilePath, nameof(FilePath));
        if (Append)
        {
            await File.AppendAllTextAsync(path, Text, cancellationToken);
            return;
        }

        await File.WriteAllTextAsync(path, Text, cancellationToken);
    }
}

[DataContract]
public sealed class GetDirectoryOfFileAction : ActionBase
{
    private string _filePath = string.Empty;

    public override string Name => "Get Directory of File";
    protected override string DefaultDescription => "Extract directory path";

    [DataMember]
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    [IgnoreDataMember]
    public string DirectoryPath { get; private set; } = string.Empty;

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        DirectoryPath = Path.GetDirectoryName(RequirePath(FilePath, nameof(FilePath))) ?? string.Empty;
        return Task.CompletedTask;
    }
}

internal static class FileSystemActionValidation
{
    public static string RequirePath(string path, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"{propertyName} cannot be empty.");
        }

        return path;
    }

    public static string RequireExistingFile(string path)
    {
        RequirePath(path, nameof(path));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File '{path}' was not found.", path);
        }

        return path;
    }

    public static string RequireExistingDirectory(string path)
    {
        RequirePath(path, nameof(path));
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory '{path}' was not found.");
        }

        return path;
    }
}
