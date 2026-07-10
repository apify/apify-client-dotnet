using System;
using Apify.Client.Http;

namespace Apify.Client;

/// <summary>
/// Configuration for an <see cref="ApifyClient"/>. All fields have sensible defaults; set only the ones
/// you need to override.
/// </summary>
public sealed class ApifyClientOptions
{
    /// <summary>API token, sent as a Bearer token.</summary>
    public string? Token { get; set; }

    /// <summary>API base URL; the <c>/v2</c> suffix is appended automatically.</summary>
    public string BaseUrl { get; set; } = ApifyClient.DefaultBaseUrl;

    /// <summary>Base URL for building public, shareable resource URLs (defaults to <see cref="BaseUrl"/>).</summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Maximum retries for failed requests (default 8).</summary>
    public int MaxRetries { get; set; } = ApifyClient.DefaultMaxRetries;

    /// <summary>Minimum delay between retries in ms (default 500).</summary>
    public int MinDelayBetweenRetriesMillis { get; set; } = ApifyClient.DefaultMinDelayMillis;

    /// <summary>Upper bound for the growing inter-retry delay in ms (defaults to the request timeout).</summary>
    public int? MaxDelayBetweenRetriesMillis { get; set; }

    /// <summary>Overall per-request timeout in seconds (default 360).</summary>
    public int TimeoutSecs { get; set; } = ApifyClient.DefaultTimeoutSecs;

    /// <summary>Custom suffix appended to the <c>User-Agent</c> header.</summary>
    public string? UserAgentSuffix { get; set; }

    /// <summary>
    /// Algorithm used to compress large request bodies (default <see cref="RequestCompression.Brotli"/>).
    /// Set it to <see cref="RequestCompression.Gzip"/> to send gzip-compressed bodies instead.
    /// </summary>
    public RequestCompression RequestCompression { get; set; } = RequestCompression.Brotli;

    /// <summary>Replaces the default transport (<see cref="HttpClientTransport"/>).</summary>
    public IHttpTransport? HttpTransport { get; set; }

    /// <summary>Test seam overriding the <c>isAtHome</c> flag detection.</summary>
    public Func<bool>? IsAtHome { get; set; }
}
