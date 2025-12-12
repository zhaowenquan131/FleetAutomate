using System.Runtime.Serialization;
using System.ComponentModel;
using Canvas.TestRunner.Model.Flow;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;

namespace Canvas.TestRunner.Model.Actions.UIAutomation
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
                var element = FindElement(desktop);
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

        /// <summary>
        /// Finds an element based on the identifier type
        /// </summary>
        private AutomationElement? FindElement(AutomationElement root)
        {
            try
            {
                return IdentifierType switch
                {
                    "XPath" => FindByXPath(root),
                    "AutomationId" => FindByAutomationId(root),
                    "Name" => FindByName(root),
                    "ClassName" => FindByClassName(root),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        private AutomationElement? FindByXPath(AutomationElement root)
        {
            try
            {
                global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Searching by XPath: {ElementIdentifier}");

                // Optimization: If searching from desktop and XPath starts with //Window,
                // find windows directly instead of using XPath from desktop (which can hang)
                if (ElementIdentifier.StartsWith("//Window["))
                {
                    global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Optimizing window search...");

                    // Extract window condition and remaining path
                    var parts = ElementIdentifier.Split(new[] { "]//" }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        var windowXPath = parts[0] + "]";  // e.g., //Window[@Name="未注册版本"]

                        global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Window XPath: {windowXPath}");

                        // Extract window search criteria (Name, AutomationId, etc.)
                        AutomationElement? targetWindow = null;

                        // Parse @Name="value"
                        var nameMatch = global::System.Text.RegularExpressions.Regex.Match(windowXPath, @"@Name\s*=\s*[""']([^""']+)[""']");
                        if (nameMatch.Success)
                        {
                            var windowName = nameMatch.Groups[1].Value;
                            global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Searching for window with Name: {windowName}");

                            // Find windows directly (fast)
                            var allWindows = root.FindAllChildren();
                            global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Found {allWindows?.Length ?? 0} top-level windows");

                            foreach (var window in allWindows ?? Array.Empty<AutomationElement>())
                            {
                                try
                                {
                                    if (window.Name == windowName)
                                    {
                                        global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Found matching window: {window.Name}");
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
                                global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Searching for window with AutomationId: {automationId}");

                                var allWindows = root.FindAllChildren();
                                foreach (var window in allWindows ?? Array.Empty<AutomationElement>())
                                {
                                    try
                                    {
                                        if (window.AutomationId == automationId)
                                        {
                                            global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Found matching window: {window.AutomationId}");
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

                                global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Searching for child element: {childPath}");

                                // Construct full XPath for this child element
                                var childXPath = "//" + childPath;
                                var childElements = currentElement.FindAllByXPath(childXPath);
                                currentElement = childElements?.FirstOrDefault();

                                if (currentElement == null)
                                {
                                    global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Failed to find child element: {childPath}");
                                    return null;
                                }

                                global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Found child element: {currentElement.Name ?? "(no name)"}");
                            }

                            return currentElement;
                        }
                        else
                        {
                            global::System.Diagnostics.Debug.WriteLine($"[ClickElement] No matching window found");
                            return null;
                        }
                    }
                }

                // Fallback to original behavior (for non-window searches)
                global::System.Diagnostics.Debug.WriteLine($"[ClickElement] Using standard XPath search");
                var allElements = root.FindAllByXPath(ElementIdentifier);
                global::System.Diagnostics.Debug.WriteLine($"[ClickElement] XPath search returned {allElements?.Length ?? 0} elements");
                return allElements?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[ClickElement] FindByXPath exception: {ex.Message}");
                return null;
            }
        }

        private AutomationElement? FindByAutomationId(AutomationElement root)
        {
            try
            {
                var condition = root.ConditionFactory.ByAutomationId(ElementIdentifier);
                return root.FindFirstDescendant(condition);
            }
            catch
            {
                return null;
            }
        }

        private AutomationElement? FindByName(AutomationElement root)
        {
            try
            {
                var condition = root.ConditionFactory.ByName(ElementIdentifier);
                return root.FindFirstDescendant(condition);
            }
            catch
            {
                return null;
            }
        }

        private AutomationElement? FindByClassName(AutomationElement root)
        {
            try
            {
                var condition = root.ConditionFactory.ByClassName(ElementIdentifier);
                return root.FindFirstDescendant(condition);
            }
            catch
            {
                return null;
            }
        }
    }
}
