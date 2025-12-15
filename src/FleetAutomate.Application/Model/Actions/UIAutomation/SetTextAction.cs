using System.Runtime.Serialization;
using System.ComponentModel;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FleetAutomate.Model.Flow;
using FleetAutomate.Helpers;

namespace FleetAutomate.Model.Actions.UIAutomation
{
    /// <summary>
    /// Action to set text in a UI input element.
    /// </summary>
    [DataContract]
    public class SetTextAction : IAction, INotifyPropertyChanged
    {
        public string Name => "Set Text";

        [DataMember]
        public string Description { get; set; } = "Set text in an input element";

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
        /// The text to set in the input element
        /// </summary>
        [DataMember]
        public string TextToSet { get; set; } = string.Empty;

        /// <summary>
        /// Whether to clear existing text before setting new text
        /// </summary>
        [DataMember]
        public bool ClearExistingText { get; set; } = true;

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
            global::System.Diagnostics.Debug.WriteLine($"[SetText] Starting - IdentifierType: {IdentifierType}, Identifier: {ElementIdentifier}, TextToSet: {TextToSet}");

            try
            {
                if (string.IsNullOrWhiteSpace(ElementIdentifier))
                {
                    global::System.Diagnostics.Debug.WriteLine("[SetText] ERROR: Element identifier is empty");
                    throw new InvalidOperationException("Element identifier cannot be empty");
                }

                // Initialize automation (UIA3)
                global::System.Diagnostics.Debug.WriteLine("[SetText] Initializing UIA3 automation...");
                _automation = new UIA3Automation();
                var desktop = _automation.GetDesktop();
                global::System.Diagnostics.Debug.WriteLine("[SetText] Desktop obtained successfully");

                // Try to find the element
                global::System.Diagnostics.Debug.WriteLine("[SetText] Searching for element...");
                var element = UIAutomationHelper.FindElement(desktop, IdentifierType, ElementIdentifier, "SetText");
                if (element == null)
                {
                    global::System.Diagnostics.Debug.WriteLine("[SetText] ERROR: Element not found");
                    State = ActionState.Failed;
                    return false;  // Element not found
                }

                global::System.Diagnostics.Debug.WriteLine($"[SetText] Element found: {element.Name}");

                // Try to set text using Value pattern (most reliable for input fields)
                if (element.Patterns.Value.IsSupported)
                {
                    global::System.Diagnostics.Debug.WriteLine("[SetText] Value pattern is supported, using it...");
                    var valuePattern = element.Patterns.Value.Pattern;

                    if (ClearExistingText || string.IsNullOrEmpty(valuePattern.Value.ValueOrDefault))
                    {
                        global::System.Diagnostics.Debug.WriteLine("[SetText] Setting text directly...");
                        valuePattern.SetValue(TextToSet);
                    }
                    else
                    {
                        global::System.Diagnostics.Debug.WriteLine("[SetText] Appending text to existing value...");
                        var currentValue = valuePattern.Value.ValueOrDefault ?? string.Empty;
                        valuePattern.SetValue(currentValue + TextToSet);
                    }

                    global::System.Diagnostics.Debug.WriteLine("[SetText] Text set successfully using Value pattern");
                }
                // Fallback to direct text input via keyboard
                else
                {
                    global::System.Diagnostics.Debug.WriteLine("[SetText] Value pattern not supported, using keyboard simulation...");

                    // Focus the element
                    element.Focus();

                    // Wait a bit for focus to take effect
                    await Task.Delay(100, cancellationToken);

                    // Clear if requested
                    if (ClearExistingText)
                    {
                        global::System.Diagnostics.Debug.WriteLine("[SetText] Simulating Ctrl+A to select all...");
                        FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                        await Task.Delay(50, cancellationToken);
                    }

                    // Type the text
                    global::System.Diagnostics.Debug.WriteLine("[SetText] Typing text via keyboard...");
                    FlaUI.Core.Input.Keyboard.Type(TextToSet);

                    global::System.Diagnostics.Debug.WriteLine("[SetText] Text set successfully using keyboard simulation");
                }

                global::System.Diagnostics.Debug.WriteLine("[SetText] Text operation completed successfully");
                State = ActionState.Completed;
                return true;
            }
            catch (OperationCanceledException)
            {
                global::System.Diagnostics.Debug.WriteLine("[SetText] Operation cancelled");
                State = ActionState.Failed;
                return false;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[SetText] EXCEPTION: {ex.GetType().Name} - {ex.Message}");
                global::System.Diagnostics.Debug.WriteLine($"[SetText] Stack trace: {ex.StackTrace}");
                State = ActionState.Failed;
                return false;
            }
            finally
            {
                // Cleanup automation
                try
                {
                    _automation?.Dispose();
                    global::System.Diagnostics.Debug.WriteLine("[SetText] Automation disposed");
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }
}
