using System.Runtime.Serialization;
using System.ComponentModel;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using FleetAutomate.Model.Flow;

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
                    var element = FindElement(desktop);
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

        /// <summary>
        /// Finds an element based on the identifier type
        /// </summary>
        private AutomationElement? FindElement(AutomationElement root)
        {
            try
            {
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] FindElement called - Type: {IdentifierType}");

                var result = IdentifierType switch
                {
                    "XPath" => FindByXPath(root),
                    "AutomationId" => FindByAutomationId(root),
                    "Name" => FindByName(root),
                    "ClassName" => FindByClassName(root),
                    _ => null
                };

                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] FindElement result: {(result != null ? "Found" : "Not found")}");
                return result;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] FindElement exception: {ex.Message}");
                return null;
            }
        }

        private AutomationElement? FindByXPath(AutomationElement root)
        {
            try
            {
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Searching by XPath: {ElementIdentifier}");

                // Optimization: If searching from desktop and XPath starts with //Window,
                // find windows directly instead of using XPath from desktop (which can hang)
                if (ElementIdentifier.StartsWith("//Window["))
                {
                    global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Optimizing window search...");

                    // Extract window condition and remaining path
                    var parts = ElementIdentifier.Split(new[] { "]//" }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        var windowXPath = parts[0] + "]";  // e.g., //Window[@Name="未注册版本"]

                        global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Window XPath: {windowXPath}");

                        // Extract window search criteria (Name, AutomationId, etc.)
                        AutomationElement? targetWindow = null;

                        // Parse @Name="value"
                        var nameMatch = global::System.Text.RegularExpressions.Regex.Match(windowXPath, @"@Name\s*=\s*[""']([^""']+)[""']");
                        if (nameMatch.Success)
                        {
                            var windowName = nameMatch.Groups[1].Value;
                            global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Searching for window with Name: {windowName}");

                            // Find windows directly (fast)
                            var allWindows = root.FindAllChildren();
                            global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Found {allWindows?.Length ?? 0} top-level windows");

                            foreach (var window in allWindows ?? Array.Empty<AutomationElement>())
                            {
                                try
                                {
                                    if (window.Name == windowName)
                                    {
                                        global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Found matching window: {window.Name}");
                                        targetWindow = window;
                                        break;
                                    }
                                }
                                catch
                                {
                                    // Skip windows we can't access
                                }
                            }
                        }
                        else
                        {
                            // Parse @AutomationId="value"
                            var idMatch = global::System.Text.RegularExpressions.Regex.Match(windowXPath, @"@AutomationId\s*=\s*[""']([^""']+)[""']");
                            if (idMatch.Success)
                            {
                                var automationId = idMatch.Groups[1].Value;
                                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Searching for window with AutomationId: {automationId}");

                                var allWindows = root.FindAllChildren();
                                foreach (var window in allWindows ?? Array.Empty<AutomationElement>())
                                {
                                    try
                                    {
                                        if (window.AutomationId == automationId)
                                        {
                                            global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Found matching window: {window.AutomationId}");
                                            targetWindow = window;
                                            break;
                                        }
                                    }
                                    catch
                                    {
                                        // Skip windows we can't access
                                    }
                                }
                            }
                        }

                        if (targetWindow != null)
                        {
                            // Handle nested paths: parts[1], parts[2], parts[3], etc.
                            // Example: //Window[@Name="x"]//TitleBar[@Name="y"]//Button[@Name="z"]
                            AutomationElement? currentElement = targetWindow;

                            for (int i = 1; i < parts.Length; i++)
                            {
                                // Add back the closing bracket that was lost in split
                                var childPath = parts[i];
                                if (!childPath.EndsWith("]"))
                                {
                                    childPath += "]";
                                }

                                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Searching for child element: {childPath}");

                                // Construct full XPath for this child element
                                var childXPath = "//" + childPath;
                                var childElements = currentElement.FindAllByXPath(childXPath);
                                currentElement = childElements?.FirstOrDefault();

                                if (currentElement == null)
                                {
                                    global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Failed to find child element: {childPath}");
                                    return null;
                                }

                                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Found child element: {currentElement.Name ?? "(no name)"}");
                            }

                            return currentElement;
                        }
                        else
                        {
                            global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] No matching window found");
                            return null;
                        }
                    }
                }

                // Fallback to original behavior (for non-window searches)
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Using standard XPath search");
                var allElements = root.FindAllByXPath(ElementIdentifier);
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] XPath search returned {allElements?.Length ?? 0} elements");
                return allElements?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] FindByXPath exception: {ex.Message}");
                return null;
            }
        }

        private AutomationElement? FindByAutomationId(AutomationElement root)
        {
            try
            {
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Searching by AutomationId: {ElementIdentifier}");
                var condition = root.ConditionFactory.ByAutomationId(ElementIdentifier);
                var result = root.FindFirstDescendant(condition);
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] AutomationId search result: {(result != null ? "Found" : "Not found")}");
                return result;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] FindByAutomationId exception: {ex.Message}");
                return null;
            }
        }

        private AutomationElement? FindByName(AutomationElement root)
        {
            try
            {
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Searching by Name: {ElementIdentifier}");
                var condition = root.ConditionFactory.ByName(ElementIdentifier);
                var result = root.FindFirstDescendant(condition);
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Name search result: {(result != null ? "Found" : "Not found")}");
                return result;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] FindByName exception: {ex.Message}");
                return null;
            }
        }

        private AutomationElement? FindByClassName(AutomationElement root)
        {
            try
            {
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] Searching by ClassName: {ElementIdentifier}");
                var condition = root.ConditionFactory.ByClassName(ElementIdentifier);
                var result = root.FindFirstDescendant(condition);
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] ClassName search result: {(result != null ? "Found" : "Not found")}");
                return result;
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[WaitForElement] FindByClassName exception: {ex.Message}");
                return null;
            }
        }
    }
}
