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
    /// Action to click on a UI element.
    /// </summary>
    [DataContract]
    public class ClickElementAction : IAction, INotifyPropertyChanged
    {
        public string Name => "Click Element";

        [DataMember]
        public string Description { get; set; } = "Click on a UI element";

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
        /// Whether to perform a double-click instead of single-click
        /// </summary>
        [DataMember]
        public bool IsDoubleClick { get; set; } = false;

        /// <summary>
        /// Whether to use Invoke pattern instead of actual mouse clicking.
        /// Useful when element is behind another element or not directly clickable.
        /// </summary>
        [DataMember]
        public bool UseInvoke { get; set; } = false;

        /// <summary>
        /// The automation instance (not serialized)
        /// </summary>
        [IgnoreDataMember]
        private AutomationBase? _automation;

        public void Cancel()
        {
            // No specific cleanup needed
            if (_automation != null)
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
            global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Starting - IdentifierType: {IdentifierType}, Identifier: {ElementIdentifier}, IsDoubleClick: {IsDoubleClick}");

            try
            {
                if (string.IsNullOrWhiteSpace(ElementIdentifier))
                {
                    global::System.Diagnostics.Debug.WriteLine("[ClickElement] ERROR: Element identifier is empty");
                    throw new InvalidOperationException("Element identifier cannot be empty");
                }

                // Initialize automation (UIA3)
                global::System.Diagnostics.Debug.WriteLine("[ClickElement] Initializing UIA3 automation...");
                _automation = new UIA3Automation();
                var desktop = _automation.GetDesktop();
                global::System.Diagnostics.Debug.WriteLine("[ClickElement] Desktop obtained successfully");

                // Try to find the element
                global::System.Diagnostics.Debug.WriteLine("[ClickElement] Searching for element...");
                var element = UIAutomationHelper.FindElement(desktop, IdentifierType, ElementIdentifier, "ClickElement");
                if (element == null)
                {
                    global::System.Diagnostics.Debug.WriteLine("[ClickElement] ERROR: Element not found");
                    State = ActionState.Failed;
                    return false;  // Element not found
                }

                global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Element found: {element.Name}");

                // Perform the click
                if (UseInvoke)
                {
                    global::System.Diagnostics.Debug.WriteLine("[ClickElement] Using Invoke pattern...");
                    // Try to use Invoke pattern (works for buttons and other invoke-able elements)
                    if (element.Patterns.Invoke.IsSupported)
                    {
                        global::System.Diagnostics.Debug.WriteLine("[ClickElement] Invoke pattern is supported, invoking...");
                        element.Patterns.Invoke.Pattern.Invoke();
                        global::System.Diagnostics.Debug.WriteLine("[ClickElement] Invoke completed successfully");
                    }
                    else
                    {
                        global::System.Diagnostics.Debug.WriteLine("[ClickElement] WARNING: Invoke pattern not supported, falling back to Click");
                        element.Click();
                    }
                }
                else if (IsDoubleClick)
                {
                    global::System.Diagnostics.Debug.WriteLine("[ClickElement] Performing double-click...");
                    element.DoubleClick();
                }
                else
                {
                    global::System.Diagnostics.Debug.WriteLine("[ClickElement] Performing single-click...");
                    element.Click();
                }

                global::System.Diagnostics.Debug.WriteLine("[ClickElement] Click completed successfully");
                State = ActionState.Completed;
                return true;
            }
            catch (OperationCanceledException)
            {
                global::System.Diagnostics.Debug.WriteLine("[ClickElement] Operation cancelled");
                State = ActionState.Failed;
                return false;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[ClickElement] EXCEPTION: {ex.GetType().Name} - {ex.Message}");
                global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Stack trace: {ex.StackTrace}");
                State = ActionState.Failed;
                return false;
            }
            finally
            {
                // Cleanup automation
                try
                {
                    _automation?.Dispose();
                    global::System.Diagnostics.Debug.WriteLine("[ClickElement] Automation disposed");
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }
}
