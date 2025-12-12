using System.Runtime.Serialization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;

namespace Canvas.TestRunner.Model.Actions.Logic.Expression
{
    /// <summary>
    /// Expression that evaluates to true if a UI element exists, false otherwise.
    /// </summary>
    [DataContract]
    public class UIElementExistsExpression : ExpressionBase<bool>
    {
        /// <summary>
        /// The XPath or identifier for the UI element to check.
        /// </summary>
        [DataMember]
        public string ElementIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// The type of identifier: "XPath", "AutomationId", "Name", "ClassName"
        /// </summary>
        [DataMember]
        public string IdentifierType { get; set; } = "XPath";

        /// <summary>
        /// Timeout in milliseconds to wait for the element (default 1000ms for quick check).
        /// </summary>
        [DataMember]
        public int TimeoutMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Number of times to retry finding the element before returning false (default 1, meaning no retry).
        /// Each retry waits for TimeoutMilliseconds before attempting again.
        /// </summary>
        [DataMember]
        public int RetryTimes { get; set; } = 1;

        public UIElementExistsExpression()
        {
        }

        public UIElementExistsExpression(string elementIdentifier, string identifierType = "XPath", int timeoutMs = 1000, int retryTimes = 1)
        {
            ElementIdentifier = elementIdentifier;
            IdentifierType = identifierType;
            TimeoutMilliseconds = timeoutMs;
            RetryTimes = retryTimes;
        }

        public override void Evaluate()
        {
            UIA3Automation? automation = null;
            try
            {
                // Initialize automation
                automation = new UIA3Automation();
                var desktop = automation.GetDesktop();

                // Retry logic: attempt to find element RetryTimes times
                AutomationElement? element = null;
                int attempts = Math.Max(1, RetryTimes); // Ensure at least 1 attempt

                for (int i = 0; i < attempts; i++)
                {
                    // Try to find the element
                    element = FindElement(desktop);

                    if (element != null)
                    {
                        // Element found, break out of retry loop
                        break;
                    }

                    // If not found and not the last attempt, wait before retrying
                    if (i < attempts - 1)
                    {
                        global::System.Threading.Thread.Sleep(TimeoutMilliseconds);
                    }
                }

                // Set result based on whether element was found
                Result = element != null;
            }
            catch
            {
                // If any error occurs, consider element as not existing
                Result = false;
            }
            finally
            {
                // Cleanup
                try
                {
                    automation?.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }

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
                // Optimization: If searching from desktop and XPath starts with //Window,
                // find windows directly instead of using XPath from desktop
                if (ElementIdentifier.StartsWith("//Window["))
                {
                    var parts = ElementIdentifier.Split(new[] { "]//" }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        var windowXPath = parts[0] + "]";

                        AutomationElement? targetWindow = null;

                        // Parse @Name="value" from window XPath
                        var nameMatch = global::System.Text.RegularExpressions.Regex.Match(windowXPath, @"@Name\s*=\s*[""']([^""']+)[""']");
                        if (nameMatch.Success)
                        {
                            var windowName = nameMatch.Groups[1].Value;
                            var allWindows = root.FindAllChildren();

                            foreach (var window in allWindows ?? Array.Empty<AutomationElement>())
                            {
                                try
                                {
                                    if (window.Name == windowName)
                                    {
                                        targetWindow = window;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            // Parse @AutomationId="value" from window XPath
                            var idMatch = global::System.Text.RegularExpressions.Regex.Match(windowXPath, @"@AutomationId\s*=\s*[""']([^""']+)[""']");
                            if (idMatch.Success)
                            {
                                var automationId = idMatch.Groups[1].Value;
                                var allWindows = root.FindAllChildren();

                                foreach (var window in allWindows ?? Array.Empty<AutomationElement>())
                                {
                                    try
                                    {
                                        if (window.AutomationId == automationId)
                                        {
                                            targetWindow = window;
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }

                        if (targetWindow != null)
                        {
                            // Handle nested paths: parts[1], parts[2], parts[3], etc.
                            // Example: //Window[@Name="x"]//TitleBar[@Name="y"]//Button[@Name="z"]
                            // parts[0] = "//Window[@Name="x"
                            // parts[1] = "TitleBar[@Name="y"
                            // parts[2] = "Button[@Name="z"]"

                            AutomationElement? currentElement = targetWindow;

                            for (int i = 1; i < parts.Length; i++)
                            {
                                // Add back the closing bracket that was lost in split
                                var childPath = parts[i];
                                if (!childPath.EndsWith("]"))
                                {
                                    childPath += "]";
                                }

                                currentElement = FindChildElementByPath(currentElement, childPath);

                                if (currentElement == null)
                                {
                                    global::System.Diagnostics.Debug.WriteLine($"[UIElementExists] Failed to find child element: {childPath}");
                                    return null;
                                }
                            }

                            return currentElement;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }

                // Fallback to standard XPath search
                var allElements = root.FindAllByXPath(ElementIdentifier);
                return allElements?.FirstOrDefault();
            }
            catch
            {
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

        /// <summary>
        /// Finds a child element by parsing a simple XPath-like path.
        /// Example: "TitleBar[@Name='value']" or "Button[@AutomationId='id']"
        /// </summary>
        private AutomationElement? FindChildElementByPath(AutomationElement parent, string elementPath)
        {
            try
            {
                // Parse element type (e.g., "TitleBar", "Button")
                var typeMatch = global::System.Text.RegularExpressions.Regex.Match(elementPath, @"^(\w+)");
                if (!typeMatch.Success)
                    return null;

                var elementType = typeMatch.Groups[1].Value;

                // Parse attribute conditions
                var nameMatch = global::System.Text.RegularExpressions.Regex.Match(elementPath, @"@Name\s*=\s*[""']([^""']+)[""']");
                var idMatch = global::System.Text.RegularExpressions.Regex.Match(elementPath, @"@AutomationId\s*=\s*[""']([^""']+)[""']");
                var classMatch = global::System.Text.RegularExpressions.Regex.Match(elementPath, @"@ClassName\s*=\s*[""']([^""']+)[""']");

                // Build condition based on available attributes
                if (nameMatch.Success)
                {
                    var name = nameMatch.Groups[1].Value;

                    // Try to combine ControlType with Name condition for more precise matching
                    try
                    {
                        var controlType = GetControlTypeFromName(elementType);
                        if (controlType != null)
                        {
                            // Special handling for TitleBar: often has empty Name, so search by ControlType only
                            if (elementType == "TitleBar")
                            {
                                var typeCondition = parent.ConditionFactory.ByControlType(controlType.Value);
                                var result = parent.FindFirstDescendant(typeCondition);
                                return result;
                            }

                            // Use AND condition: ControlType AND Name
                            var nameCondition = parent.ConditionFactory.ByName(name);
                            var typeCondition2 = parent.ConditionFactory.ByControlType(controlType.Value);
                            var combinedCondition = new AndCondition(typeCondition2, nameCondition);
                            var result2 = parent.FindFirstDescendant(combinedCondition);

                            if (result2 == null)
                            {
                                // Fallback: try name-only search
                                result2 = parent.FindFirstDescendant(nameCondition);
                            }
                            return result2;
                        }
                    }
                    catch (Exception ex)
                    {
                        global::System.Diagnostics.Debug.WriteLine($"[UIElementExists] Exception: {ex.Message}");
                    }

                    // Fallback to just name search if control type matching fails
                    var condition = parent.ConditionFactory.ByName(name);
                    return parent.FindFirstDescendant(condition);
                }
                else if (idMatch.Success)
                {
                    var automationId = idMatch.Groups[1].Value;

                    // Try to combine ControlType with AutomationId condition
                    try
                    {
                        var controlType = GetControlTypeFromName(elementType);
                        if (controlType != null)
                        {
                            var idCondition = parent.ConditionFactory.ByAutomationId(automationId);
                            var typeCondition = parent.ConditionFactory.ByControlType(controlType.Value);
                            var combinedCondition = new AndCondition(typeCondition, idCondition);
                            return parent.FindFirstDescendant(combinedCondition);
                        }
                    }
                    catch { }

                    // Fallback to just automation id search
                    var condition = parent.ConditionFactory.ByAutomationId(automationId);
                    return parent.FindFirstDescendant(condition);
                }
                else if (classMatch.Success)
                {
                    var className = classMatch.Groups[1].Value;
                    var condition = parent.ConditionFactory.ByClassName(className);
                    return parent.FindFirstDescendant(condition);
                }
                else
                {
                    // No attribute specified, search by control type only (less reliable)
                    // Try to find first child matching the element type name
                    var allChildren = parent.FindAllDescendants();
                    foreach (var child in allChildren ?? Array.Empty<AutomationElement>())
                    {
                        try
                        {
                            // Compare ControlType name (e.g., "TitleBar", "Button")
                            if (child.ControlType.ToString().Contains(elementType))
                            {
                                return child;
                            }
                        }
                        catch { }
                    }
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Maps element type name (e.g., "TitleBar", "Button") to FlaUI ControlType.
        /// </summary>
        private FlaUI.Core.Definitions.ControlType? GetControlTypeFromName(string elementTypeName)
        {
            return elementTypeName switch
            {
                "Button" => FlaUI.Core.Definitions.ControlType.Button,
                "Calendar" => FlaUI.Core.Definitions.ControlType.Calendar,
                "CheckBox" => FlaUI.Core.Definitions.ControlType.CheckBox,
                "ComboBox" => FlaUI.Core.Definitions.ControlType.ComboBox,
                "Custom" => FlaUI.Core.Definitions.ControlType.Custom,
                "DataGrid" => FlaUI.Core.Definitions.ControlType.DataGrid,
                "DataItem" => FlaUI.Core.Definitions.ControlType.DataItem,
                "Document" => FlaUI.Core.Definitions.ControlType.Document,
                "Edit" => FlaUI.Core.Definitions.ControlType.Edit,
                "Group" => FlaUI.Core.Definitions.ControlType.Group,
                "Header" => FlaUI.Core.Definitions.ControlType.Header,
                "HeaderItem" => FlaUI.Core.Definitions.ControlType.HeaderItem,
                "Hyperlink" => FlaUI.Core.Definitions.ControlType.Hyperlink,
                "Image" => FlaUI.Core.Definitions.ControlType.Image,
                "List" => FlaUI.Core.Definitions.ControlType.List,
                "ListItem" => FlaUI.Core.Definitions.ControlType.ListItem,
                "Menu" => FlaUI.Core.Definitions.ControlType.Menu,
                "MenuBar" => FlaUI.Core.Definitions.ControlType.MenuBar,
                "MenuItem" => FlaUI.Core.Definitions.ControlType.MenuItem,
                "Pane" => FlaUI.Core.Definitions.ControlType.Pane,
                "ProgressBar" => FlaUI.Core.Definitions.ControlType.ProgressBar,
                "RadioButton" => FlaUI.Core.Definitions.ControlType.RadioButton,
                "ScrollBar" => FlaUI.Core.Definitions.ControlType.ScrollBar,
                "Separator" => FlaUI.Core.Definitions.ControlType.Separator,
                "Slider" => FlaUI.Core.Definitions.ControlType.Slider,
                "Spinner" => FlaUI.Core.Definitions.ControlType.Spinner,
                "SplitButton" => FlaUI.Core.Definitions.ControlType.SplitButton,
                "StatusBar" => FlaUI.Core.Definitions.ControlType.StatusBar,
                "Tab" => FlaUI.Core.Definitions.ControlType.Tab,
                "TabItem" => FlaUI.Core.Definitions.ControlType.TabItem,
                "Table" => FlaUI.Core.Definitions.ControlType.Table,
                "Text" => FlaUI.Core.Definitions.ControlType.Text,
                "Thumb" => FlaUI.Core.Definitions.ControlType.Thumb,
                "TitleBar" => FlaUI.Core.Definitions.ControlType.TitleBar,
                "ToolBar" => FlaUI.Core.Definitions.ControlType.ToolBar,
                "ToolTip" => FlaUI.Core.Definitions.ControlType.ToolTip,
                "Tree" => FlaUI.Core.Definitions.ControlType.Tree,
                "TreeItem" => FlaUI.Core.Definitions.ControlType.TreeItem,
                "Window" => FlaUI.Core.Definitions.ControlType.Window,
                _ => null
            };
        }

        public override string ToString()
        {
            return $"UIElement '{ElementIdentifier}' exists";
        }
    }
}
