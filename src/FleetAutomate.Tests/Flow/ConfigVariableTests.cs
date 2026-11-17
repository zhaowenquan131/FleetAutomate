using System.Xml.Linq;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Project;
using FleetAutomate.Services;
using FleetAutomate.Utilities;

namespace FleetAutomate.Tests.Flow;

public sealed class ConfigVariableTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FleetConfig-" + Guid.NewGuid().ToString("N"));
    private readonly TestProject _project;

    public ConfigVariableTests()
    {
        Directory.CreateDirectory(_directory);
        _project = new TestProject { ProjectFilePath = Path.Combine(_directory, "sample.testproj") };
    }

    [Theory]
    [InlineData("settings.json", "{\"count\":42,\"ratio\":1.25,\"active\":true,\"code\":\"42\",\"empty\":null,\"items\":[{\"x/y~z\":7}],\"large\":2147483648}")]
    [InlineData("settings.yaml", "count: 42\nratio: 1.25\nactive: true\ncode: '42'\nempty: null\nitems:\n  - 'x/y~z': 7\nlarge: 2147483648")]
    public void ConfigTree_InfersTypesAndPreservesNestedKeys(string name, string text)
    {
        var file = AddConfig(name, text);
        var root = ProjectConfigValues.Read(_project, file);
        Assert.Equal(42, Assert.IsType<int>(ProjectConfigValues.Find(root, "/count")!.Value));
        Assert.Equal(1.25, Assert.IsType<double>(ProjectConfigValues.Find(root, "/ratio")!.Value));
        Assert.True(Assert.IsType<bool>(ProjectConfigValues.Find(root, "/active")!.Value));
        Assert.Equal("42", Assert.IsType<string>(ProjectConfigValues.Find(root, "/code")!.Value));
        Assert.Null(ProjectConfigValues.Find(root, "/empty")!.Value);
        Assert.Equal(7, ProjectConfigValues.Resolve(_project, new(file.Id, "/items/0/x~1y~0z")));
        Assert.Equal(2147483648L, Assert.IsType<long>(ProjectConfigValues.Find(root, "/large")!.Value));
        Assert.IsType<List<object?>>(ProjectConfigValues.Find(root, "/items")!.Value);
        Assert.IsType<Dictionary<string, object?>>(root.Value);
    }

    [Fact]
    public async Task ConfigVariable_ReadsLatestValuesOnEveryExecutionAndFailsForMissingItems()
    {
        var file = AddConfig("settings.json", "{\"value\":42}");
        var flow = new TestFlow { ParentProject = _project };
        var branch = new IfAction { Condition = true };
        var action = new SetVariableAction<object>("fromConfig", null!)
        {
            Environment = flow.Environment, ValueMode = SetVariableValueMode.FromConfig,
            ConfigReference = new ConfigValueReference(file.Id, "/value").Serialize()
        };
        branch.IfBlock.Add(action);
        flow.Actions.Add(branch);
        Assert.True(await flow.ExecuteAsync(CancellationToken.None));
        Assert.Equal(42, flow.GetRuntimeVariableValues()["fromConfig"]);
        File.WriteAllText(Path.Combine(_directory, file.FilePath), "{\"value\":\"changed\"}");
        Assert.True(await flow.ExecuteAsync(CancellationToken.None));
        Assert.Equal("changed", flow.GetRuntimeVariableValues()["fromConfig"]);
        Assert.Equal(typeof(string), action.Variable.Type);
        File.WriteAllText(Path.Combine(_directory, file.FilePath), "{\"value\":null}");
        Assert.True(await flow.ExecuteAsync(CancellationToken.None));
        Assert.Null(flow.GetRuntimeVariableValues()["fromConfig"]);
        File.WriteAllText(Path.Combine(_directory, file.FilePath), "{}");
        Assert.False(await flow.ExecuteAsync(CancellationToken.None));
        Assert.Equal(ActionState.Failed, action.State);
    }

    [Fact]
    public void ConfigReference_RoundTripsAndSurvivesProjectSaveAs()
    {
        var file = AddConfig("settings.json", "{\"value\":42}");
        var flow = new TestFlow { ParentProject = _project };
        flow.Actions.Add(new SetVariableAction<object>("config", 999)
        {
            Environment = flow.Environment, ValueMode = SetVariableValueMode.FromConfig,
            Variable = new Variable("config", 999, typeof(int)),
            ConfigReference = new ConfigValueReference(file.Id, "/value").Serialize()
        });
        var loaded = TestFlowXmlSerializer.DeserializeFromXml(TestFlowXmlSerializer.SerializeToXml(flow))!;
        var action = Assert.IsType<SetVariableAction<int>>(Assert.Single(loaded.Actions));
        Assert.Equal(SetVariableValueMode.FromConfig, action.ValueMode);
        Assert.Null(action.Variable.Value);
        var newPath = Path.Combine(_directory, "moved", "project.testproj");
        ProjectContentService.RebaseFiles(_project, _project.ProjectFilePath!, newPath);
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        _project.SaveToXmlFile(newPath);
        var project = TestProjectXmlExtensions.LoadFromXmlFile(newPath)!;
        Assert.Equal(file.Id, Assert.Single(project.Configs).Id);
        Assert.Equal(42, ProjectConfigValues.Resolve(project, ConfigValueReference.Parse(action.ConfigReference)));
    }

    [Fact]
    public void LegacyConfigReferences_GetStableIdentifiersWithoutRequiringSave()
    {
        AddConfig("settings.json", "{}");
        var document = XDocument.Parse(TestProjectXmlSerializer.SerializeToXml(_project));
        document.Descendants().Where(element => element.Name.LocalName == "Id").Remove();
        var first = TestProjectXmlSerializer.DeserializeFromXml(document.ToString())!;
        var second = TestProjectXmlSerializer.DeserializeFromXml(document.ToString())!;
        Assert.NotEmpty(first.Configs[0].Id);
        Assert.Equal(first.Configs[0].Id, second.Configs[0].Id);
    }

    private ProjectFileReference AddConfig(string name, string text)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, text);
        return ProjectContentService.AddFile(_project, _project.ProjectFilePath!, path, config: true);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
