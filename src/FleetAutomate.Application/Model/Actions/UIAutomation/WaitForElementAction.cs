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
    public class WaitForElementAction : IAction, IUIElementAction, INotifyPropertyChanged
    {
        public string Name
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ElementIdentifier))
                {
                    return "Wait for Element";
                }

                // Extract element description from identifier
                string elementDesc = Helpers.ElementDescriptionHelper.ExtractElementDescription(ElementIdentifier, IdentifierType);

                // Format: "Wait for [ControlType] '[identifier]'"
                return $"Wait for {elementDesc}";
            }
        }

        [DataMember]
        private string _description = "Wait until a UI element exists";
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
        /// Maximum time in milliseconds to wait for the element
        /// </summary>
        [DataMember]
        private int _timeoutMilliseconds = 30000;
        public int TimeoutMilliseconds
        {
            get => _timeoutMilliseconds;
            set
            {
                if (_timeoutMilliseconds != value)
                {
                    _timeoutMilliseconds = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimeoutMilliseconds)));
                }
            }
        }

        /// <summary>
        /// How often to check for the element (in milliseconds)
        /// </summary>
        [DataMember]
        private int _pollingIntervalMilliseconds = 100;
        public int PollingIntervalMilliseconds
        {
            get => _pollingIntervalMilliseconds;
            set
            {
                if (_pollingIntervalMilliseconds != value)
                {
                    _pollingIntervalMilliseconds = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PollingIntervalMilliseconds)));
                }
            }
        }

        /// <summary>
        /// The key in the GlobalElementDictionary to use as the search root.
        /// If null or empty, search from desktop.
        /// </summary>
        [DataMember]
        private string? _searchScope;
        public string? SearchScope
        {
            get => _searchScope;
            set
            {
                if (_searchScope != value)
                {
                    _searchScope = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchScope)));
                }
            }
        }

        /// <summary>
        /// Whether to add the found element to the GlobalElementDictionary.
        /// </summary>
        [DataMember]
        private bool _addToGlobalDictionary = false;
        public bool AddToGlobalDictionary
        {
            get => _addToGlobalDictionary;
            set
            {
                if (_addToGlobalDictionary != value)
                {
                    _addToGlobalDictionary = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddToGlobalDictionary)));
                }
            }
        }

        /// <summary>
        /// The key to use when adding to the GlobalElementDictionary.
        /// Defaults to ElementIdentifier if not specified.
        /// </summary>
        [DataMember]
        private string? _globalDictionaryKey;
        public string? GlobalDictionaryKey
        {
            get => _globalDictionaryKey;
            set
            {
                if (_globalDictionaryKey != value)
                {
                    _globalDictionaryKey = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GlobalDictionaryKey)));
                }
            }
        }

        /// <summary>
        /// Reference to the TestFlow's GlobalElementDictionary.
        /// Set at runtime before execution.
        /// </summary>
        [IgnoreDataMember]
        public GlobalElementDictionary? ElementDictionary { get; set; }

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
            // Yield to allow UI to update the action state immediately
            await Task.Yield();
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

                // Determine search root - use SearchScope from dictionary if specified
                AutomationElement searchRoot = desktop;
                if (!string.IsNullOrEmpty(SearchScope) && ElementDictionary != null)
                {
                    var scopeElement = ElementDictionary.GetElement(SearchScope);
                    if (scopeElement != null)
                    {
                        searchRoot = scopeElement;
                        global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Using search scope: {SearchScope}");
                    }
                    else
                    {
                        global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Warning: Search scope '{SearchScope}' not found in dictionary, using desktop");
                    }
                }

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
                    var element = UIAutomationHelper.FindElement(searchRoot, IdentifierType, ElementIdentifier, "WaitForElement");
                    if (element != null)
                    {
                        // Add element to global dictionary if requested
                        if (AddToGlobalDictionary && ElementDictionary != null)
                        {
                            var key = string.IsNullOrEmpty(GlobalDictionaryKey) ? ElementIdentifier : GlobalDictionaryKey;
                            ElementDictionary.SetElement(key, element);
                            global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Added element to global dictionary with key: {key}");
                        }

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
