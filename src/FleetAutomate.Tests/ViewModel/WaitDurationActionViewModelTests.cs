using FleetAutomate.Application.Commanding;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.ViewModel;

namespace FleetAutomate.Tests.ViewModel;

public sealed class WaitDurationActionViewModelTests : IDisposable
{
    private readonly string _tempDirectory;

    public WaitDurationActionViewModelTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FleetAutomate.WaitDuration.ViewModel.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task AddActionFromTemplate_CreatesWaitDurationAction()
    {
        var viewModel = new MainViewModel();
        var projectPath = Path.Combine(_tempDirectory, "sample.testproj");
        Assert.True(viewModel.ProjectManager.CreateNewProject("Sample", projectPath));
        var executor = new UiSessionCommandExecutor(viewModel, "session-test");
        var result = await executor.ExecuteAsync(new CommandEnvelope(
            Command: "testflow.create",
            Arguments: new Dictionary<string, string?>
            {
                ["name"] = "flow"
            },
            ProjectPath: projectPath,
            RequestId: "req-flow"));
        Assert.True(result.Ok);
        viewModel.OnPromptWaitDuration += () => (2, WaitDurationUnit.Minutes);
        var flow = viewModel.ActiveProject!.TestFlows.Single();
        viewModel.ActiveTestFlow = flow;
        var template = new ActionTemplate("Wait", "System", "wait", typeof(WaitDurationAction), "Wait");

        viewModel.AddActionFromTemplate(template);

        var action = Assert.IsType<WaitDurationAction>(Assert.Single(flow.Actions));
        Assert.Equal(2, action.Duration);
        Assert.Equal(WaitDurationUnit.Minutes, action.Unit);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
