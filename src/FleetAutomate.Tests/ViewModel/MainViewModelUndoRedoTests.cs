using FleetAutomate.Application.Commanding;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.ViewModel;

namespace FleetAutomate.Tests.ViewModel;

public sealed class MainViewModelUndoRedoTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _projectPath;

    public MainViewModelUndoRedoTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FleetAutomate.MainViewModelUndo.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _projectPath = Path.Combine(_tempDirectory, "sample.testproj");
    }

    [Fact]
    public void UndoRedoCommands_FollowActiveFlowHistoryAndRunningState()
    {
        var viewModel = CreateViewModelWithActiveFlow();
        var action = new LogAction { Message = "undo me" };

        viewModel.InsertAction(action, insertionTarget: null);

        Assert.True(viewModel.UndoTestFlowCommand.CanExecute(null));
        Assert.False(viewModel.RedoTestFlowCommand.CanExecute(null));

        viewModel.IsTestFlowRunning = true;

        Assert.False(viewModel.UndoTestFlowCommand.CanExecute(null));

        viewModel.IsTestFlowRunning = false;
        viewModel.UndoTestFlowCommand.Execute(null);

        Assert.False(viewModel.UndoTestFlowCommand.CanExecute(null));
        Assert.True(viewModel.RedoTestFlowCommand.CanExecute(null));
    }

    [Fact]
    public void SaveProjectCommand_MarksActiveFlowUndoCheckpoint()
    {
        var viewModel = CreateViewModelWithActiveFlow();
        viewModel.InsertAction(new LogAction { Message = "save me" }, insertionTarget: null);

        Assert.True(viewModel.SaveProjectCommand.CanExecute(null));

        viewModel.SaveProjectCommand.Execute(null);

        Assert.False(viewModel.ActiveTestFlow!.HasUnsavedChanges);
        Assert.False(viewModel.SaveProjectCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyActionPropertyChanges_RecordsUndoForUseInvoke()
    {
        var viewModel = CreateViewModelWithActiveFlow();
        var action = new ClickElementAction
        {
            ElementIdentifier = "OK",
            IdentifierType = "Name",
            UseInvoke = false
        };
        viewModel.ActiveTestFlow!.Actions.Add(action);
        viewModel.ActiveTestFlow.MarkSavedCheckpoint();

        viewModel.ApplyActionPropertyChanges(action, "Edit click action", [
            (nameof(ClickElementAction.UseInvoke), true)
        ]);

        Assert.True(action.UseInvoke);
        Assert.True(viewModel.UndoTestFlowCommand.CanExecute(null));

        viewModel.UndoTestFlowCommand.Execute(null);

        Assert.False(action.UseInvoke);

        viewModel.RedoTestFlowCommand.Execute(null);

        Assert.True(action.UseInvoke);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private MainViewModel CreateViewModelWithActiveFlow()
    {
        var viewModel = new MainViewModel();
        var created = viewModel.ProjectManager.CreateNewProject("Sample Project", _projectPath);
        Assert.True(created);

        var executor = new UiSessionCommandExecutor(viewModel, "session-test");
        var result = executor.ExecuteAsync(new CommandEnvelope(
            Command: "testflow.create",
            Arguments: new Dictionary<string, string?>
            {
                ["name"] = "flow"
            },
            ProjectPath: _projectPath,
            RequestId: "req-flow")).GetAwaiter().GetResult();

        Assert.True(result.Ok);
        var flow = Assert.Single(viewModel.ActiveProject!.TestFlows);
        viewModel.ActiveTestFlow = flow;
        flow.MarkSavedCheckpoint();
        return viewModel;
    }
}
