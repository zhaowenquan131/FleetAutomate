using FleetAutomate.Model.Actions.FileSystem;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Tests.Actions;

public sealed class FileSystemActionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"FleetAutomateTests_{Guid.NewGuid():N}");

    public FileSystemActionTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateDirectoryAction_CreatesDirectory()
    {
        var target = Path.Combine(_root, "created");
        var action = new CreateDirectoryAction { DirectoryPath = target };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.True(Directory.Exists(target));
        Assert.Equal(ActionState.Completed, action.State);
    }

    [Fact]
    public async Task IfFileExistsAction_ReflectsFilePresence()
    {
        var file = Path.Combine(_root, "exists.txt");
        await File.WriteAllTextAsync(file, "content");
        var action = new IfFileExistsAction { FilePath = file };

        var result = await action.ExecuteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.True(action.Exists);
        Assert.Equal(ActionState.Completed, action.State);
    }

    [Fact]
    public async Task FileCopyMoveRenameDeleteActions_ModifyFiles()
    {
        var source = Path.Combine(_root, "source.txt");
        var copied = Path.Combine(_root, "copied.txt");
        var moved = Path.Combine(_root, "moved.txt");
        var renamed = Path.Combine(_root, "renamed.txt");
        await File.WriteAllTextAsync(source, "content");

        Assert.True(await new CopyFileAction { SourcePath = source, DestinationPath = copied }.ExecuteAsync(CancellationToken.None));
        Assert.True(File.Exists(copied));

        Assert.True(await new MoveFileAction { SourcePath = copied, DestinationPath = moved }.ExecuteAsync(CancellationToken.None));
        Assert.False(File.Exists(copied));
        Assert.True(File.Exists(moved));

        Assert.True(await new RenameFileAction { FilePath = moved, NewName = "renamed.txt" }.ExecuteAsync(CancellationToken.None));
        Assert.False(File.Exists(moved));
        Assert.True(File.Exists(renamed));

        Assert.True(await new DeleteFileAction { FilePath = renamed }.ExecuteAsync(CancellationToken.None));
        Assert.False(File.Exists(renamed));
    }

    [Fact]
    public async Task ReadAndWriteTextFileActions_RoundTripContent()
    {
        var file = Path.Combine(_root, "text.txt");
        var write = new WriteTextToFileAction { FilePath = file, Text = "hello", Append = false };
        var read = new ReadTextFromFileAction { FilePath = file };

        Assert.True(await write.ExecuteAsync(CancellationToken.None));
        Assert.True(await read.ExecuteAsync(CancellationToken.None));

        Assert.Equal("hello", read.Text);
        Assert.Equal(ActionState.Completed, read.State);
    }
}
