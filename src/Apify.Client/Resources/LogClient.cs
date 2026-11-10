using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Internal;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for the log of an Actor build or run.</summary>
public sealed class LogClient
{
    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;
    private readonly bool _ambiguousNotFound;

    private LogClient(HttpClientCore http, ResourceContext ctx, bool ambiguousNotFound)
    {
        _http = http;
        _ctx = ctx;
        _ambiguousNotFound = ambiguousNotFound;
    }

    internal static LogClient ForId(HttpClientCore http, string baseUrl, string id)
        => new(http, ResourceContext.Single(http, baseUrl, "logs", id, "buildOrRunId"), ambiguousNotFound: false);

    /// <summary>
    /// Creates a log client nested under a run or build (<c>run.Log()</c>, <c>build.Log()</c>). A 404 is ambiguous there,
    /// since the run or build may be missing, so <see cref="GetAsync"/> and <see cref="StreamAsync"/> throw instead of
    /// returning <c>null</c>, as in the reference client.
    /// </summary>
    internal static LogClient Nested(HttpClientCore http, string baseUrl, QueryParams? inheritedParams = null, TimeSpan? timeout = null)
        => new(http, ResourceContext.Collection(http, baseUrl, "log", inheritedParams).WithTimeout(timeout), ambiguousNotFound: true);

    /// <summary>
    /// Fetches the log as text. A log addressed by its own ID (<see cref="ApifyClient.Log"/>) resolves a 404 to
    /// <c>null</c>; a nested log throws an <see cref="ApifyApiException"/>.
    /// </summary>
    /// <param name="options">Optional log-content options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<string?> GetAsync(LogOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new LogOptions()).AppendTo(q);
        return _ambiguousNotFound
            ? GetRequiredAsync(q, cancellationToken)
            : _ctx.GetRawAsync("", q, RequestTier.Long, cancellationToken);
    }

    private async Task<string?> GetRequiredAsync(QueryParams q, CancellationToken cancellationToken)
        => await _ctx.GetRawRequiredAsync("", q, RequestTier.Long, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Fetches the log as raw bytes, for a log that may not be valid UTF-8. The whole log is downloaded in one response;
    /// use <see cref="StreamAsync"/> to read it incrementally.
    /// </summary>
    /// <param name="options">Optional log-content options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<byte[]?> GetBytesAsync(LogOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new LogOptions()).AppendTo(q);
        return _ambiguousNotFound
            ? GetRequiredBytesAsync(q, cancellationToken)
            : _ctx.GetRawBytesAsync("", q, RequestTier.Long, cancellationToken);
    }

    private async Task<byte[]?> GetRequiredBytesAsync(QueryParams q, CancellationToken cancellationToken)
        => await _ctx.GetRawBytesRequiredAsync("", q, RequestTier.Long, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Opens a live stream of the log bytes. A log addressed by its own ID resolves a 404 to <c>null</c>, like
    /// <see cref="GetAsync"/>; a nested log throws.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="GetAsync"/>, this bypasses the buffered, retrying transport (<c>stream=1</c>) to follow the log
    /// in real time, so it is not retried. Dispose the returned stream.
    /// </remarks>
    /// <param name="options">Optional log-content options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Stream?> StreamAsync(LogOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddBool("stream", true);
        (options ?? new LogOptions()).AppendTo(q);
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl(""));

        var response = await _http.StreamAsync(url, cancellationToken).ConfigureAwait(false);
        var status = (int)response.StatusCode;
        if (status >= HttpClientCore.MaxSuccessStatus)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            response.Dispose();
            var error = HttpClientCore.BuildApiError(status, body, 1, "GET", HttpClientCore.ExtractPath(url));
            if (!_ambiguousNotFound && HttpClientCore.IsNotFound(error))
            {
                return null;
            }

            throw error;
        }

        return await ResponseOwningStream.CreateAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns this client with the timeout of every subsequent call set to <paramref name="timeout"/>, overriding the
    /// tier default (see "Timeout tiers" in docs/README.md). <see cref="TimeSpan.Zero"/> means no timeout. A value above
    /// <see cref="ApifyClientOptions.TimeoutSecs"/> is capped at it.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public LogClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
