using FleetAutomate.Model;
using FleetAutomate.Model.Flow;
using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Model.Actions
{
    /// <summary>
    /// Placeholder action for features that are not yet implemented.
    /// This action will fail when executed with a clear error message.
    /// </summary>
    [DataContract]
    public class NotImplementedAction : IAction, INotifyPropertyChanged
    {
        /// <summary>
        /// Gets or sets the name of the action.
        /// </summary>
        [DataMember]
        public string Name { get; set; } = "Not Implemented";

        /// <summary>
        /// Gets or sets the description of the action.
        /// </summary>
        [DataMember]
        public string Description { get; set; } = "This action is not yet implemented";

        private ActionState _state = ActionState.Ready;

        /// <summary>
        /// Gets or sets the current state of the action.
        /// </summary>
        [DataMember]
        public ActionState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this action is enabled.
        /// </summary>
        public bool IsEnabled => true;

        /// <summary>
        /// Gets or sets the name of the planned action that this is a placeholder for.
        /// </summary>
        [DataMember]
        public string PlannedActionName { get; set; } = "";

        /// <summary>
        /// Property changed event for data binding.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Executes the action asynchronously. Always fails with NotImplementedException.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. Always returns false.</returns>
        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            State = ActionState.Failed;
            await Task.CompletedTask;
            throw new NotImplementedException($"Action '{PlannedActionName}' is not yet implemented. This is a placeholder action that will be fully implemented in a future release.");
        }

        /// <summary>
        /// Cancels the action execution.
        /// </summary>
        public void Cancel()
        {
            // Nothing to cancel for a placeholder action
        }
    }
}
