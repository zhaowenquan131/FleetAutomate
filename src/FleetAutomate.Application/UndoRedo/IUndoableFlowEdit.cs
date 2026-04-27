using FleetAutomate.ViewModel;

namespace FleetAutomate.UndoRedo;

public interface IUndoableFlowEdit
{
    string DisplayName { get; }

    void Apply(ObservableFlow flow);

    void Unapply(ObservableFlow flow);
}
