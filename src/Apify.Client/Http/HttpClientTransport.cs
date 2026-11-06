using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;

namespace Apify.Client.Http;

/// <summary>
/// The default <see cref="IHttpTransport"/>, backed by <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// The per-attempt timeout is applied to each request by the orchestrating client via a linked
/// cancellation token, so the shared <see cref="HttpClient.Timeout"/> is left infinite. Non-2xx statuses
/// are returned as normal responses; only connection/timeout failures are thrown as
/// <see cref="ApifyTransportException"/>.
/// </remarks>
public sealed class HttpClientTransport : IHttpTransport, IDisposable
{
    /// <summary>Connection-establishment timeout (distinct from the per-request timeout the client applies).</summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _httpClient;
    private readonly HttpClient? _noRedirectClient;
    private readonly bool _ownsClient;
    private readonly bool _ownsNoRedirectClient;

    /// <summary>
    /// Creates a transport, optionally wrapping caller-supplied <see cref="HttpClient"/>s (e.g. ones
    /// configured with a proxy or custom TLS). For any client that is not supplied, an internal one is created
    /// and disposed with this instance.
    /// </summary>
    /// <remarks>
    /// A request that must not follow redirects (see <see cref="ApifyRequestOptions.FollowRedirects"/>, used for
    /// requests to an Actor's server) is sent through the no-redirect client. A client supplied as
    /// <paramref name="httpClient"/> cannot be switched to that mode, so supply a second one with automatic
    /// redirects disabled as <paramref name="noRedirectHttpClient"/>, configured like the first one otherwise;
    /// without it, such a request throws <see cref="InvalidOperationException"/> rather than risk following a
    /// redirect with the API token.
    /// </remarks>
    /// <param name="httpClient">An optional pre-configured HTTP client to use.</param>
    /// <param name="noRedirectHttpClient">
    /// An optional pre-configured HTTP client whose handler does not follow redirects, used for requests that
    /// must not follow them. Required for those requests when <paramref name="httpClient"/> is supplied.
    /// </param>
    public HttpClientTransport(HttpClient? httpClient = null, HttpClient? noRedirectHttpClient = null)
    {
        _httpClient = httpClient ?? CreateOwnedClient(allowAutoRedirect: true);
        _ownsClient = httpClient is null;

        // A transport that owns its main client can create the matching no-redirect one; one wrapping a
        // caller's client has to be given it.
        _noRedirectClient = noRedirectHttpClient ?? (httpClient is null ? CreateOwnedClient(allowAutoRedirect: false) : null);
        _ownsNoRedirectClient = noRedirectHttpClient is null && httpClient is null;
    }

    private static HttpClient CreateOwnedClient(bool allowAutoRedirect)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = ConnectTimeout,
            AllowAutoRedirect = allowAutoRedirect,
            // The API advertises brotli/gzip/deflate response compression on the dataset-items and
            // key-value-store record endpoints. Enabling automatic decompression makes the handler send
            // the matching `Accept-Encoding` request header and transparently inflate the response, so
            // compressed payloads are handled the same way as in the reference JS client.
            AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        // The client-side retry orchestrator owns the per-request timeout, so disable HttpClient's own.
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private HttpClient ClientFor(HttpRequestMessage request)
    {
        if (!request.Options.TryGetValue(ApifyRequestOptions.FollowRedirects, out var followRedirects) || followRedirects)
        {
            return _httpClient;
        }

        return _noRedirectClient ?? throw new InvalidOperationException(
            "The request must not follow redirects, but this HttpClientTransport wraps an HttpClient it cannot switch to that mode. "
            + "Pass a second HttpClient with automatic redirects disabled as the noRedirectHttpClient argument.");
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        TimeSpan timeout,
        bool streaming,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout > TimeSpan.Zero)
        {
            timeoutCts.CancelAfter(timeout);
        }

        var completion = streaming
            ? HttpCompletionOption.ResponseHeadersRead
            : HttpCompletionOption.ResponseContentRead;

        try
        {
            return await ClientFor(request).SendAsync(request, completion, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller cancelled: propagate rather than treating it as a retryable timeout.
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // The per-attempt deadline elapsed (only timeoutCts fired).
            throw new ApifyTransportException("the request timed out", ex, isTimeout: true);
        }
        catch (HttpRequestException ex)
        {
            throw new ApifyTransportException(ex.Message, ex, isTimeout: false);
        }
    }

    /// <summary>Disposes the internally-created <see cref="HttpClient"/>s (supplied ones are left alone).</summary>
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }

        if (_ownsNoRedirectClient)
        {
            _noRedirectClient?.Dispose();
        }
    }
}
