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
    public class SetTextAction : IRetryableAction, INotifyPropertyChanged
    {
        public string Name => "Set Text";

        [DataMember]
        private string _description = "Set text in an input element";
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                }
            }
        }

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
        private string _elementIdentifier = string.Empty;
        public string ElementIdentifier
        {
            get => _elementIdentifier;
            set
            {
                if (_elementIdentifier != value)
                {
                    _elementIdentifier = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ElementIdentifier)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        /// <summary>
        /// The type of identifier: "XPath", "AutomationId", "Name", "ClassName"
        /// </summary>
        [DataMember]
        private string _identifierType = "XPath";
        public string IdentifierType
        {
            get => _identifierType;
            set
            {
                if (_identifierType != value)
                {
                    _identifierType = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IdentifierType)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        /// <summary>
        /// The text to set in the input element
        /// </summary>
        [DataMember]
        private string _textToSet = string.Empty;
        public string TextToSet
        {
            get => _textToSet;
            set
            {
                if (_textToSet != value)
                {
                    _textToSet = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextToSet)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        /// <summary>
        /// Whether to clear existing text before setting new text
        /// </summary>
        [DataMember]
        private bool _clearExistingText = true;
        public bool ClearExistingText
        {
            get => _clearExistingText;
            set
            {
                if (_clearExistingText != value)
                {
                    _clearExistingText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClearExistingText)));
                }
            }
        }

        /// <summary>
        /// Number of times to retry if the action fails (0 means no retry, just one attempt)
        /// </summary>
        [DataMember]
        private int _retryTimes = 3;
        public int RetryTimes
        {
            get => _retryTimes;
            set
            {
                if (_retryTimes != value)
                {
                    _retryTimes = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RetryTimes)));
                }
            }
        }

        /// <summary>
        /// Delay in milliseconds between retry attempts
        /// </summary>
        [DataMember]
        private int _retryDelayMilliseconds = 500;
        public int RetryDelayMilliseconds
        {
            get => _retryDelayMilliseconds;
            set
            {
                if (_retryDelayMilliseconds != value)
                {
                    _retryDelayMilliseconds = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RetryDelayMilliseconds)));
                }
            }
        }

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
            // Yield to allow UI to update the action state immediately
            await Task.Yield();
            global::System.Diagnostics.Debug.WriteLine($"[SetText] Starting - IdentifierType: {IdentifierType}, Identifier: {ElementIdentifier}, TextToSet: {TextToSet}, RetryTimes: {RetryTimes}");

            if (string.IsNullOrWhiteSpace(ElementIdentifier))
            {
                global::System.Diagnostics.Debug.WriteLine("[SetText] ERROR: Element identifier is empty");
                State = ActionState.Failed;
                return false;
            }

            int maxAttempts = RetryTimes + 1; // RetryTimes = 3 means 4 total attempts (1 initial + 3 retries)
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Check for cancellation before each attempt
                if (cancellationToken.IsCancellationRequested)
                {
                    global::System.Diagnostics.Debug.WriteLine("[SetText] Cancellation requested");
                    State = ActionState.Failed;
                    cancellationToken.ThrowIfCancellationRequested();
                }

                try
                {
                    global::System.Diagnostics.Debug.WriteLine($"[SetText] Attempt {attempt}/{maxAttempts}");

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
                        global::System.Diagnostics.Debug.WriteLine($"[SetText] Element not found on attempt {attempt}/{maxAttempts}");

                        // Cleanup before retry
                        try
                        {
                            _automation?.Dispose();
                        }
                        catch
                        {
                            // Ignore
                        }

                        // If this is not the last attempt, wait and retry
                        if (attempt < maxAttempts)
                        {
                            global::System.Diagnostics.Debug.WriteLine($"[SetText] Waiting {RetryDelayMilliseconds}ms before retry...");
                            await Task.Delay(RetryDelayMilliseconds, cancellationToken);
                            continue;
                        }
                        else
                        {
                            global::System.Diagnostics.Debug.WriteLine($"[SetText] ERROR: Element not found after {maxAttempts} attempts");
                            State = ActionState.Failed;
                            return false;
                        }
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

                    global::System.Diagnostics.Debug.WriteLine($"[SetText] Text operation completed successfully on attempt {attempt}/{maxAttempts}");
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
                    lastException = ex;
                    global::System.Diagnostics.Debug.WriteLine($"[SetText] EXCEPTION on attempt {attempt}/{maxAttempts}: {ex.GetType().Name} - {ex.Message}");

                    // Cleanup before retry
                    try
                    {
                        _automation?.Dispose();
                    }
                    catch
                    {
                        // Ignore
                    }

                    // If this is not the last attempt, wait and retry
                    if (attempt < maxAttempts)
                    {
                        global::System.Diagnostics.Debug.WriteLine($"[SetText] Waiting {RetryDelayMilliseconds}ms before retry...");
                        await Task.Delay(RetryDelayMilliseconds, cancellationToken);
                        continue;
                    }
                    else
                    {
                        global::System.Diagnostics.Debug.WriteLine($"[SetText] All {maxAttempts} attempts failed");
                        global::System.Diagnostics.Debug.WriteLine($"[SetText] Last exception stack trace: {ex.StackTrace}");
                        State = ActionState.Failed;
                        return false;
                    }
                }
                finally
                {
                    // Cleanup automation (will be called on success or after last failed attempt)
                    if (attempt == maxAttempts || State == ActionState.Completed)
                    {
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

            // Should not reach here, but just in case
            State = ActionState.Failed;
            return false;
        }
    }
}
