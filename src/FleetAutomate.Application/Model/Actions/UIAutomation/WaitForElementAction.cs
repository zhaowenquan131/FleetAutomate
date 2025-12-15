using System.Runtime.Serialization;
using System.ComponentModel;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using FleetAutomate.Model.Flow;
using FleetAutomate.Helpers;

namespace FleetAutomate.Model.Actions.UIAutomation
{
    /// <summary>
    /// Action to wait for a UI element to exist with a specified timeout.
    /// </summary>
    [DataContract]
    public class WaitForElementAction : IAction, INotifyPropertyChanged
    {
        public string Name => "Wait for Element";

        [DataMember]
        public string Description { get; set; } = "Wait until a UI element exists";

        [DataMember]
        private ActionState _state = ActionState.Ready;

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

        public bool IsEnabled => true;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// The identifier to search for (XPath, AutomationId, Name, or ClassName)
        /// </summary>
        [DataMember]
        public string ElementIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// The type of identifier: "XPath", "AutomationId", "Name", "ClassName"
        /// </summary>
        [DataMember]
        public string IdentifierType { get; set; } = "XPath";

        /// <summary>
        /// Maximum time in milliseconds to wait for the element
        /// </summary>
        [DataMember]
        public int TimeoutMilliseconds { get; set; } = 30000;

        /// <summary>
        /// How often to check for the element (in milliseconds)
        /// </summary>
        [DataMember]
        public int PollingIntervalMilliseconds { get; set; } = 100;

        /// <summary>
        /// The automation instance (not serialized)
        /// </summary>
        [IgnoreDataMember]
        private AutomationBase? _automation;

        public void Cancel()
        {
            // No specific cleanup needed
            if (_automation != null && _automation != null)
            {
                try
                {
                    _automation.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            State = ActionState.Running;
            global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Starting - IdentifierType: {IdentifierType}, Identifier: {ElementIdentifier}, Timeout: {TimeoutMilliseconds}ms");

            try
            {
                if (string.IsNullOrWhiteSpace(ElementIdentifier))
                {
                    global::System.Diagnostics.Debug.WriteLine("[WaitForElement] ERROR: Element identifier is empty");
                    throw new InvalidOperationException("Element identifier cannot be empty");
                }

                // Initialize automation (UIA3)
                global::System.Diagnostics.Debug.WriteLine("[WaitForElement] Initializing UIA3 automation...");
                _automation = new UIA3Automation();
                var desktop = _automation.GetDesktop();
                global::System.Diagnostics.Debug.WriteLine("[WaitForElement] Desktop obtained successfully");

                var startTime = DateTime.UtcNow;
                int attemptCount = 0;

                while (true)
                {
                    attemptCount++;
                    global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Attempt #{attemptCount} - Searching for element...");

                    // Check for cancellation
                    if (cancellationToken.IsCancellationRequested)
                    {
                        global::System.Diagnostics.Debug.WriteLine("[WaitForElement] Cancellation requested");
                        State = ActionState.Failed;
                        return false;
                    }

                    // Try to find the element
                    var element = UIAutomationHelper.FindElement(desktop, IdentifierType, ElementIdentifier, "WaitForElement");
                    if (element != null)
                    {
                        global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] SUCCESS! Element found after {attemptCount} attempts in {(DateTime.UtcNow - startTime).TotalMilliseconds}ms");
                        State = ActionState.Completed;
                        return true;  // Element found!
                    }

                    global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Element not found on attempt #{attemptCount}");

                    // Check timeout
                    var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    if (elapsed > TimeoutMilliseconds)
                    {
                        global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] TIMEOUT! Elapsed {elapsed}ms > Timeout {TimeoutMilliseconds}ms after {attemptCount} attempts");
                        State = ActionState.Failed;
                        return false;  // Timeout
                    }

                    // Wait before next attempt
                    global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Waiting {PollingIntervalMilliseconds}ms before next attempt...");
                    await Task.Delay(PollingIntervalMilliseconds, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                global::System.Diagnostics.Debug.WriteLine("[WaitForElement] Operation cancelled");
                State = ActionState.Failed;
                return false;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] EXCEPTION: {ex.GetType().Name} - {ex.Message}");
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Stack trace: {ex.StackTrace}");
                State = ActionState.Failed;
                return false;
            }
            finally
            {
                // Cleanup automation
                try
                {
                    _automation?.Dispose();
                    global::System.Diagnostics.Debug.WriteLine("[WaitForElement] Automation disposed");
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }
}
