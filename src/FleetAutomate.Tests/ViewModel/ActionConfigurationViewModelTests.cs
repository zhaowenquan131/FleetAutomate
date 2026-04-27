using FleetAutomate.Application.ActionConfiguration;
using FleetAutomate.Application.Commanding;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.MouseAndKeyboard;
using FleetAutomate.ViewModel;

namespace FleetAutomate.Tests.ViewModel;

public sealed class ActionConfigurationViewModelTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _projectPath;

    public ActionConfigurationViewModelTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FleetAutomate.ActionConfiguration.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _projectPath = Path.Combine(_tempDirectory, "sample.testproj");
    }

    [Fact]
    public void AddActionFromTemplate_UsesConfigurationPromptForSchemaBackedAction()
    {
        var viewModel = CreateViewModelWithActiveFlow();
        var prompted = false;
        viewModel.OnPromptActionConfiguration += action =>
        {
            prompted = true;
            Assert.IsType<MoveMouseToAction>(action);
            return
            [
                new ActionConfigurationValue(nameof(MoveMouseToAction.X), 320),
                new ActionConfigurationValue(nameof(MoveMouseToAction.Y), 180)
            ];
        };

        viewModel.AddActionFromTemplate(new ActionTemplate(
            "Move Mouse To",
            "Mouse & Keyboard",
            string.Empty,
            typeof(MoveMouseToAction)));

        Assert.True(prompted);
        var action = Assert.IsType<MoveMouseToAction>(Assert.Single(viewModel.ActiveTestFlow!.Actions));
        Assert.Equal(320, action.X);
        Assert.Equal(180, action.Y);
        Assert.True(viewModel.ActiveTestFlow.HasUnsavedChanges);
    }

    [Fact]
    public void AddActionFromTemplate_DoesNotInsertActionWhenConfigurationIsCancelled()
    {
        var viewModel = CreateViewModelWithActiveFlow();
        viewModel.OnPromptActionConfiguration += _ => null;

        viewModel.AddActionFromTemplate(new ActionTemplate(
            "Move Mouse To",
            "Mouse & Keyboard",
            string.Empty,
            typeof(MoveMouseToAction)));

        Assert.Empty(viewModel.ActiveTestFlow!.Actions);
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
