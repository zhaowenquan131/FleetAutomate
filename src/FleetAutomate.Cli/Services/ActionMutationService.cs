using FleetAutomate.Cli.Infrastructure;
using SharedActionMutationService = FleetAutomate.Application.Commanding.ActionMutationService;

namespace FleetAutomate.Cli.Services;

internal sealed class ActionMutationService
{
    private readonly SharedActionMutationService _inner = new();

    public FleetAutomate.Model.IAction CreateAction(string type)
    {
        try
        {
            return _inner.CreateAction(type);
        }
        catch (InvalidOperationException ex)
        {
            throw new CliUsageException(ex.Message);
        }
    }

    public string AddAction(FleetAutomate.Model.Flow.TestFlow flow, FleetAutomate.Model.IAction action, string? parentPath, string? containerName, int? index)
    {
        try
        {
            return _inner.AddAction(flow, action, parentPath, containerName, index);
        }
        catch (InvalidOperationException ex)
        {
            throw new CliUsageException(ex.Message);
        }
    }

    public void RemoveAction(FleetAutomate.Model.Flow.TestFlow flow, string path)
    {
        try
        {
            _inner.RemoveAction(flow, path);
        }
        catch (InvalidOperationException ex)
        {
            throw new CliUsageException(ex.Message);
        }
    }

    public void SetProperty(FleetAutomate.Model.Flow.TestFlow flow, string path, string propertyName, string value)
    {
        try
        {
            _inner.SetProperty(flow, path, propertyName, value);
        }
        catch (InvalidOperationException ex)
        {
            throw new CliUsageException(ex.Message);
        }
    }
}
