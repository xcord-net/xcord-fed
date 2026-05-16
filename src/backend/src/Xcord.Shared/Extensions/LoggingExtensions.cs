namespace Xcord.Shared.Extensions;

/// <summary>
/// Helpers for sanitizing user-controlled strings before they enter log output.
/// </summary>
/// <remarks>
/// Untrusted strings (URLs, names, descriptions, headers) can contain:
/// <list type="bullet">
///   <item>Control characters such as CR/LF/TAB that can forge fake log lines</item>
///   <item>Arbitrary length, allowing an attacker to flood logs</item>
/// </list>
/// Pass any string sourced from a request body, query string, or remote
/// instance through <see cref="SafeForLog"/> before emitting it via
/// <c>ILogger</c>.
/// </remarks>
public static class LoggingExtensions
{
    /// <summary>
    /// Strips control characters (\r, \n, \t, etc.) and truncates the input to
    /// at most <paramref name="max"/> visible characters, appending an
    /// ellipsis when truncated. Returns a sentinel for null/empty input.
    /// </summary>
    public static string SafeForLog(this string? value, int max = 256)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "<empty>";
        }

        // Strip control characters in one pass. Whitespace inside the string
        // (regular spaces) is preserved; only the C0/C1 control range is removed.
        Span<char> buffer = value.Length <= 1024
            ? stackalloc char[value.Length]
            : new char[value.Length];

        var written = 0;
        foreach (var c in value)
        {
            if (!char.IsControl(c))
            {
                buffer[written++] = c;
            }
        }

        var sanitized = buffer[..written];

        if (sanitized.Length <= max)
        {
            return new string(sanitized);
        }

        // Truncate; ellipsis is a single Unicode char, kept outside the budget
        // so the budget itself stays predictable for callers.
        return new string(sanitized[..max]) + "…";
    }
}
