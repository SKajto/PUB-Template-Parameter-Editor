using System.Net.Http;
using System.Net.Http.Headers;

namespace PubTemplateEditor.Infrastructure;

/// <summary>The outcome of asking a release service for its newest release.</summary>
public sealed record ReleaseFeedResult(bool Succeeded, string? Json, string? Reason)
{
    public static ReleaseFeedResult Received(string json) => new(true, json, null);
    public static ReleaseFeedResult Refused(string reason) => new(false, null, reason);
}

/// <summary>
/// Asks where the newest published release is. Behind an interface because the transport is the part
/// that cannot be tested meaningfully, while the decision built on its answer can.
/// </summary>
public interface IReleaseFeed
{
    /// <summary>One request per call. Never sends anything about the user or their templates.</summary>
    Task<ReleaseFeedResult> FetchLatestReleaseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// GitHub's release listing for this project's own public repository.
///
/// Deliberately narrow, because the application's promise is that it does not talk to anybody unless
/// the user asks: one anonymous GET for a version number, no request body, no cookies, no identifiers
/// beyond the user agent that says which program is asking, and nothing is downloaded from the answer.
/// A failure is a plain reason string rather than an exception, so the caller can say what happened
/// without turning a blocked port or a corporate proxy into a stack trace.
/// </summary>
public sealed class GitHubReleaseFeed : IReleaseFeed
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly TimeSpan _timeout;

    /// <param name="repositoryUrl">The public GitHub project page, as published in ProductInfo.</param>
    /// <param name="appVersion">Sent only in the user agent, so a server can tell which program is asking.</param>
    public GitHubReleaseFeed(string repositoryUrl, string appVersion, TimeSpan? timeout = null, HttpMessageHandler? handler = null)
    {
        _endpoint = ApiEndpointOf(repositoryUrl) ?? throw new ArgumentException("Not a GitHub project address.", nameof(repositoryUrl));
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("PubTemplateParameterEditor", SanitizeToken(appVersion)));
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    /// <summary>The API address of a GitHub project page, or null when the text is not one.</summary>
    public static Uri? ApiEndpointOf(string? repositoryUrl)
    {
        if (Uri.TryCreate(repositoryUrl, UriKind.Absolute, out Uri? parsed)
            && string.Equals(parsed.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && parsed.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) is string[] segments
            && segments.Length == 2)
        {
            return new Uri($"https://api.github.com/repos/{segments[0]}/{segments[1]}/releases/latest");
        }

        return null;
    }

    public async Task<ReleaseFeedResult> FetchLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        try
        {
            using HttpResponseMessage response = await _http.GetAsync(_endpoint, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return ReleaseFeedResult.Refused($"the release service answered {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            string body = await response.Content.ReadAsStringAsync(timeout.Token);
            return string.IsNullOrWhiteSpace(body)
                ? ReleaseFeedResult.Refused("the release service answered with an empty reply.")
                : ReleaseFeedResult.Received(body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ReleaseFeedResult.Refused("the release check was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return ReleaseFeedResult.Refused($"the release service did not answer within {(int)_timeout.TotalSeconds} seconds.");
        }
        catch (HttpRequestException ex)
        {
            return ReleaseFeedResult.Refused($"the release service could not be reached ({ex.Message})");
        }
        catch (Exception ex) when (ex is TaskCanceledException or InvalidOperationException)
        {
            return ReleaseFeedResult.Refused($"the release check failed ({ex.GetType().Name})");
        }
    }

    /// <summary>A user agent token may not contain separators; a version that does is reduced, not forwarded.</summary>
    private static string SanitizeToken(string value)
    {
        char[] kept = (value ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && !"(),/:;@[]=<>?\"\\\t".Contains(c)).ToArray();
        return kept.Length == 0 ? "0.0.0" : new string(kept);
    }
}
