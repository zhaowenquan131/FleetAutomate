using System.Reflection;
using FleetAutomate.ViewModel;

namespace FleetAutomate.UndoRedo;

public sealed class ActionPropertyEdit : IUndoableFlowEdit
{
    private readonly int[] _actionPath;
    private readonly string _propertyName;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    public ActionPropertyEdit(int[] actionPath, string propertyName, object? oldValue, object? newValue, string? displayName = null)
    {
        _actionPath = actionPath ?? throw new ArgumentNullException(nameof(actionPath));
        _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _oldValue = oldValue;
        _newValue = newValue;
        DisplayName = displayName ?? $"Edit {propertyName}";
    }

    public string DisplayName { get; }

    public void Apply(ObservableFlow flow) => SetValue(flow, _newValue);

    public void Unapply(ObservableFlow flow) => SetValue(flow, _oldValue);

    private void SetValue(ObservableFlow flow, object? value)
    {
        var action = ActionCollectionRef.Root.ResolveAction(flow, _actionPath);
        var property = action.GetType().GetProperty(_propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Action property '{_propertyName}' was not found.");

        property.SetValue(action, value);
    }
}
