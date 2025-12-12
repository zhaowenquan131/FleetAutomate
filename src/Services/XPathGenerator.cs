using System;
using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.AutomationElements;

namespace Canvas.TestRunner.Services
{
    /// <summary>
    /// Generates human-friendly XPaths for UI elements.
    /// </summary>
    public class XPathGenerator
    {
        /// <summary>
        /// Generates a friendly XPath for the given element.
        /// Prefers Name and AutomationId, avoids duplicates.
        /// </summary>
        public static string GenerateXPath(AutomationElement element)
        {
            try
            {
                var pathParts = new List<string>();
                var current = element;

                // Build path from element up to window
                while (current != null)
                {
                    var part = GenerateElementPredicate(current);
                    if (!string.IsNullOrEmpty(part))
                    {
                        pathParts.Insert(0, part);
                    }

                    current = current.Parent;

                    // Stop at window level
                    if (current != null && IsWindow(current))
                    {
                        var windowPart = GenerateWindowPredicate(current);
                        if (!string.IsNullOrEmpty(windowPart))
                        {
                            pathParts.Insert(0, windowPart);
                        }
                        break;
                    }
                }

                // Build final XPath
                if (pathParts.Count == 0)
                    return "//Element";

                return "//" + string.Join("//", pathParts);
            }
            catch
            {
                return "//Element";
            }
        }

        private static string GenerateWindowPredicate(AutomationElement window)
        {
            try
            {
                // For windows, prefer Name
                string? name = null;
                try { name = window.Properties.Name.ValueOrDefault; } catch { }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    return $"Window[@Name=\"{EscapeXPath(name)}\"]";
                }

                // Fall back to AutomationId
                string? automationId = null;
                try { automationId = window.Properties.AutomationId.ValueOrDefault; } catch { }

                if (!string.IsNullOrWhiteSpace(automationId))
                {
                    return $"Window[@AutomationId=\"{EscapeXPath(automationId)}\"]";
                }

                return "Window";
            }
            catch
            {
                return "Window";
            }
        }

        private static string GenerateElementPredicate(AutomationElement element)
        {
            try
            {
                string controlType = "Element";
                try
                {
                    var ct = element.Properties.ControlType.ValueOrDefault;
                    controlType = ct.ToString();
                }
                catch { }

                // Special handling for TitleBar: skip Name attribute
                // TitleBar elements often report the window's title at capture time,
                // but have empty Name property when searched later
                bool isTitleBar = controlType.Equals("TitleBar", StringComparison.OrdinalIgnoreCase);

                // Try Name first (most human-friendly) - but skip for TitleBar
                string? name = null;
                if (!isTitleBar)
                {
                    try { name = element.Properties.Name.ValueOrDefault; } catch { }

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var escapedName = EscapeXPath(name);
                        return $"{controlType}[@Name=\"{escapedName}\"]";
                    }
                }

                // Try AutomationId (stable identifier)
                string? automationId = null;
                try { automationId = element.Properties.AutomationId.ValueOrDefault; } catch { }

                if (!string.IsNullOrWhiteSpace(automationId))
                {
                    var escapedId = EscapeXPath(automationId);
                    return $"{controlType}[@AutomationId=\"{escapedId}\"]";
                }

                // Try ClassName (descriptive but less unique)
                string? className = null;
                try { className = element.Properties.ClassName.ValueOrDefault; } catch { }

                if (!string.IsNullOrWhiteSpace(className))
                {
                    var escapedClass = EscapeXPath(className);
                    return $"{controlType}[@ClassName=\"{escapedClass}\"]";
                }

                // Last resort: just the control type
                return controlType;
            }
            catch
            {
                return "Element";
            }
        }

        private static bool IsWindow(AutomationElement element)
        {
            try
            {
                string controlType = "";
                try
                {
                    var ct = element.Properties.ControlType.ValueOrDefault;
                    controlType = ct.ToString();
                }
                catch { }
                return controlType.Equals("Window", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeXPath(string value)
        {
            // Handle quotes in XPath strings
            if (value.Contains("\""))
            {
                // If string contains quotes, use single quotes or escape
                if (!value.Contains("'"))
                {
                    return value;  // Already safe with double quotes
                }
                else
                {
                    // Replace double quotes with their entity
                    return value.Replace("\"", "&quot;");
                }
            }

            return value;
        }

        /// <summary>
        /// Gets the bounding rectangle of an element.
        /// </summary>
        public static System.Windows.Rect GetElementBounds(AutomationElement element)
        {
            try
            {
                var bounds = element.Properties.BoundingRectangle.ValueOrDefault;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    return new System.Windows.Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                }
            }
            catch { }
            return System.Windows.Rect.Empty;
        }

        /// <summary>
        /// Gets friendly information about the element for display.
        /// </summary>
        public static (string type, string name, string automationId, string className) GetElementInfo(AutomationElement element)
        {
            try
            {
                string type = "Unknown";
                string name = "(no name)";
                string automationId = "(no id)";
                string className = "(no class)";

                try
                {
                    var ct = element.Properties.ControlType.ValueOrDefault;
                    type = ct.ToString();
                }
                catch { }

                try { name = element.Properties.Name.ValueOrDefault ?? "(no name)"; }
                catch { }

                try { automationId = element.Properties.AutomationId.ValueOrDefault ?? "(no id)"; }
                catch { }

                try { className = element.Properties.ClassName.ValueOrDefault ?? "(no class)"; }
                catch { }

                return (type, name, automationId, className);
            }
            catch
            {
                return ("Unknown", "(error)", "(error)", "(error)");
            }
        }
    }
}
