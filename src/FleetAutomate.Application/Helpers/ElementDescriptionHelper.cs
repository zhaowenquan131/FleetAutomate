using System.Text.RegularExpressions;

namespace FleetAutomate.Helpers
{
    /// <summary>
    /// Helper class for extracting user-friendly descriptions from UI element identifiers.
    /// </summary>
    public static class ElementDescriptionHelper
    {
        /// <summary>
        /// Extracts a user-friendly description from an element identifier.
        /// </summary>
        /// <param name="identifier">The element identifier (XPath, Name, AutomationId, etc.)</param>
        /// <param name="identifierType">The type of identifier (XPath, Name, AutomationId, ClassName)</param>
        /// <returns>A friendly description of the element</returns>
        public static string ExtractElementDescription(string identifier, string identifierType)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return "Element";
            }

            // For XPath, try to extract control type and name/id
            if (identifierType == "XPath" && identifier.Contains("@"))
            {
                try
                {
                    // Parse patterns like //Button[@Name='OK'] or //Window[@Name='Test']//Button[@AutomationId='btnOk']
                    var lastElementMatch = Regex.Match(identifier, @"(\w+)\[@(\w+)=['""]([^'""]+)['""]");
                    if (lastElementMatch.Success)
                    {
                        string controlType = lastElementMatch.Groups[1].Value;
                        string attrName = lastElementMatch.Groups[2].Value;
                        string attrValue = lastElementMatch.Groups[3].Value;

                        // Format: ControlType 'value'
                        return $"{controlType} '{attrValue}'";
                    }

                    // Try simple pattern like //Button
                    var simpleMatch = Regex.Match(identifier, @"//(\w+)$");
                    if (simpleMatch.Success)
                    {
                        return simpleMatch.Groups[1].Value;
                    }
                }
                catch { }
            }

            // For AutomationId, Name, ClassName - show the value
            if (identifierType == "Name" || identifierType == "AutomationId")
            {
                return $"Element '{identifier}'";
            }

            if (identifierType == "ClassName")
            {
                return $"Element of class '{identifier}'";
            }

            // Fallback: show truncated identifier
            string truncated = identifier.Length > 30 ? identifier.Substring(0, 27) + "..." : identifier;
            return $"Element '{truncated}'";
        }
    }
}
