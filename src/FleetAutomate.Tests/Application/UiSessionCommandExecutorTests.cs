using FleetAutomate.Application.Commanding;
using FleetAutomate.ViewModel;

namespace FleetAutomate.Tests.Application;

public sealed class UiSessionCommandExecutorTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _projectPath;

    public UiSessionCommandExecutorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FleetAutomate.UiSession.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _projectPath = Path.Combine(_tempDirectory, "sample.testproj");
    }

    [Fact]
    public async Task ExecuteAsync_TestflowCreate_UpdatesUiMemoryWithoutWritingFlowFile()
    {
        var viewModel = CreateLoadedViewModel();
        var executor = new UiSessionCommandExecutor(viewModel, "session-test");

        var result = await executor.ExecuteAsync(new CommandEnvelope(
            Command: "testflow.create",
            Arguments: new Dictionary<string, string?>
            {
                ["name"] = "calculator_flow"
            },
            ProjectPath: _projectPath,
            RequestId: "req-1"));

        Assert.True(result.Ok);
        Assert.Equal(CommandExecutionMode.UiSession, result.Mode);

        var flow = Assert.Single(viewModel.ActiveProject!.TestFlows);
        Assert.Equal("calculator_flow", flow.Name);
        Assert.True(viewModel.HasUnsavedChanges);
        Assert.True(flow.HasUnsavedChanges);
        Assert.False(File.Exists(flow.FileName));
    }

    [Fact]
    public async Task ExecuteAsync_ActionAdd_RecordsUndoInUiSession()
    {
        var viewModel = CreateLoadedViewModel();
        var executor = new UiSessionCommandExecutor(viewModel, "session-test");

        await executor.ExecuteAsync(new CommandEnvelope(
            Command: "testflow.create",
            Arguments: new Dictionary<string, string?>
            {
                ["name"] = "calculator_flow"
            },
            ProjectPath: _projectPath,
            RequestId: "req-add-flow"));

        var addResult = await executor.ExecuteAsync(new CommandEnvelope(
            Command: "action.add",
            Arguments: new Dictionary<string, string?>
            {
                ["flow"] = "calculator_flow",
                ["type"] = "LogAction"
            },
            ProjectPath: _projectPath,
            RequestId: "req-add-action"));

        Assert.True(addResult.Ok);
        var flow = Assert.Single(viewModel.ActiveProject!.TestFlows);
        var action = Assert.Single(flow.Actions);
        Assert.Equal("Log", action.Name);
        Assert.True(flow.CanUndo);

        flow.Undo();

        Assert.Empty(flow.Actions);
    }

    [Fact]
    public async Task ExecuteAsync_ProjectSave_PersistsPendingUiSessionChanges()
    {
        var viewModel = CreateLoadedViewModel();
        var executor = new UiSessionCommandExecutor(viewModel, "session-test");

        await executor.ExecuteAsync(new CommandEnvelope(
            Command: "testflow.create",
            Arguments: new Dictionary<string, string?>
            {
                ["name"] = "calculator_flow"
            },
            ProjectPath: _projectPath,
            RequestId: "req-2"));

        var saveResult = await executor.ExecuteAsync(new CommandEnvelope(
            Command: "project.save",
            Arguments: new Dictionary<string, string?>(),
            ProjectPath: _projectPath,
            RequestId: "req-3"));

        Assert.True(saveResult.Ok);
        var flow = Assert.Single(viewModel.ActiveProject!.TestFlows);
        Assert.True(File.Exists(_projectPath));
        Assert.True(File.Exists(flow.FileName));
        Assert.False(viewModel.HasUnsavedChanges);
        Assert.False(flow.HasUnsavedChanges);
    }

    [Fact]
    public async Task ExecuteAsync_WriteCommandDuringRun_ReturnsBusyError()
    {
        var viewModel = CreateLoadedViewModel();
        viewModel.IsTestFlowRunning = true;
        var executor = new UiSessionCommandExecutor(viewModel, "session-test");

        var result = await executor.ExecuteAsync(new CommandEnvelope(
            Command: "testflow.create",
            Arguments: new Dictionary<string, string?>
            {
                ["name"] = "blocked_flow"
            },
            ProjectPath: _projectPath,
            RequestId: "req-4"));

        Assert.False(result.Ok);
        Assert.Equal("BUSY", result.Error?.Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private MainViewModel CreateLoadedViewModel()
    {
        var viewModel = new MainViewModel();
        var created = viewModel.ProjectManager.CreateNewProject("Sample Project", _projectPath);
        Assert.True(created);
        Assert.NotNull(viewModel.ActiveProject);
        return viewModel;
    }
}
