using System.Runtime.Serialization;
using System.ComponentModel;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using FleetAutomate.Model.Flow;
using FleetAutomate.Helpers;
using NLog;

namespace FleetAutomate.Model.Actions.UIAutomation
{
    /// <summary>
    /// Action to click on a UI element.
    /// </summary>
    [DataContract]
    public class ClickElementAction : IRetryableAction, INotifyPropertyChanged
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
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
        /// Number of times to retry if the action fails (0 means no retry, just one attempt)
        /// </summary>
        [DataMember]
        public int RetryTimes { get; set; } = 3;

        /// <summary>
        /// Delay in milliseconds between retry attempts
        /// </summary>
        [DataMember]
        public int RetryDelayMilliseconds { get; set; } = 500;

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
            Logger.Info($"[ClickElement] Starting - IdentifierType: {IdentifierType}, Identifier: {ElementIdentifier}, IsDoubleClick: {IsDoubleClick}, RetryTimes: {RetryTimes}");

            if (string.IsNullOrWhiteSpace(ElementIdentifier))
            {
                Logger.Error("[ClickElement] ERROR: Element identifier is empty");
                State = ActionState.Failed;
                return false;
            }

            int maxAttempts = RetryTimes + 1; // RetryTimes = 3 means 4 total attempts (1 initial + 3 retries)
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Attempt {attempt}/{maxAttempts}");

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
                        global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Element not found on attempt {attempt}/{maxAttempts}");

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
                            global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Waiting {RetryDelayMilliseconds}ms before retry...");
                            await Task.Delay(RetryDelayMilliseconds, cancellationToken);
                            continue;
                        }
                        else
                        {
                            global::System.Diagnostics.Debug.WriteLine($"[ClickElement] ERROR: Element not found after {maxAttempts} attempts");
                            State = ActionState.Failed;
                            return false;
                        }
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

                    global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Click completed successfully on attempt {attempt}/{maxAttempts}");
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
                    lastException = ex;
                    global::System.Diagnostics.Debug.WriteLine($"[ClickElement] EXCEPTION on attempt {attempt}/{maxAttempts}: {ex.GetType().Name} - {ex.Message}");

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
                        global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Waiting {RetryDelayMilliseconds}ms before retry...");
                        await Task.Delay(RetryDelayMilliseconds, cancellationToken);
                        continue;
                    }
                    else
                    {
                        global::System.Diagnostics.Debug.WriteLine($"[ClickElement] All {maxAttempts} attempts failed");
                        global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Last exception stack trace: {ex.StackTrace}");
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
                            global::System.Diagnostics.Debug.WriteLine("[ClickElement] Automation disposed");
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
