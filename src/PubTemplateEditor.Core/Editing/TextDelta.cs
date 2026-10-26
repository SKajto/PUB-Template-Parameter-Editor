namespace PubTemplateEditor.Core.Editing;

/// <summary>
/// The difference between two parameter values in the shape a reviewer reads: the unchanged text
/// around a changed stretch. Comparison is ordinal - no trimming, no case folding, no whitespace
/// normalisation - because those are exactly the things a template value keeps.
///
/// Common prefix plus common suffix is deliberate: it is exact, it is cheap, and it covers the case
/// that matters in these templates (a description or default value edited in the middle). When the
/// two values share nothing, <see cref="IsCompleteReplacement"/> says so instead of pretending a
/// rewrite is a small edit.
/// </summary>
public sealed record TextDelta
{
    private const int ContextCharacters = 32;

    public TextDelta(string? oldValue, string? newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;

        string a = oldValue ?? string.Empty;
        string b = newValue ?? string.Empty;

        IsEqual = string.Equals(a, b, StringComparison.Ordinal);

        int common = 0;
        int limit = Math.Min(a.Length, b.Length);
        while (common < limit && a[common] == b[common])
        {
            common++;
        }

        int suffix = 0;
        while (suffix < limit - common
               && a[a.Length - 1 - suffix] == b[b.Length - 1 - suffix])
        {
            suffix++;
        }

        Prefix = a[..common];
        Removed = a.Substring(common, a.Length - common - suffix);
        Added = b.Substring(common, b.Length - common - suffix);
        Suffix = suffix > 0 ? a[^suffix..] : string.Empty;

        IsCompleteReplacement = !IsEqual && Prefix.Length == 0 && Suffix.Length == 0;
    }

    /// <summary>The value currently in the template, exactly as read.</summary>
    public string? OldValue { get; }

    /// <summary>The staged value, exactly as proposed.</summary>
    public string? NewValue { get; }

    public bool IsEqual { get; }

    /// <summary>True when nothing at all is shared, so the "changed part" view would be meaningless.</summary>
    public bool IsCompleteReplacement { get; }

    public string Prefix { get; }

    public string Removed { get; }

    public string Added { get; }

    public string Suffix { get; }

    public bool HasChange => !IsEqual;

    public bool HasPrefix => Prefix.Length > 0;

    public bool HasSuffix => Suffix.Length > 0;

    public bool HasRemoved => Removed.Length > 0;

    public bool HasAdded => Added.Length > 0;

    /// <summary>
    /// Unchanged context, shortened at the inner edge so the changed stretch stays visible. The marker
    /// says the text was shortened, and the full values are shown separately anyway.
    /// </summary>
    public string PrefixShown => ShortenFromFront(Prefix);

    public string SuffixShown => ShortenFromBack(Suffix);

    public string RemovedShown => ShortenFromMiddle(Removed);

    public string AddedShown => ShortenFromMiddle(Added);

    private static string ShortenFromFront(string text)
        => text.Length <= ContextCharacters ? text : "\u2026" + text[^ContextCharacters..];

    private static string ShortenFromBack(string text)
        => text.Length <= ContextCharacters ? text : text[..ContextCharacters] + "\u2026";

    private static string ShortenFromMiddle(string text)
        => text.Length <= ContextCharacters * 2 ? text : text[..ContextCharacters] + "\u2026" + text[^ContextCharacters..];
}
