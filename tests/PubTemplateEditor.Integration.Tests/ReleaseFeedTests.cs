using System;
using PubTemplateEditor.Infrastructure;
using Xunit;

namespace PubTemplateEditor.Integration.Tests;

/// <summary>
/// The release-check transport's own rules: where the one request goes, and that an address which is not
/// this project's GitHub page is refused rather than fetched. What an answer means is covered by the Core
/// tests over the parsed release document; nothing here touches a network.
/// </summary>
public class ReleaseFeedTests
{
    [Theory]
    [InlineData("https://github.com/SKajto/PUB-Template-Parameter-Editor",
                "https://api.github.com/repos/SKajto/PUB-Template-Parameter-Editor/releases/latest")]
    [InlineData("https://github.com/SKajto/PUB-Template-Parameter-Editor/",
                "https://api.github.com/repos/SKajto/PUB-Template-Parameter-Editor/releases/latest")]
    public void TheProjectPageBecomesItsReleaseEndpoint(string projectUrl, string expectedApi) =>
        Assert.Equal(expectedApi, GitHubReleaseFeed.ApiEndpointOf(projectUrl)?.ToString());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.invalid/SKajto/Repository")]        // not GitHub
    [InlineData("https://github.com/SKajto/Repo/releases")]          // not a project page
    [InlineData("https://github.com/SKajto")]                        // no repository part
    [InlineData("not a url")]
    public void AnAddressThatIsNotThisProjectsGitHubPageIsRefused(string? projectUrl) =>
        Assert.Null(GitHubReleaseFeed.ApiEndpointOf(projectUrl));

    [Fact]
    public void AFeedConstructedFromAnUnusableAddressIsRefusedAtConstruction() =>
        Assert.Throws<ArgumentException>(() => new GitHubReleaseFeed("https://example.invalid/x/y", "1.0.0"));

    [Fact]
    public void ARefusalIsAReasonRatherThanAnExceptionSomebodyHasToRead()
    {
        ReleaseFeedResult refused = ReleaseFeedResult.Refused("the release service did not answer within 10 seconds.");

        Assert.False(refused.Succeeded);
        Assert.Null(refused.Json);
        Assert.Contains("did not answer", refused.Reason, StringComparison.Ordinal);
    }
}
