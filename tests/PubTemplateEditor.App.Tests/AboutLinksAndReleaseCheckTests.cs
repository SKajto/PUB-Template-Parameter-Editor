using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using PubTemplateEditor.App;
using PubTemplateEditor.App.Infrastructure;
using PubTemplateEditor.Infrastructure;
using Xunit;

namespace PubTemplateEditor.UiTests;

/// <summary>
/// The About window's two ways of reaching outside: a link handed to the user's own browser, and a
/// release check that asks one service for one number. Both are driven here through the real WPF path -
/// a hyperlink invoked the way an accessibility client invokes it - with only the browser hand-off and
/// the network replaced, so no test can launch a browser or ask a service anything.
///
/// Everything that reads a window does so on the UI thread, because a WPF object belongs to the thread
/// that created it.
/// </summary>
[Collection("WPF shell")]
public class AboutLinksAndReleaseCheckTests
{
    private readonly WpfHost _host;

    public AboutLinksAndReleaseCheckTests(WpfHost host) => _host = host;

    private sealed class RecordingOpener : ILinkOpener
    {
        public List<string> Opened { get; } = [];
        public Exception? Throws { get; set; }

        public void Open(string url)
        {
            if (Throws is not null)
            {
                throw Throws;
            }

            Opened.Add(url);
        }
    }

    private sealed class StubFeed : IReleaseFeed
    {
        public int Calls { get; private set; }
        public string? Json { get; set; }
        public string? Refusal { get; set; }
        public Func<CancellationToken, Task>? WaitBeforeAnswering { get; set; }

        public async Task<ReleaseFeedResult> FetchLatestReleaseAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            if (WaitBeforeAnswering is not null)
            {
                await WaitBeforeAnswering(cancellationToken);
            }

            return Json is not null
                ? ReleaseFeedResult.Received(Json)
                : ReleaseFeedResult.Refused(Refusal ?? "no answer was configured");
        }
    }

    private sealed record AboutState(string RepositoryUri, string SourceUri, string IssuesUri, string Status,
                                     bool Checking, bool ReleaseLinkVisible, string ReleaseLinkUri, int TabCount);

    private LicensesWindow NewWindow(IReleaseFeed? feed = null, ILinkOpener? opener = null)
    {
        LicensesWindow window = null!;
        _host.Do(() =>
        {
            window = new LicensesWindow(feed, opener);
            window.Show();
        });
        return window;
    }

    private AboutState Read(LicensesWindow window) => _host.Do(() => new AboutState(
        window.AboutRepositoryUri, window.AboutSourceUri, window.AboutIssuesUri, window.UpdateStatus,
        window.IsCheckingForUpdate, window.IsReleaseLinkVisible, window.ReleaseLinkUri, window.NoticeTabCount));

    private void ClickLink(LicensesWindow window, System.Windows.Documents.Hyperlink link) =>
        _host.Do(() => ((IInvokeProvider)new HyperlinkAutomationPeer(link)).Invoke());

    /// <summary>Pumps the UI thread until the window's check has finished, so a test never sleeps on a guess.</summary>
    private void WaitUntilIdle(LicensesWindow window)
    {
        for (int spin = 0; spin < 200 && _host.Do(() => window.IsCheckingForUpdate); spin++)
        {
            _host.Do(() => System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                () => { }, System.Windows.Threading.DispatcherPriority.Background));
        }
    }

    [Fact]
    public void TheProjectSourceIsALinkRatherThanTextSomebodyHasToCopy()
    {
        LicensesWindow window = NewWindow();
        AboutState about = Read(window);

        Assert.Equal(ProductInfo.RepositoryUrl, about.RepositoryUri);
        Assert.Equal(ProductInfo.ReleaseSourceUrl, about.SourceUri);
        Assert.Equal(ProductInfo.IssuesUrl, about.IssuesUri);
        Assert.Contains($"/releases/tag/v{ProductInfo.Version}", about.SourceUri, StringComparison.Ordinal);

        _host.Do(() => window.Close());
    }

    [Fact]
    public void ClickingTheProjectLinkHandsTheAddressToTheUsersBrowser()
    {
        var opener = new RecordingOpener();
        LicensesWindow window = NewWindow(opener: opener);

        ClickLink(window, _host.Do(() => window.RepositoryLink));

        Assert.Equal([ProductInfo.RepositoryUrl], opener.Opened);

        _host.Do(() => window.Close());
    }

    [Fact]
    public void EachLinkGoesToThePageItsLabelPromises()
    {
        var opener = new RecordingOpener();
        LicensesWindow window = NewWindow(opener: opener);

        ClickLink(window, _host.Do(() => window.SourceLink));
        ClickLink(window, _host.Do(() => window.IssuesLink));

        Assert.Equal([ProductInfo.ReleaseSourceUrl, ProductInfo.IssuesUrl], opener.Opened);

        _host.Do(() => window.Close());
    }

    [Fact]
    public void AWebsiteThisMachineCannotOpenIsSaidInTheWindowNotThrownAtTheUser()
    {
        var opener = new RecordingOpener { Throws = new InvalidOperationException("This Windows account has no program to open a web link with.") };
        LicensesWindow window = NewWindow(opener: opener);

        ClickLink(window, _host.Do(() => window.RepositoryLink));

        Assert.Contains("no program to open", Read(window).Status, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Read(window).ReleaseLinkUri);

        _host.Do(() => window.Close());
    }

    [Fact]
    public void OpeningTheAboutWindowAsksNobodyAnything()
    {
        var feed = new StubFeed { Json = """{ "tag_name": "v9.9.9" }""" };
        LicensesWindow window = NewWindow(feed);
        AboutState about = Read(window);

        Assert.Equal(0, feed.Calls);                 // no request on opening, and none in the background
        Assert.False(about.Checking);
        Assert.False(about.ReleaseLinkVisible);
        Assert.Contains("No check has been made", about.Status, StringComparison.Ordinal);

        _host.Do(() => window.Close());
    }

    [Fact]
    public async Task ANewerPublishedReleaseIsNamedAndOfferedAsALinkNotAsADownload()
    {
        var feed = new StubFeed
        {
            Json = """
                { "tag_name": "v9.9.9",
                  "html_url": "https://github.com/SKajto/PUB-Template-Parameter-Editor/releases/tag/v9.9.9",
                  "prerelease": false }
                """,
        };
        LicensesWindow window = NewWindow(feed);

        await _host.Do(() => window.CheckForUpdateAsync());
        AboutState about = Read(window);

        Assert.Equal(1, feed.Calls);
        Assert.Contains("v9.9.9", about.Status, StringComparison.Ordinal);
        Assert.Contains(ProductInfo.Version, about.Status, StringComparison.Ordinal);
        Assert.True(about.ReleaseLinkVisible);
        Assert.EndsWith("/releases", about.ReleaseLinkUri, StringComparison.Ordinal);

        _host.Do(() => window.Close());
    }

    [Fact]
    public async Task RunningTheNewestPublishedReleaseSaysSoPlainly()
    {
        var feed = new StubFeed
        {
            Json = $$"""{ "tag_name": "v{{ProductInfo.Version}}", "html_url": "https://github.com/SKajto/PUB-Template-Parameter-Editor/releases" }""",
        };
        LicensesWindow window = NewWindow(feed);

        await _host.Do(() => window.CheckForUpdateAsync());

        Assert.Contains("newest published release", Read(window).Status, StringComparison.OrdinalIgnoreCase);

        _host.Do(() => window.Close());
    }

    [Fact]
    public async Task AReleaseServiceThatCannotBeReachedSaysWhatFailedAndChangesNothingElse()
    {
        var feed = new StubFeed { Refusal = "the release service could not be reached (no route to host)" };
        var opener = new RecordingOpener();
        LicensesWindow window = NewWindow(feed, opener);

        await _host.Do(() => window.CheckForUpdateAsync());
        AboutState about = Read(window);

        Assert.Contains("No release check", about.Status, StringComparison.Ordinal);
        Assert.Contains("no route to host", about.Status, StringComparison.Ordinal);
        Assert.False(about.ReleaseLinkVisible);
        Assert.False(about.Checking);
        Assert.Empty(opener.Opened);

        _host.Do(() => window.Close());
    }

    [Fact]
    public async Task AnAnswerThatIsNotAReleaseIsReportedAsUnreadable()
    {
        var feed = new StubFeed { Json = "<html><body>a proxy login page</body></html>" };
        LicensesWindow window = NewWindow(feed);

        await _host.Do(() => window.CheckForUpdateAsync());

        Assert.Contains("could not read", Read(window).Status, StringComparison.OrdinalIgnoreCase);

        _host.Do(() => window.Close());
    }

    [Fact]
    public void TwoClicksWhileACheckIsRunningMakeOneRequest()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var feed = new StubFeed
        {
            Json = """{ "tag_name": "v9.9.9" }""",
            WaitBeforeAnswering = _ => started.Task,
        };
        LicensesWindow window = NewWindow(feed);

        ClickLinkButton(window);
        _host.Do(() =>
        {
            Assert.True(window.IsCheckingForUpdate);
            Assert.False(window.CheckUpdateButton.IsEnabled);        // a second click cannot even land
            window.CheckForUpdateAsync();
            Assert.Equal(1, feed.Calls);
        });

        started.SetResult();
        WaitUntilIdle(window);

        AboutState about = Read(window);
        Assert.False(about.Checking);
        Assert.True(_host.Do(() => window.CheckUpdateButton.IsEnabled));
        Assert.Equal(1, feed.Calls);

        _host.Do(() => window.Close());
    }

    [Fact]
    public void ClosingTheWindowCancelsAReleaseCheckThatHasNotAnswered()
    {
        var answered = false;
        var feed = new StubFeed
        {
            Json = """{ "tag_name": "v9.9.9" }""",
            WaitBeforeAnswering = async (cancel) =>
            {
                try { await Task.Delay(Timeout.Infinite, cancel); }
                catch (TaskCanceledException) { answered = true; }   // the check ending because the window closed
            },
        };
        LicensesWindow window = NewWindow(feed);

        _host.Do(() => _ = window.CheckForUpdateAsync());
        Assert.True(Read(window).Checking);

        _host.Do(() => window.Close());
        for (int spin = 0; spin < 200 && Read(window).Checking; spin++)
        {
            _host.Do(() => System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                () => { }, System.Windows.Threading.DispatcherPriority.Background));
        }

        Assert.False(Read(window).Checking);
        Assert.True(answered, "the pending request was not cancelled when its window closed");
        Assert.Equal(1, feed.Calls);
    }

    [Fact]
    public void TheLinksFollowTheInterfaceThemeRatherThanWPFsFixedLinkBlue()
    {
        ThemeChoice before = ThemeManager.LastApplied;
        LicensesWindow window = NewWindow();

        (string Accent, string Link) Under(System.Windows.Application app, ThemeChoice choice) => _host.Do(() =>
        {
            ThemeManager.Apply(app, choice);
            string accent = ((System.Windows.Media.Brush)window.FindResource("AccentBrush")).ToString();
            string link = ((System.Windows.Media.Brush)window.RepositoryLink.Foreground).ToString();
            return (Accent: accent, Link: link);
        });

        (string accentDark, string linkDark) = Under(_host.App, ThemeChoice.Dark);
        (string accentLight, string linkLight) = Under(_host.App, ThemeChoice.Light);

        Assert.Equal(accentDark, linkDark);
        Assert.Equal(accentLight, linkLight);
        Assert.NotEqual(accentDark, accentLight);      // not WPF's fixed link blue, which would ignore both

        _host.Do(() => ThemeManager.Apply(_host.App, before));
        _host.Do(() => window.Close());
    }

    [Fact]
    public void TheAboutTabGainsALinkAndACheckWithoutGainingAnotherTab()
    {
        LicensesWindow window = NewWindow();
        AboutState about = Read(window);

        Assert.Equal(4, about.TabCount);
        Assert.Contains("Nothing is fetched or sent", _host.Do(() => window.AboutOperation), StringComparison.Ordinal);

        _host.Do(() => window.Close());
    }

    private void ClickLinkButton(LicensesWindow window) => _host.Do(() => window.CheckUpdateButton.RaiseEvent(
        new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));
}
