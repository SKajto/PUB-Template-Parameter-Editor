using System.Net.Http;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Core.Updates;
using PubTemplateEditor.Infrastructure;

namespace PubTemplateEditor.App;

/// <summary>
/// Offline licence/notices viewer. The texts are compiled into the executable, so the licence stays
/// reachable without network access (GPL distribution rule). Only plain text is shown; no bundled text
/// is ever interpreted as markup.
///
/// Two things here can reach outside, and both only when the user acts on them: a web link is handed to
/// the shell to open, and a release check asks one service for one number. Nothing is downloaded,
/// nothing is uploaded, and no request happens on its own.
/// </summary>
public partial class LicensesWindow : Window
{
    private readonly IReleaseFeed _releaseFeed;
    private readonly ILinkOpener _linkOpener;
    private CancellationTokenSource? _checkInProgress;

    /// <summary>Text panes exposed for UI tests (the x:Name fields are internal to this assembly).</summary>
    public string LicenseContent => LicenseText.Text;
    public string NoticeContent => NoticeText.Text;
    public string ThirdPartyContent => ThirdPartyText.Text;
    public int NoticeTabCount => NoticeTabs.Items.Count;

    /// <summary>About view fields, exposed for UI tests.</summary>
    public string AboutName => AboutNameText.Text;
    public string AboutVersion => AboutVersionText.Text;
    public string AboutLicense => AboutLicenseText.Text;
    public string AboutCopyright => AboutCopyrightText.Text;
    public string AboutRuntime => AboutRuntimeText.Text;
    public string AboutBundled => AboutBundledText.Text;
    public string AboutRepositoryUri => RepositoryLink.NavigateUri?.ToString() ?? string.Empty;
    public string AboutSourceUri => SourceLink.NavigateUri?.ToString() ?? string.Empty;
    public string AboutIssuesUri => IssuesLink.NavigateUri?.ToString() ?? string.Empty;
    public string UpdateStatus => UpdateStatusRun.Text;
    public string ReleaseLinkUri => ReleaseLink.NavigateUri?.ToString() ?? string.Empty;

    /// <summary>Whether the release link is part of the status line right now.</summary>
    public bool IsReleaseLinkVisible => UpdateStatusText.Inlines.Contains(ReleaseLink);
    public bool IsCheckingForUpdate => _checkInProgress is not null;

    /// <param name="releaseFeed">
    /// Null means the real release service. Tests pass a double, so a test never asks a network and can
    /// check exactly how often one was asked.
    /// </param>
    /// <param name="linkOpener">Null means the shell opens the link. Tests pass a double for the same reason.</param>
    public LicensesWindow(IReleaseFeed? releaseFeed = null, ILinkOpener? linkOpener = null)
    {
        InitializeComponent();
        WindowFrameTheme.Attach(this); // caption follows the interface theme, not only the client area

        _releaseFeed = releaseFeed ?? new GitHubReleaseFeed(ProductInfo.RepositoryUrl, ProductInfo.Version);
        _linkOpener = linkOpener ?? new ShellLinkOpener();

        LicenseText.Text = EmbeddedNotices.Read(EmbeddedNotices.License);
        NoticeText.Text = EmbeddedNotices.Read(EmbeddedNotices.CopyrightNotice);
        ThirdPartyText.Text = EmbeddedNotices.Read(EmbeddedNotices.ThirdParty);

        // About view: identity, licence and runtime.
        AboutNameText.Text = ProductInfo.Name;
        AboutTaglineText.Text = ProductInfo.Tagline;
        AboutDescriptionText.Text = ProductInfo.Description;
        AboutVersionText.Text = ProductInfo.Version;
        AboutLicenseText.Text = ProductInfo.LicenseId;
        AboutCopyrightText.Text = ProductInfo.CopyrightLine;
        AboutRuntimeText.Text = $"{ProductInfo.RuntimeDescription}, {ProductInfo.ProcessArchitecture} process on {ProductInfo.OperatingSystemDescription}";

        SetLink(RepositoryLink, RepositoryLinkText, ProductInfo.RepositoryUrl, ProductInfo.RepositoryLabel);
        SetLink(SourceLink, SourceLinkText, ProductInfo.ReleaseSourceUrl, $"v{ProductInfo.Version} source archive");
        SetLink(IssuesLink, IssuesLinkText, ProductInfo.IssuesUrl, "report a problem or ask a question");

        // The status line stays empty until a release check has an answer to give.
        HideReleaseLink();

        AboutBundledText.Text =
            $".NET runtime and WPF ({ProductInfo.RuntimeDescription}) are bundled in the self-contained " +
            "payload under the MIT/.NET Runtime licence; they are not relicensed. Build tooling such as " +
            "xUnit is test-only and not shipped. Full texts are in the Third-party tab.";
        AboutWarrantyText.Text =
            "This program is free software and comes with ABSOLUTELY NO WARRANTY; to the extent permitted " +
            "by law, no warranty is given. You can redistribute it and/or modify it under the GNU General " +
            "Public License version 3.";

        Closed += (_, _) => _checkInProgress?.Cancel();
    }

    private static void SetLink(Hyperlink link, Run text, string url, string label)
    {
        link.NavigateUri = new Uri(url);
        text.Text = label;
    }

    // ---------------------------------------------------------------- links

    private void OnLinkNavigate(object sender, RequestNavigateEventArgs args)
    {
        OpenUrl(args.Uri?.ToString());
        args.Handled = true;
    }

    /// <summary>
    /// Opens an address with the user's own browser choice. A machine that cannot open one is told so in
    /// the status line, where the address is written out in full, rather than by an exception dialog.
    /// </summary>
    public void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            _linkOpener.Open(url);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            UpdateStatusRun.Text = ex.Message;
        }
    }

    // ---------------------------------------------------------------- release check

    private void CheckUpdate_Click(object sender, RoutedEventArgs e) => _ = CheckForUpdateAsync();

    /// <summary>
    /// One anonymous question to the release service: which version is published as the newest. A second
    /// click while one is running does nothing, closing the window cancels it, and whatever the answer is,
    /// nothing is downloaded - the release page is offered as a link for the user to decide about.
    /// </summary>
    public async Task CheckForUpdateAsync()
    {
        if (_checkInProgress is not null)
        {
            return;
        }

        _checkInProgress = new CancellationTokenSource();
        CheckUpdateButton.IsEnabled = false;
        HideReleaseLink();
        UpdateStatusRun.Text = "Checking…";

        try
        {
            ReleaseFeedResult answer = await _releaseFeed.FetchLatestReleaseAsync(_checkInProgress.Token);

            if (!answer.Succeeded)
            {
                UpdateStatusRun.Text = $"Release check failed: {answer.Reason}";
                return;
            }

            ReleaseInfo? published = null;
            if (!ReleaseInfo.TryParse(answer.Json, out published))
            {
                UpdateStatusRun.Text = "The release service answered with something that is not a release.";
                return;
            }

            UpdateCheckResult comparison = UpdateDecision.Compare(ProductInfo.Version, published);
            switch (comparison.State)
            {
                case UpdateState.NewerAvailable:
                    SetReleaseLink(ProductInfo.ReleasesUrl, $"open the {published.Tag} release");
                    UpdateStatusRun.Text = $"{published.Tag} is available; this program is {ProductInfo.Version}."
                                         + (published.IsPreRelease ? " Pre-release." : string.Empty) + " ";
                    break;

                case UpdateState.UpToDate:
                    UpdateStatusRun.Text = $"{ProductInfo.Version} is the newest published release.";
                    break;

                case UpdateState.UnknownVersion:
                    UpdateStatusRun.Text = $"The newest published release is tagged '{published.Tag}', which is not a version number.";
                    break;

                default:
                    UpdateStatusRun.Text = "The release answer could not be read.";
                    break;
            }
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
            _checkInProgress.Dispose();
            _checkInProgress = null;
        }
    }

    private void SetReleaseLink(string url, string label)
    {
        ReleaseLink.NavigateUri = new Uri(url);
        ReleaseLinkText.Text = label;
        if (!UpdateStatusText.Inlines.Contains(ReleaseLink))
        {
            UpdateStatusText.Inlines.Add(ReleaseLink);   // an inline element has no Visibility: it is in the line, or not
        }
    }

    private void HideReleaseLink()
    {
        UpdateStatusText.Inlines.Remove(ReleaseLink);
    }
}
