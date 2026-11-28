namespace CSharpCodeAnalyst.Linux;

/// <summary>
///     The parsed command line. Same syntax as the application's own command line
///     (<c>-flag</c> and <c>-key:value</c>, keys case-insensitive), so a user only learns one style.
///     Unlike the application, a stray positional argument is an error rather than ignored: a typo
///     on a build agent should fail loudly, not run with a default.
/// </summary>
internal sealed class Arguments
{
    private readonly Dictionary<string, string> _values;

    private Arguments(Dictionary<string, string> values)
    {
        _values = values;
    }

    public IEnumerable<string> Keys
    {
        get => _values.Keys;
    }

    /// <exception cref="ArgumentException">An argument does not start with '-' or has no key.</exception>
    public static Arguments Parse(IEnumerable<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var arg in args)
        {
            if (!arg.StartsWith('-') || arg.Length == 1)
            {
                throw new ArgumentException($"Unexpected argument '{arg}'. Arguments have the form -flag or -key:value.");
            }

            // Split at the first colon only: the value may be a Windows path ("C:\...") or contain
            // colons of its own.
            var colonIndex = arg.IndexOf(':');
            if (colonIndex == 1)
            {
                throw new ArgumentException($"Unexpected argument '{arg}'. Arguments have the form -flag or -key:value.");
            }

            if (colonIndex < 0)
            {
                values[arg[1..]] = string.Empty;
            }
            else
            {
                values[arg[1..colonIndex]] = arg[(colonIndex + 1)..];
            }
        }

        return new Arguments(values);
    }

    public bool Has(string key)
    {
        return _values.ContainsKey(key);
    }

    public string? Get(string key)
    {
        return _values.TryGetValue(key, out var value) && value.Length > 0 ? value : null;
    }
}
