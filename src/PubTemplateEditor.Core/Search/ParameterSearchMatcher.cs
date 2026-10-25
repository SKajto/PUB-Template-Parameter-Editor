using System.Text.RegularExpressions;

namespace PubTemplateEditor.Core.Search;

/// <summary>
/// Parameter filtering rules for the explorer search box. Names are case-sensitive
/// anchored prefix searches where '*' spans any text; descriptions match anywhere,
/// case-insensitively. A query that occurs inside a name but not at its start
/// counts as a name-region attempt and does not fall through to description matching.
/// Values are matched as text only, never interpreted.
/// </summary>
public static class ParameterSearchMatcher
{
    public static bool Matches(string? exactName, string? description, string rawQuery)
    {
        string query = rawQuery.Trim();
        if (query.Length == 0)
        {
            return true;
        }

        if (exactName is not null && IsMatch(exactName, query, anchored: true, caseSensitive: true))
        {
            return true;
        }

        if (exactName is not null && IsMatch(exactName, query, anchored: false, caseSensitive: false))
        {
            return false;
        }

        return description is not null && IsMatch(description!, query, anchored: false, caseSensitive: false);
    }

    private static bool IsMatch(string value, string query, bool anchored, bool caseSensitive)
    {
        string pattern = BuildPattern(query, anchored);
        RegexOptions options = RegexOptions.CultureInvariant
            | RegexOptions.Singleline          // '.' also spans newlines inside long values
            | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
        return Regex.IsMatch(value, pattern, options, TimeSpan.FromSeconds(1));
    }

    /// <summary>Only '*' is a wildcard in the query; every other character is literal.</summary>
    private static string BuildPattern(string query, bool anchored)
    {
        string[] parts = query.Split('*').Select(Regex.Escape).ToArray();
        string body = string.Join(".*", parts);
        return anchored ? "^" + body : body;
    }
}
