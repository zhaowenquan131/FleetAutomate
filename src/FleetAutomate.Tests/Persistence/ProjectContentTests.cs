using System.Xml.Linq;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Project;
using FleetAutomate.Services;
using FleetAutomate.Utilities;
using FleetAutomate.View.Dialog;
using FleetAutomate.ViewModel;

namespace FleetAutomate.Tests.Persistence;

public class ProjectContentTests
{
    [Fact]
    public void ProjectContent_RoundTripsAndLoadsLegacyProjects()
    {
        var project = new TestProject { Name = "project" };
        project.Configs.Add(new() { FilePath = @"Configs\app.yaml" });
        project.Resources.Add(new() { FilePath = @"C:\external\image.png" });
        ProjectContentService.SetUiPath(project, null, "save", "//Button[@Name='Save']");

        var xml = TestProjectXmlSerializer.SerializeToXml(project);
        var loaded = TestProjectXmlSerializer.DeserializeFromXml(xml)!;
        Assert.Equal(@"Configs\app.yaml", Assert.Single(loaded.Configs).FilePath);
        Assert.Equal(@"C:\external\image.png", Assert.Single(loaded.Resources).FilePath);
        Assert.Equal("//Button[@Name='Save']", Assert.Single(loaded.UiPathReferences).XPath);

        var legacy = XDocument.Parse(xml);
        legacy.Root!.Elements().Where(element => element.Name.LocalName is "Configs" or "Resources" or "UiPathReferences").Remove();
        var old = TestProjectXmlSerializer.DeserializeFromXml(legacy.ToString())!;
        Assert.Empty(old.Configs);
        Assert.Empty(old.Resources);
        Assert.Empty(old.UiPathReferences);
    }

    [Fact]
    public void FileReferences_UseRelativePathsOnlyInsideProjectAndRebaseOnSaveAs()
    {
        var projectPath = @"C:\work\example\sample.testproj";
        var inside = @"C:\work\example\Configs\app.json";
        var outside = @"C:\work\example-other\app.yaml";
        var project = new TestProject();
        project.Configs.Add(new() { FilePath = ProjectContentService.StoreFilePath(projectPath, inside) });
        project.Configs.Add(new() { FilePath = ProjectContentService.StoreFilePath(projectPath, outside) });
        Assert.Equal(@"Configs\app.json", project.Configs[0].FilePath);
        Assert.Equal(outside, project.Configs[1].FilePath);
        ProjectContentService.RebaseFiles(project, projectPath, @"C:\work\copy\copy.testproj");
        Assert.Equal(inside, project.Configs[0].FilePath);
        Assert.Equal(outside, project.Configs[1].FilePath);
        ProjectContentService.RebaseFiles(project, @"C:\work\copy\copy.testproj", @"C:\work\root.testproj");
        Assert.Equal(@"example\Configs\app.json", project.Configs[0].FilePath);
    }

    [Fact]
    public void UiPaths_RejectDuplicateNamesAndInvalidXPath()
    {
        var project = new TestProject();
        ProjectContentService.SetUiPath(project, null, "save", "//Button");
        Assert.Throws<ArgumentException>(() => ProjectContentService.SetUiPath(project, null, "SAVE", "//Window"));
        Assert.Throws<System.Xml.XPath.XPathException>(() => ProjectContentService.SetUiPath(project, null, "broken", "//Button["));
        Assert.Single(project.UiPathReferences);
    }

    [Fact]
    public async Task Execution_ResolvesProjectReferencesInsideNestedActionsWithoutLeakingToOtherFlows()
    {
        async Task<string?> Run(string xpath)
        {
            var project = new TestProject();
            ProjectContentService.SetUiPath(project, null, "target", xpath);
            var flow = new TestFlow { ParentProject = project };
            var action = new ResolvePathAction();
            var branch = new IfAction { Condition = true };
            branch.IfBlock.Add(action);
            flow.Actions.Add(branch);
            Assert.True(await flow.ExecuteAsync(CancellationToken.None));
            return action.Resolved;
        }

        var results = await Task.WhenAll(Run("//Button"), Run("//Window"));
        Assert.Equal(new[] { "//Button", "//Window" }, results);
        Assert.Throws<InvalidOperationException>(() => UiPathReferenceResolver.Resolve("@{target}"));
        Assert.Equal("//Button", UiPathReferenceResolver.Resolve("//Button"));
    }

    [Fact]
    public void TreeRefresh_KeepsOpenFlowInstancesAndGroupExpansion()
    {
        var model = new TestProject { Name = "Project" };
        model.AddTestFlowToCollection(new TestFlow { Name = "First" });
        var project = new ObservableProject(model);
        var first = project.TestFlows[0];
        project.ProjectNodes[0].Children[1].IsExpanded = false;
        model.AddTestFlowToCollection(new TestFlow { Name = "Second" });
        project.RefreshTestFlows();
        Assert.Same(first, project.TestFlows[0]);
        Assert.Equal(2, model.TestFlows!.Count);
        var root = Assert.Single(project.ProjectNodes);
        Assert.Equal("Project", root.Title);
        Assert.Equal(new[] { "test flows", "configs", "ui path references", "resources" }, root.Children.Select(node => node.Title));
        Assert.False(root.Children[1].IsExpanded);
        Assert.Equal(2, root.Children[0].Children.Count);
    }

    [Theory]
    [InlineData("app.json", "{\"enabled\": true}", true)]
    [InlineData("app.json", "{broken", false)]
    [InlineData("app.yaml", "enabled: true\nitems: [one, two]", true)]
    [InlineData("app.yml", "items: [", false)]
    public void ConfigEditor_ValidatesJsonAndYaml(string path, string text, bool valid)
    {
        var error = Record.Exception(() => ProjectFileEditorWindow.ValidateConfig(path, text));
        Assert.Equal(valid, error == null);
    }

    private sealed class ResolvePathAction : IAction
    {
        public string Name => "Resolve path";
        public string Description => Name;
        public bool IsEnabled => true;
        public ActionState State { get; set; }
        public string? Resolved { get; private set; }
        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            Resolved = UiPathReferenceResolver.Resolve("@{TARGET}");
            State = ActionState.Completed;
            return true;
        }
        public void Cancel() { }
    }
}
