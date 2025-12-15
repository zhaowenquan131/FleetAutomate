using FlaUI.Core.AutomationElements;
using System;
using System.Linq;

namespace FleetAutomate.Helpers
{
    /// <summary>
    /// Helper class for common UI Automation operations using FlaUI.
    /// Provides centralized methods for finding elements and windows.
    /// </summary>
    public static class UIAutomationHelper
    {
        /// <summary>
        /// Finds an element based on the identifier type.
        /// </summary>
        /// <param name="root">The root element to search from (usually desktop)</param>
        /// <param name="identifierType">Type of identifier: "XPath", "AutomationId", "Name", or "ClassName"</param>
        /// <param name="elementIdentifier">The identifier value to search for</param>
        /// <param name="debugPrefix">Optional prefix for debug messages</param>
        /// <returns>The found element, or null if not found</returns>
        public static AutomationElement? FindElement(
            AutomationElement root,
            string identifierType,
            string elementIdentifier,
            string debugPrefix = "")
        {
            try
            {
                LogDebug(debugPrefix, $"FindElement called - Type: {identifierType}");

                var result = identifierType switch
                {
                    "XPath" => FindByXPath(root, elementIdentifier, debugPrefix),
                    "AutomationId" => FindByAutomationId(root, elementIdentifier, debugPrefix),
                    "Name" => FindByName(root, elementIdentifier, debugPrefix),
                    "ClassName" => FindByClassName(root, elementIdentifier, debugPrefix),
                    _ => null
                };

                LogDebug(debugPrefix, $"FindElement result: {(result != null ? "Found" : "Not found")}");
                return result;
            }
            catch (Exception ex)
            {
                LogDebug(debugPrefix, $"FindElement exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds a window by name or AutomationId.
        /// </summary>
        /// <param name="desktop">The desktop element</param>
        /// <param name="identifierType">"Name" or "AutomationId"</param>
        /// <param name="windowIdentifier">The window identifier value</param>
        /// <param name="debugPrefix">Optional prefix for debug messages</param>
        /// <returns>The found window, or null if not found</returns>
        public static AutomationElement? FindWindow(
            AutomationElement desktop,
            string identifierType,
            string windowIdentifier,
            string debugPrefix = "")
        {
            try
            {
                var allWindows = desktop.FindAllChildren();
                LogDebug(debugPrefix, $"Found {allWindows?.Length ?? 0} top-level windows");

                foreach (var window in allWindows ?? Array.Empty<AutomationElement>())
                {
                    try
                    {
                        if (identifierType == "Name")
                        {
                            if (window.Name == windowIdentifier)
                            {
                                LogDebug(debugPrefix, $"Found matching window by Name: {window.Name}");
                                return window;
                            }
                        }
                        else if (identifierType == "AutomationId")
                        {
                            if (window.AutomationId == windowIdentifier)
                            {
                                LogDebug(debugPrefix, $"Found matching window by AutomationId: {window.AutomationId}");
                                return window;
                            }
                        }
                    }
                    catch
                    {
                        // Skip windows we can't access
                    }
                }

                LogDebug(debugPrefix, "No matching window found");
                return null;
            }
            catch (Exception ex)
            {
                LogDebug(debugPrefix, $"FindWindow exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds an element by XPath, with optimization for window searches.
        /// </summary>
        /// <param name="root">The root element to search from</param>
        /// <param name="xPath">The XPath expression</param>
        /// <param name="debugPrefix">Optional prefix for debug messages</param>
        /// <returns>The found element, or null if not found</returns>
        public static AutomationElement? FindByXPath(
            AutomationElement root,
            string xPath,
            string debugPrefix = "")
        {
            try
            {
                LogDebug(debugPrefix, $"Searching by XPath: {xPath}");

                // Optimization: If searching from desktop and XPath starts with //Window,
                // find windows directly instead of using XPath from desktop (which can hang)
                if (xPath.StartsWith("//Window["))
                {
                    LogDebug(debugPrefix, "Optimizing window search...");

                    // Extract window condition and remaining path
                    var parts = xPath.Split(["]//"], StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        var windowXPath = parts[0] + "]";  // e.g., //Window[@Name="Notepad"]
                        LogDebug(debugPrefix, $"Window XPath: {windowXPath}");

                        // Extract window search criteria (Name, AutomationId, etc.)
                        AutomationElement? targetWindow = null;

                        // Parse @Name="value"
                        var nameMatch = global::System.Text.RegularExpressions.Regex.Match(
                            windowXPath, @"@Name\s*=\s*[""']([^""']+)[""']");
                        if (nameMatch.Success)
                        {
                            var windowName = nameMatch.Groups[1].Value;
                            LogDebug(debugPrefix, $"Searching for window with Name: {windowName}");
                            targetWindow = FindWindow(root, "Name", windowName, debugPrefix);
                        }
                        else
                        {
                            // Parse @AutomationId="value"
                            var idMatch = global::System.Text.RegularExpressions.Regex.Match(
                                windowXPath, @"@AutomationId\s*=\s*[""']([^""']+)[""']");
                            if (idMatch.Success)
                            {
                                var automationId = idMatch.Groups[1].Value;
                                LogDebug(debugPrefix, $"Searching for window with AutomationId: {automationId}");
                                targetWindow = FindWindow(root, "AutomationId", automationId, debugPrefix);
                            }
                        }

                        if (targetWindow != null)
                        {
                            // Handle nested paths: parts[1], parts[2], parts[3], etc.
                            // Example: //Window[@Name="x"]//TitleBar[@Name="y"]//Button[@Name="z"]
                            AutomationElement? currentElement = targetWindow;

                            for (int i = 1; i < parts.Length; i++)
                            {
                                var childPath = parts[i];
                                if (!childPath.EndsWith("]"))
                                {
                                    childPath += "]";
                                }

                                LogDebug(debugPrefix, $"Searching for child element: {childPath}");

                                // Construct full XPath for this child element
                                var childXPath = "//" + childPath;
                                var children = currentElement.FindAllDescendants();
                                var childElements = currentElement.FindAllByXPath(childXPath);
                                currentElement = childElements?.FirstOrDefault();
                                 
                                if (currentElement == null)
                                {
                                    LogDebug(debugPrefix, $"Failed to find child element: {childPath}");
                                    return null;
                                }

                                LogDebug(debugPrefix, $"Found child element: {currentElement.Name ?? "(no name)"}");
                            }

                            return currentElement;
                        }
                        else
                        {
                            LogDebug(debugPrefix, "No matching window found");
                            return null;
                        }
                    }
                }

                // Fallback to original behavior (for non-window searches)
                LogDebug(debugPrefix, "Using standard XPath search");
                var allElements = root.FindAllByXPath(xPath);
                LogDebug(debugPrefix, $"XPath search returned {allElements?.Length ?? 0} elements");
                return allElements?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                LogDebug(debugPrefix, $"FindByXPath exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds an element by AutomationId.
        /// </summary>
        /// <param name="root">The root element to search from</param>
        /// <param name="automationId">The AutomationId to search for</param>
        /// <param name="debugPrefix">Optional prefix for debug messages</param>
        /// <returns>The found element, or null if not found</returns>
        public static AutomationElement? FindByAutomationId(
            AutomationElement root,
            string automationId,
            string debugPrefix = "")
        {
            try
            {
                LogDebug(debugPrefix, $"Searching by AutomationId: {automationId}");
                var condition = root.ConditionFactory.ByAutomationId(automationId);
                var result = root.FindFirstDescendant(condition);
                LogDebug(debugPrefix, $"AutomationId search result: {(result != null ? "Found" : "Not found")}");
                return result;
            }
            catch (Exception ex)
            {
                LogDebug(debugPrefix, $"FindByAutomationId exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds an element by Name.
        /// </summary>
        /// <param name="root">The root element to search from</param>
        /// <param name="name">The name to search for</param>
        /// <param name="debugPrefix">Optional prefix for debug messages</param>
        /// <returns>The found element, or null if not found</returns>
        public static AutomationElement? FindByName(
            AutomationElement root,
            string name,
            string debugPrefix = "")
        {
            try
            {
                LogDebug(debugPrefix, $"Searching by Name: {name}");
                var condition = root.ConditionFactory.ByName(name);
                var result = root.FindFirstDescendant(condition);
                LogDebug(debugPrefix, $"Name search result: {(result != null ? "Found" : "Not found")}");
                return result;
            }
            catch (Exception ex)
            {
                LogDebug(debugPrefix, $"FindByName exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds an element by ClassName.
        /// </summary>
        /// <param name="root">The root element to search from</param>
        /// <param name="className">The class name to search for</param>
        /// <param name="debugPrefix">Optional prefix for debug messages</param>
        /// <returns>The found element, or null if not found</returns>
        public static AutomationElement? FindByClassName(
            AutomationElement root,
            string className,
            string debugPrefix = "")
        {
            try
            {
                LogDebug(debugPrefix, $"Searching by ClassName: {className}");
                var condition = root.ConditionFactory.ByClassName(className);
                var result = root.FindFirstDescendant(condition);
                LogDebug(debugPrefix, $"ClassName search result: {(result != null ? "Found" : "Not found")}");
                return result;
            }
            catch (Exception ex)
            {
                LogDebug(debugPrefix, $"FindByClassName exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Logs a debug message with optional prefix.
        /// </summary>
        private static void LogDebug(string prefix, string message)
        {
            if (!string.IsNullOrEmpty(prefix))
            {
                global::System.Diagnostics.Debug.WriteLine($"[{prefix}] {message}");
            }
            else
            {
                global::System.Diagnostics.Debug.WriteLine(message);
            }
        }
    }
}
