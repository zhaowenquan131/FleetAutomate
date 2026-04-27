using System.Reflection;
using FleetAutomate.ViewModel;

namespace FleetAutomate.UndoRedo;

public sealed class FlowPropertyEdit : IUndoableFlowEdit
{
    private readonly string _propertyName;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    public FlowPropertyEdit(ObservableFlow flow, string propertyName, object? oldValue, object? newValue, string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(flow);
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
        var property = flow.GetType().GetProperty(_propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Flow property '{_propertyName}' was not found.");

        property.SetValue(flow, value);
    }
}
