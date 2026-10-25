using System.Reflection;
using System.Windows;
using PubTemplateEditor.App.Infrastructure;

namespace PubTemplateEditor.App;

/// <summary>
/// Offline licence/notices viewer. The texts are compiled into the executable, so
/// the licence stays reachable without network access (GPL distribution rule).
/// Only plain text is shown; no bundled text is ever interpreted as markup.
/// </summary>
public partial class LicensesWindow : Window
{
    /// <summary>Text panes exposed for UI tests (the x:Name fields are internal to this assembly).</summary>
    public string LicenseContent => LicenseText.Text;
    public string NoticeContent => NoticeText.Text;
    public string ThirdPartyContent => ThirdPartyText.Text;
    public string SourceContent => SourceLine.Text;
    public int NoticeTabCount => NoticeTabs.Items.Count;

    public LicensesWindow()
    {
        InitializeComponent();

        LicenseText.Text = EmbeddedNotices.Read(EmbeddedNotices.License);
        NoticeText.Text = EmbeddedNotices.Read(EmbeddedNotices.CopyrightNotice);
        ThirdPartyText.Text = EmbeddedNotices.Read(EmbeddedNotices.ThirdParty);

        string version = typeof(LicensesWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        SourceLine.Text =
            $"Version {version}. The corresponding source for this exact build is published with the " +
            "release of the same version under GPL-3.0-only (see the NOTICE tab).";
    }
}
