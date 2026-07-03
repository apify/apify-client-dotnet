using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

    private LogClient(HttpClientCore http, ResourceContext ctx)
    {
        _http = http;
        _ctx = ctx;
    }

    internal static LogClient ForId(HttpClientCore http, string baseUrl, string id)
        => new(http, ResourceContext.Single(http, baseUrl, "logs", id));

    internal static LogClient Nested(HttpClientCore http, string baseUrl)
        => new(http, ResourceContext.Collection(http, baseUrl, "log"));

    /// <summary>Fetches the log as text, or <c>null</c> if the log does not exist.</summary>
    /// <param name="options">Optional log-content options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<string?> GetAsync(LogOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new LogOptions()).AppendTo(q);
        return _ctx.GetRawAsync("", q, cancellationToken);
    }

    /// <summary>
    /// Opens a live, streaming connection to the log and returns a stream over the log bytes.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="GetAsync"/>, this bypasses the buffered/retrying transport so the log can be
    /// followed in real time as the run produces it (the <c>stream=1</c> query parameter). Because the
    /// response is consumed incrementally, it is not retried. The caller must dispose the returned stream.
    /// </remarks>
    /// <param name="options">Optional log-content options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Stream> StreamAsync(LogOptions? options = null, CancellationToken cancellationToken = default)
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
            throw HttpClientCore.BuildApiError(status, body, 1, "GET", HttpClientCore.ExtractPath(url));
        }

        return await ResponseOwningStream.CreateAsync(response, cancellationToken).ConfigureAwait(false);
    }
}
