using FleetAutomate.Model;
using FleetAutomate.Model.Actions.FileSystem;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Actions.Text;
using FleetAutomate.Model.Flow;
using FleetAutomate.Persistence;
using FleetAutomate.UndoRedo;
using FleetAutomate.ViewModel;

namespace FleetAutomate.Tests.Flow;

public sealed class TextParameterVariableTests
{
    [Fact]
    public async Task NestedAction_ReadsLatestVariableValueAndPreservesLiteral()
    {
        var flow = new TestFlow();
        var variable = new Variable("message", "{other}", typeof(string));
        flow.Environment.Variables.Add(variable);
        flow.Environment.Variables.Add(new Variable("other", "must not interpolate", typeof(string)));
        var log = new LogAction { Message = "original literal" };
        ((IAction)log).TextVariableBindings = new Dictionary<string, string> { [nameof(LogAction.Message)] = "message" };
        var branch = new IfAction { Condition = true, Environment = flow.Environment };
        branch.IfBlock.Add(log);
        flow.Actions.Add(branch);
        var observable = new ObservableFlow(flow);
        observable.UndoRedoService.MarkSavedCheckpoint();

        Assert.True(await flow.StartAsync(CancellationToken.None));
        Assert.Equal("{other}", log.LastResolvedMessage);
        Assert.Equal("original literal", log.Message);
        Assert.False(observable.HasUnsavedChanges);
        variable.Value = "next run";
        Assert.True(await flow.StartFromActionAsync(log, CancellationToken.None));
        Assert.Equal("next run", log.LastResolvedMessage);
        Assert.Equal("original literal", log.Message);
    }

    [Fact]
    public async Task MissingVariable_FailsNestedActionAndStopsFollowingActions()
    {
        var flow = new TestFlow();
        var missing = new ChangeTextCaseAction { Text = "literal" };
        ((IAction)missing).TextVariableBindings = new Dictionary<string, string> { [nameof(missing.Text)] = "removed" };
        var following = new LogAction { Message = "should not run" };
        var branch = new IfAction { Condition = true };
        branch.IfBlock.Add(missing);
        branch.IfBlock.Add(following);
        flow.Actions.Add(branch);

        Assert.False(await flow.StartAsync(CancellationToken.None));
        Assert.Equal(ActionState.Failed, missing.State);
        Assert.Equal(ActionState.Ready, following.State);
        Assert.Equal("literal", missing.Text);
    }

    [Fact]
    public async Task SavingDuringExecutionAndCancellation_PreserveLiteralAndVariableSource()
    {
        var flow = new TestFlow();
        flow.Environment.Variables.Add(new Variable("path", Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".missing"), typeof(string)));
        var waiting = new WaitForFileAction { FilePath = "saved literal", TimeoutMilliseconds = 10000, PollingIntervalMilliseconds = 10 };
        ((IAction)waiting).TextVariableBindings = new Dictionary<string, string> { [nameof(waiting.FilePath)] = "path" };
        flow.Actions.Add(waiting);
        using var cancellation = new CancellationTokenSource();
        var execution = ActionTextParameters.ExecuteAsync(waiting, flow.Environment, cancellation.Token);
        try
        {
            var loaded = TestFlowXmlSerializer.DeserializeFromXml(TestFlowXmlSerializer.SerializeToXml(flow))!;
            var savedAction = Assert.IsType<WaitForFileAction>(Assert.Single(loaded.Actions));
            Assert.Equal("saved literal", savedAction.FilePath);
            Assert.Equal("path", ((IAction)savedAction).TextVariableBindings[nameof(waiting.FilePath)]);
        }
        finally
        {
            cancellation.Cancel();
            Assert.False(await execution);
        }
        Assert.Equal("saved literal", waiting.FilePath);
    }

    [Fact]
    public void SaveReloadAndActionSnapshot_PreserveNestedBindings()
    {
        var flow = new TestFlow();
        var text = new ReplaceTextAction { Text = "literal", SearchText = "a", ReplacementText = "b" };
        ((IAction)text).TextVariableBindings = new Dictionary<string, string> { [nameof(text.Text)] = "source" };
        var branch = new IfAction { Condition = true };
        branch.IfBlock.Add(text);
        flow.Actions.Add(branch);
        var loaded = TestFlowXmlSerializer.DeserializeFromXml(TestFlowXmlSerializer.SerializeToXml(flow))!;
        var restored = Assert.IsType<IfAction>(new ActionSnapshotService().Capture(loaded.Actions[0]).Restore());
        var restoredText = Assert.IsType<ReplaceTextAction>(Assert.Single(restored.IfBlock));
        Assert.Equal("source", ((IAction)restoredText).TextVariableBindings[nameof(text.Text)]);
        Assert.Equal("literal", restoredText.Text);
    }

    [Fact]
    public void AvailableVariables_RespectOrderBranchesDisabledDeclarationsAndLoopInitialization()
    {
        var flow = new TestFlow();
        SetVariableAction<object> Declare(string name) => new(name, "value") { Environment = flow.Environment };
        var before = Declare("before");
        var local = Declare("local");
        var disabled = Declare("disabled");
        disabled.IsEnabled = false;
        var future = Declare("future");
        var target = new LogAction();
        var otherBranch = new LogAction();
        var branch = new IfAction { Condition = true };
        branch.IfBlock.Add(local);
        branch.IfBlock.Add(target);
        branch.ElseBlock.Add(otherBranch);
        var loopTarget = new LogAction();
        var loop = new ForLoopAction { Environment = flow.Environment, Initialization = Declare("index"), Condition = false };
        loop.Body.Add(loopTarget);
        flow.Actions.Add(before);
        flow.Actions.Add(disabled);
        flow.Actions.Add(branch);
        flow.Actions.Add(loop);
        flow.Actions.Add(future);
        flow.Environment.Variables.Add(new Variable("future", "stale runtime value", typeof(string)));

        string[] Names(IAction action) => ActionVariableScope.GetAvailableVariables(flow.Actions, flow.Environment, action).Select(v => v.Name).ToArray();
        Assert.Equal(new[] { "before", "local" }, Names(target));
        Assert.Equal(new[] { "before" }, Names(otherBranch));
        Assert.Equal(new[] { "before", "index" }, Names(loopTarget));
        Assert.Equal(new[] { "before" }, Names(future));
    }

    [Fact]
    public void VariableSourceEdit_CanUndoAndRedo()
    {
        var flow = new ObservableFlow(new TestFlow());
        IAction action = new LogAction { Message = "literal" };
        flow.Actions.Add(action);
        flow.UndoRedoService.MarkSavedCheckpoint();
        flow.UndoRedoService.Execute(new ActionPropertyEdit([0], nameof(IAction.TextVariableBindings),
            action.TextVariableBindings, new Dictionary<string, string> { [nameof(LogAction.Message)] = "message" }));
        Assert.True(flow.HasUnsavedChanges);
        Assert.Equal("message", action.TextVariableBindings[nameof(LogAction.Message)]);
        flow.UndoRedoService.Undo();
        Assert.Empty(action.TextVariableBindings);
        flow.UndoRedoService.Redo();
        Assert.Equal("message", action.TextVariableBindings[nameof(LogAction.Message)]);
    }
}
