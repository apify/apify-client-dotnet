using System;
using System.Collections.Generic;
using Apify.Client.Http;

namespace Apify.Client;

/// <summary>Configuration of an <see cref="ApifyClient"/>. Set only the fields you need to override.</summary>
public sealed class ApifyClientOptions
{
    /// <summary>API token, sent as a Bearer token.</summary>
    public string? Token { get; set; }

    /// <summary>
    /// API base URL, with or without the <c>/v2</c> path (it is appended when missing), so <c>https://api.apify.com</c> and
    /// <c>https://api.apify.com/v2</c> give the same <see cref="ApifyClient.ApiBaseUrl"/>.
    /// </summary>
    public string BaseUrl { get; set; } = ApifyClient.DefaultBaseUrl;

    /// <summary>
    /// Base URL of public, shareable resource URLs, with or without <c>/v2</c>. Defaults to
    /// <see cref="ApifyClient.DefaultBaseUrl"/> whatever <see cref="BaseUrl"/> is, as in the reference client, so a client
    /// behind an internal <see cref="BaseUrl"/> does not hand out internal links.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Maximum retries for failed requests (default 8).</summary>
    public int MaxRetries { get; set; } = ApifyClient.DefaultMaxRetries;

    /// <summary>Minimum delay between retries in ms (default 500).</summary>
    public int MinDelayBetweenRetriesMillis { get; set; } = ApifyClient.DefaultMinDelayMillis;

    /// <summary>Upper bound for the growing inter-retry delay in ms (defaults to the request timeout).</summary>
    public int? MaxDelayBetweenRetriesMillis { get; set; }

    /// <summary>
    /// The overall per-request cap in seconds (default 360), as the reference client's <c>timeoutMaxSecs</c>: every
    /// attempt's timeout, including one set with <c>WithTimeout</c>, is clamped to it. It is also the base of the
    /// <c>Long</c> tier unless <see cref="TimeoutLongSecs"/> is set.
    /// </summary>
    public int TimeoutSecs { get; set; } = ApifyClient.DefaultTimeoutSecs;

    /// <summary>
    /// Base duration of the <c>Long</c> timeout tier in seconds: downloads, uploads, and streaming. Defaults to
    /// <see cref="TimeoutSecs"/>, which still caps it.
    /// </summary>
    public int? TimeoutLongSecs { get; set; }

    /// <summary>Base duration of the <c>Short</c> timeout tier in seconds (default 5): metadata reads and writes.</summary>
    public int TimeoutShortSecs { get; set; } = ApifyClient.DefaultTimeoutShortSecs;

    /// <summary>Base duration of the <c>Medium</c> timeout tier in seconds (default 30): listing, batch, and trigger calls.</summary>
    public int TimeoutMediumSecs { get; set; } = ApifyClient.DefaultTimeoutMediumSecs;

    /// <summary>Custom suffix appended to the <c>User-Agent</c> header.</summary>
    public string? UserAgentSuffix { get; set; }

    /// <summary>
    /// Algorithm used to compress large request bodies (default <see cref="RequestCompression.Brotli"/>) with
    /// its default quality. Use <see cref="Compressor"/> to tune the quality or plug in another algorithm.
    /// </summary>
    public RequestCompression RequestCompression { get; set; } = RequestCompression.Brotli;

    /// <summary>
    /// A custom request body compressor, for another quality (<see cref="BrotliHttpCompressor"/>,
    /// <see cref="GzipHttpCompressor"/>, <see cref="ZstdHttpCompressor"/>) or another algorithm (derive from
    /// <see cref="HttpCompressor"/>). It takes precedence over <see cref="RequestCompression"/>. Only large bodies without
    /// their own compression are compressed.
    /// </summary>
    public HttpCompressor? Compressor { get; set; }

    /// <summary>
    /// Additional headers sent with every request, such as an integration marker. They win over the built-in
    /// <c>User-Agent</c> and <c>Authorization</c>; names are case-insensitive. A content header such as <c>Content-Type</c>
    /// makes the <see cref="ApifyClient"/> constructor throw <see cref="ArgumentException"/>, because the client sets
    /// those per request.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }

    /// <summary>Replaces the default transport (<see cref="HttpClientTransport"/>).</summary>
    public IHttpTransport? HttpTransport { get; set; }

    /// <summary>Test seam overriding the <c>isAtHome</c> flag detection.</summary>
    public Func<bool>? IsAtHome { get; set; }
}
