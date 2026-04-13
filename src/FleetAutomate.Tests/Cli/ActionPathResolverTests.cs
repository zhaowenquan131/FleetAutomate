using FleetAutomate.Cli.Services;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.System;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Tests.Cli;

public sealed class ActionPathResolverTests
{
    [Fact]
    public void Flatten_ProducesExpectedNestedPaths()
    {
        var flow = new TestFlow
        {
            Name = "basic_flow"
        };

        flow.Actions.Add(new LogAction
        {
            Message = "top-level"
        });

        var ifAction = new IfAction
        {
            Condition = true,
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };
        ifAction.IfBlock.Add(new LogAction { Message = "if-branch" });
        ifAction.ElseBlock.Add(new LogAction { Message = "else-branch" });
        ifAction.ElseIfs.Add(new IfAction
        {
            Condition = false,
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        });

        flow.Actions.Add(ifAction);

        var resolver = new ActionPathResolver();

        var paths = resolver.Flatten(flow).Select(node => node.Path).ToList();

        Assert.Contains("0", paths);
        Assert.Contains("1", paths);
        Assert.Contains("1.if.0", paths);
        Assert.Contains("1.else.0", paths);
        Assert.Contains("1.elseif.0", paths);
    }

    [Fact]
    public void Resolve_ReturnsNestedActionByPath()
    {
        var flow = new TestFlow
        {
            Name = "basic_flow"
        };

        var ifAction = new IfAction
        {
            Condition = true,
            Environment = new FleetAutomate.Model.Actions.Logic.Environment()
        };

        var nestedLog = new LogAction
        {
            Message = "nested"
        };
        ifAction.IfBlock.Add(nestedLog);
        flow.Actions.Add(ifAction);

        var resolver = new ActionPathResolver();

        var resolved = resolver.Resolve(flow, "0.if.0");

        Assert.Same(nestedLog, resolved.Action);
        Assert.Equal("if", resolved.Container);
        Assert.Equal("0.if.0", resolved.Path);
    }
}
