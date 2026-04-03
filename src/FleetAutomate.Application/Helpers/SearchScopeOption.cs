namespace FleetAutomate.Helpers
{
    /// <summary>
    /// User-facing search scope option that still preserves the runtime dictionary key.
    /// </summary>
    public sealed record SearchScopeOption(string Key, string DisplayText);
}
