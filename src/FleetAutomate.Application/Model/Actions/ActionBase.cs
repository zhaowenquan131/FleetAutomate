using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Model.Actions;

[DataContract]
public abstract class ActionBase : IAction, INotifyPropertyChanged
{
    private string _description = string.Empty;
    private ActionState _state = ActionState.Ready;

    public abstract string Name { get; }

    [DataMember]
    public string Description
    {
        get => string.IsNullOrWhiteSpace(_description) ? DefaultDescription : _description;
        set => SetProperty(ref _description, value);
    }

    protected virtual string DefaultDescription => Name;

    [IgnoreDataMember]
    public ActionState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    public bool IsEnabled => true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public virtual void Cancel()
    {
    }

    public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        State = ActionState.Running;
        await Task.Yield();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteCoreAsync(cancellationToken);
            State = ActionState.Completed;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            State = ActionState.Paused;
            return false;
        }
        catch
        {
            State = ActionState.Failed;
            return false;
        }
    }

    protected abstract Task ExecuteCoreAsync(CancellationToken cancellationToken);

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
