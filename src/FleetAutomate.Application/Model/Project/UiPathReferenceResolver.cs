namespace FleetAutomate.Model.Project;

/// <summary>Scopes project paths to the current async flow, including expression evaluation.</summary>
public static class UiPathReferenceResolver
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, string>?> Current = new();

    public static IDisposable Enter(IEnumerable<UiPathReference>? references)
    {
        var previous = Current.Value;
        if (references != null)
        {
            Current.Value = references.ToDictionary(reference => reference.Name, reference => reference.XPath, StringComparer.OrdinalIgnoreCase);
        }
        return new Scope(previous);
    }

    public static bool IsReference(string value) => value.Trim().StartsWith("@{", StringComparison.Ordinal) && value.Trim().EndsWith('}');

    public static string Resolve(string value)
    {
        if (!IsReference(value)) return value;
        var name = value.Trim()[2..^1];
        return Current.Value?.TryGetValue(name, out var xpath) == true
            ? xpath : throw new InvalidOperationException($"Project UI path reference '{name}' was not found.");
    }

    private sealed class Scope(IReadOnlyDictionary<string, string>? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
