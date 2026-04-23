using FleetAutomate.Cli.Infrastructure;
using FleetAutomate.Cli.Output;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Project;
using FleetAutomate.Utilities;
using System.Text.Json;

namespace FleetAutomate.Tests.Cli;

public sealed class CliWriteCommandsTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _projectPath;

    public CliWriteCommandsTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FleetAutomate.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _projectPath = Path.Combine(_tempDirectory, "sample.testproj");
    }

    [Fact]
    public async Task DispatchAsync_TestprojCreate_CreatesProjectFile()
    {
        var exitCode = await DispatchAsync("testproj", "create", "--project", _projectPath, "--name", "Sample Project");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(_projectPath));

        var project = TestProjectXmlExtensions.LoadFromXmlFile(_projectPath);
        Assert.NotNull(project);
        Assert.Equal("Sample Project", project!.Name);
        Assert.Empty(project.TestFlows!);
    }

    [Fact]
    public async Task DispatchAsync_TestflowCreate_CreatesFlowFileAndRegistersFlow()
    {
        CreateProject();
        var exitCode = await DispatchAsync("testflow", "create", "--project", _projectPath, "--name", "calculator_flow");

        Assert.Equal(0, exitCode);

        var project = TestProjectXmlExtensions.LoadFromXmlFile(_projectPath);
        Assert.NotNull(project);

        var flow = project!.FindTestFlowByName("calculator_flow");
        Assert.NotNull(flow);
        Assert.Equal("calculator_flow", flow!.Name);
        Assert.True(File.Exists(flow.FileName));
    }

    [Fact]
    public async Task DispatchAsync_ActionAddSetAndRemove_ModifiesFlow()
    {
        CreateProject();
        await DispatchAsync("testflow", "create", "--project", _projectPath, "--name", "calculator_flow");

        var addLaunchExitCode = await DispatchAsync(
            "action",
            "add",
            "--project",
            _projectPath,
            "--flow",
            "calculator_flow",
            "--type",
            "LaunchApplicationAction");

        Assert.Equal(0, addLaunchExitCode);

        var setLaunchExitCode = await DispatchAsync(
            "action",
            "set",
            "--project",
            _projectPath,
            "--flow",
            "calculator_flow",
            "--path",
            "0",
            "--property",
            "ExecutablePath",
            "--value",
            "calc.exe");

        Assert.Equal(0, setLaunchExitCode);

        var addWaitExitCode = await DispatchAsync(
            "action",
            "add",
            "--project",
            _projectPath,
            "--flow",
            "calculator_flow",
            "--type",
            "WaitForElementAction");

        Assert.Equal(0, addWaitExitCode);

        var setWaitIdentifierExitCode = await DispatchAsync(
            "action",
            "set",
            "--project",
            _projectPath,
            "--flow",
            "calculator_flow",
            "--path",
            "1",
            "--property",
            "ElementIdentifier",
            "--value",
            "//Window[@name='Calculator']");

        Assert.Equal(0, setWaitIdentifierExitCode);

        var setWaitTypeExitCode = await DispatchAsync(
            "action",
            "set",
            "--project",
            _projectPath,
            "--flow",
            "calculator_flow",
            "--path",
            "1",
            "--property",
            "IdentifierType",
            "--value",
            "XPath");

        Assert.Equal(0, setWaitTypeExitCode);

        var removeExitCode = await DispatchAsync(
            "action",
            "remove",
            "--project",
            _projectPath,
            "--flow",
            "calculator_flow",
            "--path",
            "1");

        Assert.Equal(0, removeExitCode);

        var project = TestProjectXmlExtensions.LoadFromXmlFile(_projectPath);
        Assert.NotNull(project);

        var flow = project!.FindTestFlowByName("calculator_flow");
        Assert.NotNull(flow);
        Assert.Single(flow!.Actions);

        var launch = Assert.IsType<LaunchApplicationAction>(flow.Actions[0]);
        Assert.Equal("calc.exe", launch.ExecutablePath);
    }

    [Fact]
    public async Task DispatchAsync_ActionSet_ConvertsPrimitiveTypes()
    {
        CreateProject();
        await DispatchAsync("testflow", "create", "--project", _projectPath, "--name", "calculator_flow");
        await DispatchAsync(
            "action",
            "add",
            "--project",
            _projectPath,
            "--flow",
            "calculator_flow",
            "--type",
            "WaitForElementAction");

        var timeoutExitCode = await DispatchAsync(
            "action",
            "set",
            "--project",
            _projectPath,
            "--flow",
            "calculator_flow",
            "--path",
            "0",
            "--property",
            "TimeoutMilliseconds",
            "--value",
            "5000");

        var addDictionaryExitCode = await DispatchAsync(
            "action",
            "set",
            "--project",
            _projectPath,
            "--flow",
            "calculator_flow",
            "--path",
            "0",
            "--property",
            "AddToGlobalDictionary",
            "--value",
            "true");

        Assert.Equal(0, timeoutExitCode);
        Assert.Equal(0, addDictionaryExitCode);

        var project = TestProjectXmlExtensions.LoadFromXmlFile(_projectPath);
        var flow = project!.FindTestFlowByName("calculator_flow");
        var wait = Assert.IsType<WaitForElementAction>(Assert.Single(flow!.Actions));
        Assert.Equal(5000, wait.TimeoutMilliseconds);
        Assert.True(wait.AddToGlobalDictionary);
    }

    [Fact]
    public async Task RunAsync_ProjectSave_SucceedsInOfflineModeAndWritesModeField()
    {
        CreateProject();

        var (exitCode, stdout, stderr) = await RunProgramAsync(
            "project",
            "save",
            "--project",
            _projectPath,
            "--format",
            "json");

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr));

        using var json = JsonDocument.Parse(stdout);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("offline", json.RootElement.GetProperty("mode").GetString());
        Assert.Equal(Path.GetFullPath(_projectPath), json.RootElement.GetProperty("payload").GetProperty("projectPath").GetString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static Task<int> DispatchAsync(string resource, string verb, params string[] args)
    {
        var parser = new CliArgumentParser(args);
        var dispatcher = new CliCommandDispatcher(new CliOutputWriter(OutputFormat.Json));
        var projectPath = parser.GetRequiredOption("project");
        return dispatcher.DispatchAsync(resource, verb, projectPath, parser);
    }

    private void CreateProject()
    {
        var project = new TestProject
        {
            Name = "Sample Project"
        };

        project.SaveProjectAndTestFlows(_projectPath);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProgramAsync(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        await using var stdout = new StringWriter();
        await using var stderr = new StringWriter();

        Console.SetOut(stdout);
        Console.SetError(stderr);

        try
        {
            var exitCode = await CliProgram.RunAsync(args);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
