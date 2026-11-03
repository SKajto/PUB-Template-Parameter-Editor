using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace PubTemplateEditor.Core.Updates;

/// <summary>
/// What a release service says about the newest published release. Parsing is kept apart from
/// transport so that the meaning of an answer can be checked without any network: what arrives is
/// untrusted text, and an answer this code cannot read is reported as unreadable rather than guessed
/// into a version number.
/// </summary>
public sealed record ReleaseInfo(string Tag, string Url, bool IsPreRelease)
{
    /// <summary>Reads the fields of a GitHub release document. Returns false when there is no usable tag.</summary>
    public static bool TryParse(string? json, [NotNullWhen(true)] out ReleaseInfo? release)
    {
        release = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            string? tag = Text(root, "tag_name");
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            string url = Text(root, "html_url") ?? string.Empty;
            bool preRelease = root.TryGetProperty("prerelease", out JsonElement flag)
                              && flag.ValueKind is JsonValueKind.True;

            release = new ReleaseInfo(tag.Trim(), url, preRelease);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// The version a tag names. Release tags are "v1.2.3" by this project's convention; a fourth
    /// component and surrounding spaces are accepted. Anything else is not a version, including a bare
    /// major.minor, which cannot be told apart from an unfinished number.
    /// </summary>
    public static bool TryParseVersion(string? tagOrVersion, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(tagOrVersion))
        {
            return false;
        }

        // A running build may carry "+<commit>" build metadata from the source it was built from; that
        // says which commit produced a binary, it is not part of the version anyone compares.
        string text = tagOrVersion.Trim().TrimStart('v', 'V').Split('+')[0].Trim();
        if (!Version.TryParse(text, out Version? parsed) || parsed.Build < 0)
        {
            return false;
        }

        version = parsed;
        return true;
    }

    public Version? ParsedVersion => TryParseVersion(Tag, out Version parsed) ? parsed : null;
}

/// <summary>How a release answer relates to the version that is running.</summary>
public enum UpdateState
{
    /// <summary>The running version is the newest published one, or newer than it.</summary>
    UpToDate,

    /// <summary>Something newer is published; the user decides what to do about it.</summary>
    NewerAvailable,

    /// <summary>The service answered, but the answer could not be read as a release.</summary>
    UnreadableAnswer,

    /// <summary>The service answered with something usable but no version this code can compare.</summary>
    UnknownVersion,
}

/// <summary>The comparison of the running version against the newest published release.</summary>
public sealed record UpdateCheckResult(UpdateState State, string? NewestTag, string? Url, bool IsPreRelease);

public static class UpdateDecision
{
    /// <summary>
    /// Compares version numbers only. Nothing here downloads or installs anything: whether a newer
    /// release is wanted, and how it is obtained, is the user's decision.
    /// </summary>
    public static UpdateCheckResult Compare(string? runningVersion, ReleaseInfo? published)
    {
        if (published is null)
        {
            return new UpdateCheckResult(UpdateState.UnreadableAnswer, null, null, false);
        }

        if (!ReleaseInfo.TryParseVersion(runningVersion, out Version? running))
        {
            return new UpdateCheckResult(UpdateState.UnknownVersion, published.Tag, published.Url, published.IsPreRelease);
        }

        Version? newest = published.ParsedVersion;
        if (newest is null)
        {
            return new UpdateCheckResult(UpdateState.UnknownVersion, published.Tag, published.Url, published.IsPreRelease);
        }

        int order = newest.CompareTo(running);
        return order > 0
            ? new UpdateCheckResult(UpdateState.NewerAvailable, published.Tag, published.Url, published.IsPreRelease)
            : new UpdateCheckResult(UpdateState.UpToDate, published.Tag, published.Url, published.IsPreRelease);
    }
}
