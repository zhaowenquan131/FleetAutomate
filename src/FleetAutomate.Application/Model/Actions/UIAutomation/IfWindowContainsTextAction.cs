using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FleetAutomate.Model.Flow;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace FleetAutomate.Model.Actions.UIAutomation
{
    /// <summary>
    /// Action to check if a window contains specific text.
    /// Returns true if text is found, false otherwise.
    /// </summary>
    [DataContract]
    public class IfWindowContainsTextAction : IAction<bool>, INotifyPropertyChanged
    {
        public string Name => "If Window Contains Text";

        [DataMember]
        public string Description { get; set; } = "Check if window contains text";

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
        /// The window identifier (window name/title)
        /// </summary>
        [DataMember]
        public string WindowIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// The type of window identifier: "Name" or "AutomationId"
        /// </summary>
        [DataMember]
        public string IdentifierType { get; set; } = "Name";

        /// <summary>
        /// The text to search for in the window
        /// </summary>
        [DataMember]
        public string SearchText { get; set; } = string.Empty;

        /// <summary>
        /// Whether the search should be case sensitive
        /// </summary>
        [DataMember]
        public bool CaseSensitive { get; set; } = false;

        /// <summary>
        /// Whether to search in all descendant elements (deep search)
        /// </summary>
        [DataMember]
        public bool DeepSearch { get; set; } = true;

        /// <summary>
        /// The result of the check (true if text found, false otherwise)
        /// </summary>
        [DataMember]
        public bool Result { get; set; } = false;

        /// <summary>
        /// The automation instance (not serialized)
        /// </summary>
        [IgnoreDataMember]
        private AutomationBase? _automation;

        public void Cancel()
        {
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
            Result = false;

            global::System.Diagnostics.Debug.WriteLine($"[IfWindowContainsText] Starting - Window: {WindowIdentifier}, SearchText: '{SearchText}', CaseSensitive: {CaseSensitive}");

            try
            {
                if (string.IsNullOrWhiteSpace(WindowIdentifier))
                {
                    global::System.Diagnostics.Debug.WriteLine("[IfWindowContainsText] ERROR: Window identifier is empty");
                    throw new InvalidOperationException("Window identifier cannot be empty");
                }

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    global::System.Diagnostics.Debug.WriteLine("[IfWindowContainsText] ERROR: Search text is empty");
                    throw new InvalidOperationException("Search text cannot be empty");
                }

                // Initialize automation (UIA3)
                global::System.Diagnostics.Debug.WriteLine("[IfWindowContainsText] Initializing UIA3 automation...");
                _automation = new UIA3Automation();
                var desktop = _automation.GetDesktop();

                // Find the window
                global::System.Diagnostics.Debug.WriteLine("[IfWindowContainsText] Searching for window...");
                var window = FindWindow(desktop);
                if (window == null)
                {
                    global::System.Diagnostics.Debug.WriteLine("[IfWindowContainsText] Window not found");
                    State = ActionState.Completed;
                    Result = false;
                    return true; // Action completed successfully, but text not found
                }

                global::System.Diagnostics.Debug.WriteLine($"[IfWindowContainsText] Window found: {window.Name}");

                // Search for text in window
                bool textFound = SearchForTextInElement(window);

                global::System.Diagnostics.Debug.WriteLine($"[IfWindowContainsText] Text found: {textFound}");

                Result = textFound;
                State = ActionState.Completed;
                return true;
            }
            catch (OperationCanceledException)
            {
                global::System.Diagnostics.Debug.WriteLine("[IfWindowContainsText] Operation cancelled");
                State = ActionState.Failed;
                return false;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[IfWindowContainsText] EXCEPTION: {ex.GetType().Name} - {ex.Message}");
                State = ActionState.Failed;
                return false;
            }
            finally
            {
                // Cleanup automation
                try
                {
                    _automation?.Dispose();
                    global::System.Diagnostics.Debug.WriteLine("[IfWindowContainsText] Automation disposed");
                }
                catch
                {
                    // Ignore
                }
            }
        }

        /// <summary>
        /// Finds the window based on identifier type
        /// </summary>
        private AutomationElement? FindWindow(AutomationElement desktop)
        {
            try
            {
                var allWindows = desktop.FindAllChildren();
                global::System.Diagnostics.Debug.WriteLine($"[IfWindowContainsText] Found {allWindows?.Length ?? 0} top-level windows");

                foreach (var window in allWindows ?? Array.Empty<AutomationElement>())
                {
                    try
                    {
                        if (IdentifierType == "Name")
                        {
                            if (window.Name == WindowIdentifier)
                            {
                                return window;
                            }
                        }
                        else if (IdentifierType == "AutomationId")
                        {
                            if (window.AutomationId == WindowIdentifier)
                            {
                                return window;
                            }
                        }
                    }
                    catch
                    {
                        // Skip windows we can't access
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[IfWindowContainsText] FindWindow exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Searches for text in the element and optionally its descendants
        /// </summary>
        private bool SearchForTextInElement(AutomationElement element)
        {
            try
            {
                // Check the element's name
                if (CheckTextMatch(element.Name))
                {
                    return true;
                }

                // Check the element's text (for text controls)
                try
                {
                    var textPattern = element.Patterns.Text.PatternOrDefault;
                    if (textPattern != null)
                    {
                        var text = textPattern.DocumentRange.GetText(-1);
                        if (CheckTextMatch(text))
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    // Text pattern not supported, continue
                }

                // Check Value pattern (for input controls)
                try
                {
                    var valuePattern = element.Patterns.Value.PatternOrDefault;
                    if (valuePattern != null)
                    {
                        if (CheckTextMatch(valuePattern.Value))
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    // Value pattern not supported, continue
                }

                // If deep search is enabled, search in all descendants
                if (DeepSearch)
                {
                    var descendants = element.FindAllDescendants();
                    foreach (var descendant in descendants ?? Array.Empty<AutomationElement>())
                    {
                        try
                        {
                            // Check descendant's name
                            if (CheckTextMatch(descendant.Name))
                            {
                                return true;
                            }

                            // Check descendant's text pattern
                            try
                            {
                                var textPattern = descendant.Patterns.Text.PatternOrDefault;
                                if (textPattern != null)
                                {
                                    var text = textPattern.DocumentRange.GetText(-1);
                                    if (CheckTextMatch(text))
                                    {
                                        return true;
                                    }
                                }
                            }
                            catch
                            {
                                // Continue to next element
                            }

                            // Check descendant's value pattern
                            try
                            {
                                var valuePattern = descendant.Patterns.Value.PatternOrDefault;
                                if (valuePattern != null)
                                {
                                    if (CheckTextMatch(valuePattern.Value))
                                    {
                                        return true;
                                    }
                                }
                            }
                            catch
                            {
                                // Continue to next element
                            }
                        }
                        catch
                        {
                            // Skip elements we can't access
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[IfWindowContainsText] SearchForTextInElement exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the text matches the search criteria
        /// </summary>
        private bool CheckTextMatch(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (CaseSensitive)
            {
                return text.Contains(SearchText);
            }
            else
            {
                return text.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
