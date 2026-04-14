using System.Buffers;

namespace EaaS.Infrastructure.Data;

/// <summary>
/// Helpers for escaping user-supplied input before it is embedded in SQL
/// LIKE/ILIKE patterns. Prevents wildcard injection (e.g. a caller slipping
/// '%' or '_' into a search term to turn a bounded lookup into a scan or to
/// probe rows they would otherwise not match).
///
/// Must be paired with an explicit ESCAPE clause, e.g.
/// <c>EF.Functions.ILike(column, $"%{SqlLikeEscape.Escape(input)}%", "\\")</c>.
/// </summary>
public static class SqlLikeEscape
{
    /// <summary>
    /// The escape character this helper prefixes ahead of LIKE metacharacters.
    /// Use the same value in the ESCAPE clause at the call site.
    /// </summary>
    public const string EscapeCharacter = "\\";

    private static readonly SearchValues<char> Metacharacters = SearchValues.Create("\\%_");

    /// <summary>
    /// Escapes LIKE/ILIKE metacharacters (<c>\</c>, <c>%</c>, <c>_</c>) in
    /// <paramref name="value"/> by prefixing each with a backslash, so the
    /// resulting pattern matches the literal characters rather than wildcards.
    /// </summary>
    /// <param name="value">Raw user input. <c>null</c> is returned as <see cref="string.Empty"/>.</param>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Fast path: nothing to escape.
        if (value.AsSpan().IndexOfAny(Metacharacters) < 0)
        {
            return value;
        }

        var sb = new System.Text.StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            if (ch is '\\' or '%' or '_')
            {
                sb.Append('\\');
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }
}
