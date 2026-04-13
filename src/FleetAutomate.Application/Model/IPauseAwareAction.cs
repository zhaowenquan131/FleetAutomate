using FleetAutomate.Model.Flow;

namespace FleetAutomate.Model
{
    public interface IPauseAwareAction : IAction
    {
        ActionPauseBehavior PauseBehavior { get; }
    }
}
