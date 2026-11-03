using PubTemplateEditor.Core.Updates;
using Xunit;

namespace PubTemplateEditor.Core.Tests;

/// <summary>
/// What a release answer means. The answer is untrusted text from a service, so an answer that cannot be
/// read has to stay unreadable rather than become a guessed version, and the comparison is numbers only:
/// deciding to update is the user's, and nothing here downloads anything.
/// </summary>
public class ReleaseInfoTests
{
    private const string NewestRelease = """
        {
          "tag_name": "v1.0.2",
          "name": "PUB Template Parameter Editor 1.0.2",
          "html_url": "https://github.com/SKajto/PUB-Template-Parameter-Editor/releases/tag/v1.0.2",
          "prerelease": false,
          "draft": false,
          "assets": [ { "name": "PUB_Template_Parameter_Editor.exe", "browser_download_url": "https://example.invalid/x" } ]
        }
        """;

    [Fact]
    public void APublishedReleaseIsReadWithoutCareAboutTheRestOfTheDocument()
    {
        Assert.True(ReleaseInfo.TryParse(NewestRelease, out ReleaseInfo? release));
        Assert.Equal("v1.0.2", release!.Tag);
        Assert.EndsWith("/releases/tag/v1.0.2", release.Url, StringComparison.Ordinal);
        Assert.False(release.IsPreRelease);
        Assert.Equal(new Version(1, 0, 2), release.ParsedVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("""{ "html_url": "https://github.com/x/y/releases/tag/v2" }""")]      // no tag
    [InlineData("""{ "tag_name": null }""")]
    [InlineData("""[1,2,3]""")]                                                       // not an object
    public void AnAnswerThatIsNotAReleaseStaysUnreadable(string? json) =>
        Assert.False(ReleaseInfo.TryParse(json, out ReleaseInfo? release) && release is not null);

    [Theory]
    [InlineData("v1.0.2", 1, 0, 2, -1)]
    [InlineData("V1.0.2", 1, 0, 2, -1)]
    [InlineData("  v1.0.2  ", 1, 0, 2, -1)]
    [InlineData("1.0.2.0", 1, 0, 2, 0)]           // a four-part tag is still the same release number
    public void AReleaseTagNameIsAVersion(string tag, int major, int minor, int build, int revision)
    {
        Assert.True(ReleaseInfo.TryParseVersion(tag, out Version parsed));
        Assert.Equal(major, parsed.Major);
        Assert.Equal(minor, parsed.Minor);
        Assert.Equal(build, parsed.Build);
        Assert.Equal(revision, parsed.Revision);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v1.0.2-rc1")]
    [InlineData("2.0")]                            // a release number needs major.minor.build, not two parts
    public void AnythingElseIsNotClaimedToBeAVersion(string? tag) =>
        Assert.False(ReleaseInfo.TryParseVersion(tag, out _));

    [Fact]
    public void AVersionCarryingItsSourceRevisionIsStillComparable()
    {
        ReleaseInfo.TryParse(NewestRelease, out ReleaseInfo? published);   // tag v1.0.2

        // A build can report "1.0.0+<commit>"; the metadata says which commit produced the binary and is
        // not part of the number anybody compares, so it must not turn the check into "unreadable".
        Assert.Equal(UpdateState.NewerAvailable, UpdateDecision.Compare("1.0.0+46985167eb3ab", published).State);
        Assert.Equal(UpdateState.UpToDate, UpdateDecision.Compare("1.0.2+46985167eb3ab", published).State);
    }

    [Fact]
    public void ANewerPublishedReleaseIsReportedAsNewer()
    {
        ReleaseInfo.TryParse(NewestRelease, out ReleaseInfo? published);

        UpdateCheckResult result = UpdateDecision.Compare("1.0.1", published);

        Assert.Equal(UpdateState.NewerAvailable, result.State);
        Assert.Equal("v1.0.2", result.NewestTag);
        Assert.False(result.IsPreRelease);
    }

    [Fact]
    public void TheNewestPublishedReleaseIsReportedAsUpToDate()
    {
        ReleaseInfo.TryParse(NewestRelease, out ReleaseInfo? published);

        Assert.Equal(UpdateState.UpToDate, UpdateDecision.Compare("1.0.2", published).State);
        Assert.Equal(UpdateState.UpToDate, UpdateDecision.Compare("1.0.3", published).State);   // a build newer than any published release
    }

    [Fact]
    public void APreReleaseIsStillFlaggedAsOne()
    {
        ReleaseInfo.TryParse("""{ "tag_name": "v2.0.0", "prerelease": true }""", out ReleaseInfo? published);

        UpdateCheckResult result = UpdateDecision.Compare("1.0.1", published);

        Assert.Equal(UpdateState.NewerAvailable, result.State);
        Assert.True(result.IsPreRelease);
    }

    [Fact]
    public void AReleaseTagWithoutAVersionIsNotInventedIntoOne()
    {
        ReleaseInfo.TryParse("""{ "tag_name": "current", "html_url": "https://example.invalid" }""", out ReleaseInfo? published);

        UpdateCheckResult result = UpdateDecision.Compare("1.0.1", published);

        Assert.Equal(UpdateState.UnknownVersion, result.State);
        Assert.Equal("current", result.NewestTag);
    }

    [Fact]
    public void NoAnswerAtAllIsNotSilentlyTreatedAsUpToDate()
    {
        UpdateCheckResult result = UpdateDecision.Compare("1.0.1", published: null);

        Assert.Equal(UpdateState.UnreadableAnswer, result.State);
        Assert.Null(result.Url);
    }

    [Fact]
    public void ARunningVersionThatIsNotAVersionSaysSoRatherThanComparingNothing()
    {
        ReleaseInfo.TryParse(NewestRelease, out ReleaseInfo? published);

        Assert.Equal(UpdateState.UnknownVersion, UpdateDecision.Compare(null, published).State);
        Assert.Equal(UpdateState.UnknownVersion, UpdateDecision.Compare("nightly", published).State);
    }
}
