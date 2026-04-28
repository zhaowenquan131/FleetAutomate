namespace FleetAutomate.Expressions;

public sealed class NullExpressionUiQueryService : IExpressionUiQueryService
{
    public static NullExpressionUiQueryService Instance { get; } = new();

    private NullExpressionUiQueryService()
    {
    }

    public bool Exists(string elementPath) => false;

    public bool Exists(string elementPath, string identifierType, int retryTimes) => false;

    public bool ContainsText(string elementPath, string text) => false;

    public string? GetProperty(string elementPath, string propertyName) => null;

    public int Count(string elementPath) => 0;
}
