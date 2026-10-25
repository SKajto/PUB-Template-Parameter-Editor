using System.IO;
using System.Windows;
using System.Windows.Resources;

namespace PubTemplateEditor.App.Infrastructure;

/// <summary>
/// Offline access to the license and notice texts that are compiled into the
/// application as WPF resources. REL/notice requirements: the licence text must
/// be reachable without a network connection, from inside the app.
/// </summary>
public static class EmbeddedNotices
{
    public sealed record Notice(string Title, string ResourcePath);

    private static string Component(string name)
    {
        // The App assembly is the entry assembly when the exe runs, but a plain
        // library when the WPF test host loads it. An assembly-qualified pack URI
        // resolves the resource table in both cases.
        string assembly = typeof(EmbeddedNotices).Assembly.GetName().Name!;
        return $"pack://application:,,,/{assembly};component/Assets/{name}";
    }

    public static readonly Notice License = new Notice(
        "GNU General Public License v3 (application licence)", Component("LICENSE.txt"));

    public static readonly Notice CopyrightNotice = new Notice(
        "Copyright and bundled-component notice", Component("NOTICE.txt"));

    public static readonly Notice ThirdParty = new Notice(
        "Third-party notices (.NET/WPF and build tools)", Component("THIRD-PARTY-NOTICES.txt"));

    public static readonly IReadOnlyList<Notice> All = [License, CopyrightNotice, ThirdParty];

    /// <summary>Returns the plain text of a notice, or a clear marker if the resource is absent.</summary>
    public static string Read(Notice notice)
    {
        try
        {
            StreamResourceInfo? info = Application.GetResourceStream(new Uri(notice.ResourcePath, UriKind.Absolute));
            if (info is null)
            {
                return $"(bundled notice '{notice.Title}' is missing from this build)";
            }

            using StreamReader reader = new(info.Stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            return $"(bundled notice '{notice.Title}' could not be read: {ex.Message})";
        }
    }
}
