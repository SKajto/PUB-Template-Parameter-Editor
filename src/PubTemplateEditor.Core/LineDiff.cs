namespace PubTemplateEditor.Core;

/// <summary>
/// Minimal built-in line diff (Myers-style LCS) for readable old/new presentation.
/// Never ignores whitespace or letter case.
/// </summary>
public static class LineDiff
{
    public static IReadOnlyList<DiffPair> Diff(string? oldValue, string? newValue)
    {
        string[] a = Split(oldValue);
        string[] b = Split(newValue);

        int n = a.Length, m = b.Length;
        // LCS table; field sizes are bounded by realistic description/default sizes.
        int[,] lcs = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = m - 1; j >= 0; j--)
            {
                lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var result = new List<DiffPair>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
            {
                result.Add(new DiffPair(false, a[x], b[y]));
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                result.Add(new DiffPair(true, a[x], null));
                x++;
            }
            else
            {
                result.Add(new DiffPair(true, null, b[y]));
                y++;
            }
        }

        while (x < n)
        {
            result.Add(new DiffPair(true, a[x++], null));
        }

        while (y < m)
        {
            result.Add(new DiffPair(true, null, b[y++]));
        }

        return result;
    }

    public sealed record DiffPair(bool Changed, string? OldText, string? NewText);

    private static string[] Split(string? value) =>
        value is null || value.Length == 0
            ? []
            : value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
}
