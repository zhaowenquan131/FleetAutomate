using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;

using System;
using System.Linq;
using System.Text.RegularExpressions;

using FlaUIElement = FlaUI.Core.AutomationElements.AutomationElement;

namespace FleetAutomate.Helpers
{
    public sealed class XPathSegment
    {
        public ControlType? ControlType { get; init; }
        public string? Name { get; init; }
        public string? AutomationId { get; init; }

        public bool IsWildcard { get; init; }

        public bool Match(FlaUIElement e)
        {
            if (IsWildcard)
                return true;

            // 剪枝优先级：AutomationId > Name > ControlType
            if (!string.IsNullOrEmpty(AutomationId) &&
                e.Properties.AutomationId.ValueOrDefault != AutomationId)
                return false;

            if (!string.IsNullOrEmpty(Name) &&
                e.Properties.Name.ValueOrDefault != Name)
                return false;

            if (ControlType.HasValue &&
                e.Properties.ControlType.ValueOrDefault != ControlType.Value)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Helper class for common UI Automation operations using FlaUI.
    /// Provides centralized methods for finding elements and windows.
    /// </summary>
    public static partial class UIAutomationHelper
    {
        public static List<XPathSegment> ParseXPath(string xPath)
        {
            var segments = new List<XPathSegment>();

            var parts = xPath.Split(["//"], StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                // ControlType
                var typeEnd = part.IndexOf('[');
                var typeName = typeEnd > 0 ? part[..typeEnd] : part;

                var nameMatch = NameMatchRegex().Match(part);
                var idMatch = AutomationIdMatchRegex().Match(part);
                var segment = new XPathSegment
                {
                    ControlType = Enum.TryParse<ControlType>(typeName, out var ct) ? ct : null,
                    Name = nameMatch.Success ? nameMatch.Groups[1].Value : null,
                    AutomationId = idMatch.Success ? idMatch.Groups[1].Value : null
                };
                segments.Add(segment);
            }
            return segments;
        }

        public static bool FindBySegments(
            FlaUIElement current,
            IReadOnlyList<XPathSegment> segments,
            int level,
            int maxDepth,
            out FlaUIElement? result)
        {
            result = null;

            if (level == segments.Count)
            {
                result = current;
                return true;
            }

            if (level >= maxDepth)
                return false;

            var segment = segments[level];

            // ⚠ 关键：只找直接子节点（避免 //）
            var children = current.FindAllChildren();

            foreach (var child in children)
            {
                if (!segment.Match(child))
                    continue;

                // DFS
                if (FindBySegments(child, segments, level + 1, maxDepth, out result))
                    return true;
            }

            // 本层所有分支失败 → 回溯
            return false;
        }

        /// <summary>
        /// Finds a window by name or AutomationId.
        /// </summary>
        /// <param name="desktop">The desktop element</param>
        /// <param name="identifierType">"Name" or "AutomationId"</param>
        /// <param name="windowIdentifier">The window identifier value</param>
        /// <param name="debugPrefix">Optional prefix for debug messages</param>
        /// <returns>The found window, or null if not found</returns>
        public static FlaUIElement? FindWindow(
            FlaUIElement desktop,
            string identifierType,
            string windowIdentifier,
            string debugPrefix = "")
        {
            try
            {
                var allWindows = desktop.FindAllChildren();
                LogDebug(debugPrefix, $"Found {allWindows?.Length ?? 0} top-level windows");

                foreach (var window in allWindows ?? [])
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
        /// Finds an element based on the identifier type.
        /// </summary>
        /// <param name="root">The root element to search from (usually desktop)</param>
        /// <param name="identifierType">Type of identifier: "XPath", "AutomationId", "Name", or "ClassName"</param>
        /// <param name="elementIdentifier">The identifier value to search for</param>
        /// <param name="debugPrefix">Optional prefix for debug messages</param>
        /// <returns>The found element, or null if not found</returns>
        public static FlaUIElement? FindElement(
            FlaUIElement root,
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

        public static FlaUIElement? FindByXPath(
            FlaUIElement desktop,
            string xPath,
            string debugPrefix = "")
        {
            try
            {
                FlaUIElement start = desktop;
                string remaining = xPath;

                // 1️⃣ Window 快速定位
                if (xPath.StartsWith("//Window["))
                {
                    var split = xPath.Split(new[] { "]//" }, 2, StringSplitOptions.None);
                    if (split.Length != 2)
                        return null;

                    var windowXPath = split[0] + "]";
                    remaining = split[1];

                    var nameMatch = NameMatchRegex().Match(windowXPath);
                    var idMatch = AutomationIdMatchRegex().Match(windowXPath);

                    if (idMatch.Success)
                        start = FindWindow(desktop, "AutomationId", idMatch.Groups[1].Value)!;
                    else if (nameMatch.Success)
                        start = FindWindow(desktop, "Name", nameMatch.Groups[1].Value)!;

                    if (start == null)
                        return null;
                }

                // 2️⃣ 解析 XPath
                var segments = ParseXPath(remaining);
                if (segments.Count == 0)
                    return start;

                // 3️⃣ DFS + 回溯
                return FindBySegments(
                    start,
                    segments,
                    level: 0,
                    maxDepth: 12,
                    out var result)
                    ? result
                    : null;
            }
            catch
            {
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
        public static FlaUIElement? FindByAutomationId(
            FlaUIElement root,
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
        public static FlaUIElement? FindByName(
            FlaUIElement root,
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
        public static FlaUIElement? FindByClassName(
            FlaUIElement root,
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

        [GeneratedRegex(@"@Name\s*=\s*[""']([^""']+)[""']")]
        private static partial Regex NameMatchRegex();

        [GeneratedRegex(@"@AutomationId\s*=\s*[""']([^""']+)[""']")]
        private static partial Regex AutomationIdMatchRegex();
    }
}
