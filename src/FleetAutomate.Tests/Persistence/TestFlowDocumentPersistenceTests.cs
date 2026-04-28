using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Expression;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Flow;
using FleetAutomate.Expressions;

namespace FleetAutomate.Tests.Persistence;

public class TestFlowDocumentPersistenceTests
{
    [Fact]
    public void SerializeToXml_WritesDocumentFormatInsteadOfRuntimeKnownTypes()
    {
        var flow = new TestFlow
        {
            Name = "Document Flow",
            Description = "DTO-backed flow"
        };
        flow.Actions.Add(new WaitDurationAction { Duration = 3, Unit = WaitDurationUnit.Seconds });
        flow.State = ActionState.Completed;
        flow.GlobalElementDictionary.RegisterKey("main-window");

        var xml = TestFlowXmlSerializer.SerializeToXml(flow);

        Assert.Contains("TestFlowDocument", xml);
        Assert.Contains("formatVersion", xml);
        Assert.Contains("system.waitDuration", xml);
        Assert.DoesNotContain("ActionsArray", xml);
        Assert.DoesNotContain("State", xml);
        Assert.DoesNotContain("CurrentAction", xml);
    }

    [Fact]
    public void DocumentRoundTrip_PreservesVariablesGlobalKeysAndNestedActionOrder()
    {
        var flow = new TestFlow
        {
            Name = "Nested Flow",
            Description = "Contains branches",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment
            {
                Variables =
                [
                    new Variable("count", 42, typeof(int)),
                    new Variable("ready", true, typeof(bool))
                ]
            }
        };
        flow.GlobalElementDictionary.RegisterKey("calculator");

        var ifAction = new IfAction
        {
            Condition = true,
            Environment = flow.Environment,
            Description = "branch"
        };
        ifAction.IfBlock.Add(new WaitDurationAction { Duration = 1, Unit = WaitDurationUnit.Seconds });
        ifAction.ElseBlock.Add(new WaitDurationAction { Duration = 2, Unit = WaitDurationUnit.Minutes });
        flow.Actions.Add(ifAction);

        var loaded = TestFlowXmlSerializer.DeserializeFromXml(TestFlowXmlSerializer.SerializeToXml(flow));

        Assert.NotNull(loaded);
        Assert.Equal("Nested Flow", loaded!.Name);
        Assert.Equal("Contains branches", loaded.Description);
        Assert.Equal(2, loaded.Environment.Variables.Count);
        Assert.Equal(42, loaded.Environment.Variables.Single(v => v.Name == "count").Value);
        Assert.True(loaded.GlobalElementDictionary.ContainsKey("calculator"));

        var loadedIf = Assert.IsType<IfAction>(Assert.Single(loaded.Actions));
        var thenWait = Assert.IsType<WaitDurationAction>(Assert.Single(loadedIf.IfBlock));
        var elseWait = Assert.IsType<WaitDurationAction>(Assert.Single(loadedIf.ElseBlock));
        Assert.Equal(1, thenWait.Duration);
        Assert.Equal(WaitDurationUnit.Seconds, thenWait.Unit);
        Assert.Equal(2, elseWait.Duration);
        Assert.Equal(WaitDurationUnit.Minutes, elseWait.Unit);
        Assert.Equal(ActionState.Ready, loadedIf.State);
    }

    [Fact]
    public void DocumentRoundTrip_PreservesLogActionExpressionMode()
    {
        var flow = new TestFlow
        {
            Name = "Log Expression Flow"
        };
        flow.Actions.Add(new LogAction
        {
            LogLevel = LogLevel.Warn,
            MessageMode = LogMessageMode.Expression,
            Message = "getUiProperty(\"Window/Pane/Button\", \"Name\").ContainsText(\"window\")"
        });

        var loaded = TestFlowXmlSerializer.DeserializeFromXml(TestFlowXmlSerializer.SerializeToXml(flow));

        var loadedLog = Assert.IsType<LogAction>(Assert.Single(loaded!.Actions));
        Assert.Equal(LogLevel.Warn, loadedLog.LogLevel);
        Assert.Equal(LogMessageMode.Expression, loadedLog.MessageMode);
        Assert.Equal("getUiProperty(\"Window/Pane/Button\", \"Name\").ContainsText(\"window\")", loadedLog.Message);
    }

    [Fact]
    public void DocumentRoundTrip_PreservesIfActionUiElementExistsConditionAsExpression()
    {
        const string expressionText = "uiExists('//Pane[@Name=\"桌面 1\"]//Window[@Name=\"计算器\"]//Window[@Name=\"计算器\"]')";
        var flow = new TestFlow
        {
            Name = "If Condition Flow"
        };
        flow.Actions.Add(new IfAction
        {
            Condition = new ExpressionDocument
            {
                TypeId = "logic",
                RawText = expressionText,
                ResultTypeId = TypeIds.Bool
            },
            Environment = flow.Environment,
            Description = "Name: 计算器 (retry:3x)"
        });

        var xml = TestFlowXmlSerializer.SerializeToXml(flow);
        var loaded = TestFlowXmlSerializer.DeserializeFromXml(xml);

        Assert.Contains("ConditionExpressionRawText", xml);
        Assert.Contains("uiExists", xml);
        var loadedIf = Assert.IsType<IfAction>(Assert.Single(loaded!.Actions));
        var condition = Assert.IsType<ExpressionDocument>(loadedIf.Condition);
        Assert.Equal("logic", condition.TypeId);
        Assert.Equal(expressionText, condition.RawText);
        Assert.Equal(TypeIds.Bool, condition.ResultTypeId);
    }

    [Fact]
    public void DeserializeFromXml_FallsBackToLegacyRuntimeXml()
    {
        var flow = new TestFlow { Name = "Legacy Flow", Description = "old" };
        flow.Actions.Add(new WaitDurationAction { Duration = 5, Unit = WaitDurationUnit.Seconds });

        var legacyXml = TestFlowXmlSerializer.SerializeLegacyRuntimeXmlForMigration(flow);

        var loaded = TestFlowXmlSerializer.DeserializeFromXml(legacyXml);

        Assert.NotNull(loaded);
        Assert.Equal("Legacy Flow", loaded!.Name);
        var wait = Assert.IsType<WaitDurationAction>(Assert.Single(loaded.Actions));
        Assert.Equal(5, wait.Duration);
    }

    [Fact]
    public void LegacyXmlCanBeMigratedToDocumentXmlWithoutChangingRuntimeShape()
    {
        var original = new TestFlow
        {
            Name = "Legacy Migration",
            Description = "roundtrip",
            Environment = new FleetAutomate.Model.Actions.Logic.Environment
            {
                Variables = [new Variable("threshold", 10, typeof(int))]
            }
        };
        original.GlobalElementDictionary.RegisterKey("root");
        var ifAction = new IfAction { Condition = true, Environment = original.Environment };
        ifAction.IfBlock.Add(new WaitDurationAction { Duration = 4, Unit = WaitDurationUnit.Seconds });
        ifAction.ElseBlock.Add(new WaitDurationAction { Duration = 1, Unit = WaitDurationUnit.Minutes });
        original.Actions.Add(ifAction);

        var migratedFromLegacy = TestFlowXmlSerializer.DeserializeFromXml(
            TestFlowXmlSerializer.SerializeLegacyRuntimeXmlForMigration(original));

        var documentXml = TestFlowXmlSerializer.SerializeToXml(migratedFromLegacy!);
        var reloadedFromDocument = TestFlowXmlSerializer.DeserializeFromXml(documentXml);

        AssertEquivalentRuntimeShape(migratedFromLegacy!, reloadedFromDocument!);
    }

    private static void AssertEquivalentRuntimeShape(TestFlow expected, TestFlow actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.Environment.Variables.Count, actual.Environment.Variables.Count);
        Assert.Equal(expected.GlobalElementDictionary.Keys.Order(StringComparer.Ordinal), actual.GlobalElementDictionary.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(expected.Actions.Count, actual.Actions.Count);

        var expectedIf = Assert.IsType<IfAction>(Assert.Single(expected.Actions));
        var actualIf = Assert.IsType<IfAction>(Assert.Single(actual.Actions));
        Assert.Equal(expectedIf.IfBlock.Count, actualIf.IfBlock.Count);
        Assert.Equal(expectedIf.ElseBlock.Count, actualIf.ElseBlock.Count);
        Assert.Equal(
            ((WaitDurationAction)Assert.Single(expectedIf.IfBlock)).Duration,
            ((WaitDurationAction)Assert.Single(actualIf.IfBlock)).Duration);
        Assert.Equal(
            ((WaitDurationAction)Assert.Single(expectedIf.ElseBlock)).Unit,
            ((WaitDurationAction)Assert.Single(actualIf.ElseBlock)).Unit);
    }
}
