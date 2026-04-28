namespace FleetAutomate.Expressions;

public sealed record ExpressionTemplate(string Id, string DisplayName, string TemplateText);

public sealed record ExpressionTemplateInsertResult(string Text, int CaretIndex);

public static class ExpressionTemplateCatalog
{
    private static readonly IReadOnlyList<ExpressionTemplate> Templates =
    [
        new("ui.exists", "UI exists", "uiExists(\"Window/Pane/Button\")"),
        new("ui.containsText", "UI contains text", "uiContainsText(\"Window/Pane\", \"text\")"),
        new("ui.property", "Get UI property", "getUiProperty(\"Window/Pane/Button\", \"Name\")"),
        new("ui.count", "UI count", "uiCount(\"Window/Pane\")"),
        new("datetime.nowLaterThan", "Now later than", "isNowLaterThan(\"2026-01-01T00:00:00Z\")"),
        new("datetime.nowEarlierThan", "Now earlier than", "isNowEarlierThan(\"2026-01-01T00:00:00Z\")"),
        new("text.containsText", "Text contains", "\"source text\".ContainsText(\"text\")"),
        new("text.startsWith", "Text starts with", "\"source text\".StartsWithText(\"prefix\")"),
        new("text.endsWith", "Text ends with", "\"source text\".EndsWithText(\"suffix\")")
    ];

    public static IReadOnlyList<ExpressionTemplate> GetTemplates() => Templates;

    public static ExpressionTemplateInsertResult InsertTemplate(string text, int caretIndex, string templateId)
    {
        var template = Templates.FirstOrDefault(t => string.Equals(t.Id, templateId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Unknown expression template '{templateId}'.", nameof(templateId));

        caretIndex = Math.Clamp(caretIndex, 0, text.Length);
        var updated = text.Insert(caretIndex, template.TemplateText);
        return new ExpressionTemplateInsertResult(updated, caretIndex + template.TemplateText.Length);
    }
}
