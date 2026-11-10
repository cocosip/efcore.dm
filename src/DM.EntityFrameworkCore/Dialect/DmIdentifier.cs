using System.Text;

namespace DM.EntityFrameworkCore.Dialect;

/// <summary>
/// Identifier delimiting and escaping rules for Dameng.
/// </summary>
internal static class DmIdentifier
{
    /// <summary>Dameng delimits identifiers with double quotes.</summary>
    public const char Delimiter = '"';

    private static readonly string Quote = '"'.ToString();
    private static readonly string EscapedQuote = "\"\"";

    /// <summary>Escapes embedded delimiters. The input is not expected to be delimited.</summary>
    public static string Escape(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        return identifier.Replace(Quote, EscapedQuote, StringComparison.Ordinal);
    }

    /// <summary>Wraps the identifier in delimiters, escaping embedded delimiters.</summary>
    public static string Delimit(string identifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        return string.Concat(Quote, Escape(identifier), Quote);
    }

    /// <summary>Wraps the identifier in delimiters without allocating an intermediate string.</summary>
    public static void Delimit(StringBuilder builder, string identifier)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        builder.Append(Delimiter);
        AppendEscaped(builder, identifier);
        builder.Append(Delimiter);
    }

    /// <summary>Appends the escaped identifier without delimiters.</summary>
    public static void AppendEscaped(StringBuilder builder, string identifier)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(identifier);

        var start = builder.Length;
        builder.Append(identifier);

        if (identifier.Contains(Delimiter, StringComparison.Ordinal))
        {
            builder.Replace(Quote, EscapedQuote, start, identifier.Length);
        }
    }

    /// <summary>
    /// Splits a possibly qualified name into its parts, respecting quoting.
    /// </summary>
    public static IReadOnlyList<string> Split(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var parts = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (c == Delimiter)
            {
                if (quoted && i + 1 < name.Length && name[i + 1] == Delimiter)
                {
                    current.Append(Delimiter);
                    i++;
                    continue;
                }

                quoted = !quoted;
                continue;
            }

            if (c == '.' && !quoted)
            {
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        parts.Add(current.ToString());

        return parts;
    }
}
