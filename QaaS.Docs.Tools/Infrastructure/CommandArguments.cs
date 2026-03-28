namespace QaaS.Docs.Tools.Infrastructure;

/// <summary>
/// Parses simple <c>--name value</c> options and boolean <c>--flag</c> switches without taking a dependency on
/// an external command-line framework.
/// </summary>
internal sealed class CommandArguments
{
    private readonly Dictionary<string, List<string>> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Parses the raw CLI token list into named values and switch flags.
    /// </summary>
    public static CommandArguments Parse(IEnumerable<string> args)
    {
        var parsed = new CommandArguments();
        var values = args.ToArray();

        for (var index = 0; index < values.Length; index++)
        {
            var current = values[index];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (index + 1 < values.Length && !values[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                if (!parsed._values.TryGetValue(current, out var entries))
                {
                    entries = [];
                    parsed._values[current] = entries;
                }

                entries.Add(values[++index]);
                continue;
            }

            parsed._flags.Add(current);
        }

        return parsed;
    }

    /// <summary>
    /// Returns <see langword="true"/> when a switch-style option is present.
    /// </summary>
    public bool HasFlag(string name) => _flags.Contains(name);

    /// <summary>
    /// Reads the last supplied value for an option when one exists.
    /// </summary>
    public bool TryGetSingleValue(string name, out string value)
    {
        if (_values.TryGetValue(name, out var entries) && entries.Count != 0)
        {
            value = entries[^1];
            return true;
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Reads and normalizes an optional path value.
    /// </summary>
    public string? GetOptionalPath(string name)
    {
        return TryGetSingleValue(name, out var value)
            ? Path.GetFullPath(value)
            : null;
    }

    /// <summary>
    /// Returns every supplied value for a repeated option.
    /// </summary>
    public IReadOnlyList<string> GetValues(string name)
    {
        return _values.TryGetValue(name, out var entries)
            ? entries
            : [];
    }
}
