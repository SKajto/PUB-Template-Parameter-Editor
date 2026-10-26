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

    /// <summary>About view fields, exposed for UI tests.</summary>
    public string AboutName => AboutNameText.Text;
    public string AboutVersion => AboutVersionText.Text;
    public string AboutLicense => AboutLicenseText.Text;
    public string AboutCopyright => AboutCopyrightText.Text;
    public string AboutRuntime => AboutRuntimeText.Text;
    public string AboutBundled => AboutBundledText.Text;
    public string AboutSource => AboutSourceText.Text;

    public LicensesWindow()
    {
        InitializeComponent();
        WindowFrameTheme.Attach(this); // caption follows the interface theme, not only the client area

        LicenseText.Text = EmbeddedNotices.Read(EmbeddedNotices.License);
        NoticeText.Text = EmbeddedNotices.Read(EmbeddedNotices.CopyrightNotice);
        ThirdPartyText.Text = EmbeddedNotices.Read(EmbeddedNotices.ThirdParty);

        // About view: identity, licence and the bundled runtime, all offline.
        AboutNameText.Text = ProductInfo.Name;
        AboutTaglineText.Text = ProductInfo.Tagline;
        AboutDescriptionText.Text = ProductInfo.Description;
        AboutVersionText.Text = ProductInfo.Version;
        AboutLicenseText.Text = $"{ProductInfo.LicenseId} (see the GNU GPL v3 tab for the full text)";
        AboutCopyrightText.Text = ProductInfo.CopyrightLine;
        AboutRuntimeText.Text = $"{ProductInfo.RuntimeDescription}, {ProductInfo.ProcessArchitecture} process on {ProductInfo.OperatingSystemDescription}";
        AboutSourceText.Text = ProductInfo.SourceNote + " Source: " + ProductInfo.RepositoryUrl
                             + ". This build: " + ProductInfo.ReleaseSourceUrl;
        AboutBundledText.Text =
            $".NET runtime and WPF ({ProductInfo.RuntimeDescription}) are bundled in the self-contained " +
            "payload under the MIT/.NET Runtime licence; they are not relicensed. Build tooling such as " +
            "xUnit is test-only and not shipped. Full texts are in the Third-party tab.";
        AboutWarrantyText.Text =
            "This program is free software and comes with ABSOLUTELY NO WARRANTY; to the extent permitted " +
            "by law, no warranty is given. You can redistribute it and/or modify it under the GNU General " +
            "Public License version 3.";

        string version = ProductInfo.Version;
        SourceLine.Text =
            $"Version {version}. " + ProductInfo.SourceNote + " " + ProductInfo.ReleaseSourceUrl;
    }
}
