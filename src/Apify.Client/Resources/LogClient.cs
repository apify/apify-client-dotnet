using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Internal;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>
/// A client for accessing the log of an Actor build or run (<c>/v2/logs/{buildOrRunId}</c>, or the
/// run/build-nested <c>.../log</c>).
/// </summary>
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
        => new(http, ResourceContext.Single(http, baseUrl, "logs", id), ambiguousNotFound: false);

    /// <summary>
    /// Creates a log client nested under a run or build that has no id of its own (<c>run.Log()</c>,
    /// <c>build.Log()</c>). A 404 here is ambiguous — it could mean the run/build itself or its log is
    /// missing — so <see cref="GetAsync"/> and <see cref="StreamAsync"/> throw instead of resolving
    /// <c>null</c>, matching the reference client's <c>catchNotFoundForResourceOrThrow()</c>.
    /// </summary>
    internal static LogClient Nested(HttpClientCore http, string baseUrl, QueryParams? inheritedParams = null)
        => new(http, ResourceContext.Collection(http, baseUrl, "log", inheritedParams), ambiguousNotFound: true);

    /// <summary>
    /// Fetches the log as text. For a log addressed by its own id (<see cref="ApifyClient.Log"/>), a 404
    /// resolves to <c>null</c>; for a log nested under a run/build with no id of its own
    /// (<c>run.Log()</c>/<c>build.Log()</c>), a 404 is ambiguous (the run/build itself may be what is
    /// missing) and throws an <see cref="ApifyApiException"/> instead.
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
    /// Opens a live, streaming connection to the log and returns a stream over the log bytes. For a log
    /// addressed by its own id, a 404 resolves to <c>null</c> (matching <see cref="GetAsync"/>); for a log
    /// nested under a run/build with no id of its own, a 404 is ambiguous and throws instead.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="GetAsync"/>, this bypasses the buffered/retrying transport so the log can be
    /// followed in real time as the run produces it (the <c>stream=1</c> query parameter). Because the
    /// response is consumed incrementally, it is not retried. The caller must dispose the returned stream.
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
}
