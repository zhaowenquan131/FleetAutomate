using FleetAutomate.Helpers;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace FleetAutomate.Expressions;

public sealed class DefaultExpressionUiQueryService : IExpressionUiQueryService
{
    public static DefaultExpressionUiQueryService Instance { get; } = new();

    private DefaultExpressionUiQueryService()
    {
    }

    public bool Exists(string elementPath)
    {
        return FindElement(elementPath) != null;
    }

    public bool Exists(string elementPath, string identifierType, int retryTimes)
    {
        var attempts = Math.Max(1, retryTimes);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (FindElement(elementPath, identifierType) != null)
            {
                return true;
            }

            if (attempt < attempts - 1)
            {
                Thread.Sleep(1000);
            }
        }

        return false;
    }

    public bool ContainsText(string elementPath, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var element = FindElement(elementPath);
        return element != null && ContainsTextInElementTree(element, text);
    }

    public string? GetProperty(string elementPath, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        var element = FindElement(elementPath);
        if (element == null)
        {
            return null;
        }

        return propertyName switch
        {
            "Name" => element.Name,
            "AutomationId" => element.AutomationId,
            "ClassName" => element.ClassName,
            "ControlType" => element.ControlType.ToString(),
            "Value" => TryGetValue(element),
            "Text" => TryGetText(element),
            _ => null
        };
    }

    public int Count(string elementPath)
    {
        var element = FindElement(elementPath);
        if (element == null)
        {
            return 0;
        }

        try
        {
            return 1 + (element.FindAllDescendants()?.Length ?? 0);
        }
        catch
        {
            return 1;
        }
    }

    private static AutomationElement? FindElement(string elementPath)
    {
        return FindElement(elementPath, GuessIdentifierType(elementPath));
    }

    private static AutomationElement? FindElement(string elementPath, string identifierType)
    {
        if (string.IsNullOrWhiteSpace(elementPath))
        {
            return null;
        }

        try
        {
            using var automation = new UIA3Automation();
            var desktop = automation.GetDesktop();
            return UIAutomationHelper.FindElement(desktop, NormalizeIdentifierType(identifierType), elementPath, "Expression");
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeIdentifierType(string? identifierType)
    {
        return string.IsNullOrWhiteSpace(identifierType) ? "XPath" : identifierType.Trim();
    }

    private static string GuessIdentifierType(string elementPath)
    {
        if (elementPath.TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
            return "XPath";
        }

        return "Name";
    }

    private static bool ContainsTextInElementTree(AutomationElement element, string text)
    {
        if (ContainsTextInElement(element, text))
        {
            return true;
        }

        try
        {
            foreach (var descendant in element.FindAllDescendants() ?? [])
            {
                if (ContainsTextInElement(descendant, text))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Inaccessible descendants are ignored; the element itself was already checked.
        }

        return false;
    }

    private static bool ContainsTextInElement(AutomationElement element, string text)
    {
        return Contains(element.Name, text) ||
            Contains(TryGetText(element), text) ||
            Contains(TryGetValue(element), text);
    }

    private static bool Contains(string? candidate, string text)
    {
        return !string.IsNullOrEmpty(candidate) &&
            candidate.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetText(AutomationElement element)
    {
        try
        {
            var textPattern = element.Patterns.Text.PatternOrDefault;
            return textPattern?.DocumentRange.GetText(-1);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetValue(AutomationElement element)
    {
        try
        {
            var valuePattern = element.Patterns.Value.PatternOrDefault;
            return valuePattern?.Value;
        }
        catch
        {
            return null;
        }
    }
}
