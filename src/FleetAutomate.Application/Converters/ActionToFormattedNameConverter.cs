using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using FleetAutomate.Model;
using FleetAutomate.Model.Actions.UIAutomation;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Helpers;

namespace FleetAutomate.Converters
{
    /// <summary>
    /// Converts an IAction to a formatted TextBlock with colored and bold text.
    /// Parameters follow the current theme accent.
    /// </summary>
    public class ActionToFormattedNameConverter : IValueConverter
    {
        private static Run AccentRun(string text)
        {
            var run = new Run(text);
            run.SetResourceReference(TextElement.ForegroundProperty, "FleetAccentBrush");
            return run;
        }

        private static string ParameterText(IAction action, string propertyName, string literal) =>
            action.TextVariableBindings.TryGetValue(propertyName, out var variable) ? "${" + variable + "}" : literal;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var textBlock = new TextBlock
            {
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };

            switch (value)
            {
                case ClickElementAction clickAction:
                    FormatClickElementAction(textBlock, clickAction);
                    break;

                case SetTextAction setTextAction:
                    FormatSetTextAction(textBlock, setTextAction);
                    break;

                case WaitForElementAction waitAction:
                    FormatWaitForElementAction(textBlock, waitAction);
                    break;

                case IfAction ifAction:
                    FormatIfAction(textBlock, ifAction);
                    break;

                default:
                    // Default: just show the action name
                    if (value is IAction action)
                    {
                        textBlock.Text = action.Name;
                    }
                    break;
            }

            return textBlock;
        }

        private void FormatClickElementAction(TextBlock textBlock, ClickElementAction action)
        {
            var elementIdentifier = ParameterText(action, nameof(action.ElementIdentifier), action.ElementIdentifier);
            if (string.IsNullOrWhiteSpace(elementIdentifier))
            {
                textBlock.Text = action.IsDoubleClick ? "Double-Click Element" : "Click Element";
                return;
            }

            var (controlType, identifier) = ParseElementDescription(elementIdentifier, action.IdentifierType);

            // "Click " or "Double-Click "
            textBlock.Inlines.Add(new Run(action.IsDoubleClick ? "Double-Click " : "Click "));

            // Control type in theme accent
            if (!string.IsNullOrEmpty(controlType))
            {
                textBlock.Inlines.Add(AccentRun(controlType));
                textBlock.Inlines.Add(new Run(" "));
            }

            // Identifier in theme accent
            if (!string.IsNullOrEmpty(identifier))
            {
                textBlock.Inlines.Add(AccentRun(identifier));
            }
        }

        private void FormatSetTextAction(TextBlock textBlock, SetTextAction action)
        {
            var elementIdentifier = ParameterText(action, nameof(action.ElementIdentifier), action.ElementIdentifier);
            if (string.IsNullOrWhiteSpace(elementIdentifier))
            {
                textBlock.Text = "Set Text";
                return;
            }

            var (controlType, identifier) = ParseElementDescription(elementIdentifier, action.IdentifierType);

            // "Set "
            textBlock.Inlines.Add(new Run("Set "));

            // Text value in quotes
            string displayText = ParameterText(action, nameof(action.TextToSet), action.TextToSet);
            if (!string.IsNullOrEmpty(displayText))
            {
                if (displayText.Length > 30)
                {
                    displayText = displayText.Substring(0, 27) + "...";
                }
                textBlock.Inlines.Add(AccentRun($"'{displayText}'"));
                textBlock.Inlines.Add(new Run(" to "));
            }
            else
            {
                textBlock.Inlines.Add(new Run("text to "));
            }

            // Control type in theme accent
            if (!string.IsNullOrEmpty(controlType))
            {
                textBlock.Inlines.Add(AccentRun(controlType));
                textBlock.Inlines.Add(new Run(" "));
            }

            // Identifier in theme accent
            if (!string.IsNullOrEmpty(identifier))
            {
                textBlock.Inlines.Add(AccentRun(identifier));
            }
        }

        private void FormatWaitForElementAction(TextBlock textBlock, WaitForElementAction action)
        {
            var elementIdentifier = ParameterText(action, nameof(action.ElementIdentifier), action.ElementIdentifier);
            if (string.IsNullOrWhiteSpace(elementIdentifier))
            {
                textBlock.Text = "Wait for Element";
                return;
            }

            var (controlType, identifier) = ParseElementDescription(elementIdentifier, action.IdentifierType);

            // "Wait for "
            textBlock.Inlines.Add(new Run("Wait for "));

            // Control type in theme accent
            if (!string.IsNullOrEmpty(controlType))
            {
                textBlock.Inlines.Add(AccentRun(controlType));
                textBlock.Inlines.Add(new Run(" "));
            }

            // Identifier in theme accent
            if (!string.IsNullOrEmpty(identifier))
            {
                textBlock.Inlines.Add(AccentRun(identifier));
            }
        }

        private void FormatIfAction(TextBlock textBlock, IfAction ifAction)
        {
            if (ifAction.Condition == null)
            {
                textBlock.Text = "If Action";
                return;
            }

            // "If "
            textBlock.Inlines.Add(new Run("If "));

            // Handle UIElementExistsExpression
            if (ifAction.Condition is Model.Actions.Logic.Expression.UIElementExistsExpression uiElementExpr)
            {
                var (controlType, identifier) = ParseElementDescription(uiElementExpr.ElementIdentifier, uiElementExpr.IdentifierType);

                // Control type in theme accent
                if (!string.IsNullOrEmpty(controlType))
                {
                    textBlock.Inlines.Add(AccentRun(controlType));
                    textBlock.Inlines.Add(new Run(" "));
                }

                // Identifier in theme accent
                if (!string.IsNullOrEmpty(identifier))
                {
                    textBlock.Inlines.Add(AccentRun(identifier));
                    textBlock.Inlines.Add(new Run(" "));
                }

                textBlock.Inlines.Add(new Run("exists"));
                return;
            }

            // Handle boolean expressions with RawText
            if (ifAction.Condition is Model.Actions.Logic.ExpressionBase<bool> boolExpr &&
                !string.IsNullOrWhiteSpace(boolExpr.RawText))
            {
                string expr = boolExpr.RawText.Trim();
                if (expr.Length > 50)
                {
                    expr = expr.Substring(0, 47) + "...";
                }
                textBlock.Inlines.Add(new Run(expr));
                return;
            }

            // Handle literal boolean values
            if (ifAction.Condition is bool boolVal)
            {
                textBlock.Inlines.Add(new Run(boolVal.ToString().ToLower()));
                return;
            }

            // Fallback
            textBlock.Text = "If Action";
        }

        /// <summary>
        /// Parses element description to extract control type and identifier.
        /// Returns (controlType, identifier) tuple.
        /// </summary>
        private (string controlType, string identifier) ParseElementDescription(string elementIdentifier, string identifierType)
        {
            if (string.IsNullOrWhiteSpace(elementIdentifier))
            {
                return ("Element", "");
            }

            // For XPath, try to extract control type and name/id
            if (identifierType == "XPath" && elementIdentifier.Contains("@"))
            {
                try
                {
                    // Parse patterns like //Button[@Name='OK'] or //Window[@Name='Test']//Button[@AutomationId='btnOk']
                    // Use the LAST match (leaf node in XPath) for display
                    var matches = System.Text.RegularExpressions.Regex.Matches(elementIdentifier, @"(\w+)\[@(\w+)=['""]([^'""]+)['""]");
                    if (matches.Count > 0)
                    {
                        // Use the last match (leaf node)
                        var lastElementMatch = matches[matches.Count - 1];
                        string controlType = lastElementMatch.Groups[1].Value;
                        string identifier = $"'{lastElementMatch.Groups[3].Value}'";
                        return (controlType, identifier);
                    }

                    // Try simple pattern like //Button
                    var simpleMatch = System.Text.RegularExpressions.Regex.Match(elementIdentifier, @"//(\w+)$");
                    if (simpleMatch.Success)
                    {
                        return (simpleMatch.Groups[1].Value, "");
                    }
                }
                catch { }
            }

            // For AutomationId, Name, ClassName - show the value
            if (identifierType == "Name" || identifierType == "AutomationId")
            {
                return ("Element", $"'{elementIdentifier}'");
            }

            if (identifierType == "ClassName")
            {
                return ("Element", $"of class '{elementIdentifier}'");
            }

            // Fallback: show truncated identifier
            string truncated = elementIdentifier.Length > 30 ? elementIdentifier.Substring(0, 27) + "..." : elementIdentifier;
            return ("Element", $"'{truncated}'");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
