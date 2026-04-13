namespace FleetAutomate.Cli.Infrastructure;

internal sealed class CliArgumentParser
{
    private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);

    public CliArgumentParser(IEnumerable<string> args)
    {
        foreach (var arg in args)
        {
            if (arg is "--help" or "-h")
            {
                IsHelpRequested = true;
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var split = arg[2..].Split('=', 2);
                if (split.Length == 2)
                {
                    _options[split[0]] = split[1];
                }
                else
                {
                    PendingOption = split[0];
                }

                continue;
            }

            if (PendingOption != null)
            {
                _options[PendingOption] = arg;
                PendingOption = null;
                continue;
            }

            Positionals.Add(arg);
        }

        if (PendingOption != null)
        {
            throw new CliUsageException($"Missing value for option '--{PendingOption}'.");
        }
    }

    public bool IsHelpRequested { get; }

    public List<string> Positionals { get; } = [];

    private string? PendingOption { get; set; }

    public string? GetOption(string name)
    {
        return _options.TryGetValue(name, out var value) ? value : null;
    }

    public string GetRequiredOption(string name)
    {
        return GetOption(name) ?? throw new CliUsageException($"Missing required option '--{name}'.");
    }
}

