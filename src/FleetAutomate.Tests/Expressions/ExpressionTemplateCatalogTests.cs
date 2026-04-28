using FleetAutomate.Expressions;

namespace FleetAutomate.Tests.Expressions;

public class ExpressionTemplateCatalogTests
{
    [Fact]
    public void Templates_IncludeUiDateTimeAndTextTemplatesOnly()
    {
        var templates = ExpressionTemplateCatalog.GetTemplates().ToList();

        Assert.Contains(templates, t => t.Id == "ui.exists");
        Assert.Contains(templates, t => t.Id == "ui.containsText");
        Assert.Contains(templates, t => t.Id == "ui.property");
        Assert.Contains(templates, t => t.Id == "ui.count");
        Assert.Contains(templates, t => t.Id == "datetime.nowLaterThan");
        Assert.Contains(templates, t => t.Id == "datetime.nowEarlierThan");
        Assert.Contains(templates, t => t.Id == "text.containsText");
        Assert.DoesNotContain(templates, t => t.Id.Contains("arithmetic", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(templates, t => t.Id.Contains("logical", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InsertTemplate_InsertsAtCaretPosition()
    {
        var result = ExpressionTemplateCatalog.InsertTemplate(
            "ready && ()",
            caretIndex: 10,
            templateId: "ui.exists");

        Assert.Equal("ready && (uiExists(\"Window/Pane/Button\"))", result.Text);
        Assert.Equal("ready && (uiExists(\"Window/Pane/Button\")".Length, result.CaretIndex);
    }

    [Fact]
    public void InsertTemplate_ThrowsForUnknownTemplate()
    {
        Assert.Throws<ArgumentException>(() => ExpressionTemplateCatalog.InsertTemplate("", 0, "missing"));
    }
}
