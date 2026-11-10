using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;

namespace Apify.Client.Http;

/// <summary>The default <see cref="IHttpTransport"/>, backed by <see cref="HttpClient"/>.</summary>
/// <remarks>
/// The client applies the per-attempt timeout with a linked cancellation token, so <see cref="HttpClient.Timeout"/> is
/// infinite. Non-2xx statuses are returned as responses; connection and timeout failures are thrown as
/// <see cref="ApifyTransportException"/>.
/// </remarks>
public sealed class HttpClientTransport : IHttpTransport, IDisposable
{
    /// <summary>Connection-establishment timeout.</summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _httpClient;
    private readonly HttpClient? _noRedirectClient;
    private readonly bool _ownsClient;
    private readonly bool _ownsNoRedirectClient;

    /// <summary>
    /// Creates a transport, optionally wrapping caller-supplied <see cref="HttpClient"/>s, for example ones configured with
    /// a proxy or custom TLS. A client that is not supplied is created and disposed with this instance.
    /// </summary>
    /// <remarks>
    /// A request that must not follow redirects (<see cref="ApifyRequestOptions.FollowRedirects"/>, used for requests to an
    /// Actor's server) goes through the no-redirect client. A supplied <paramref name="httpClient"/> cannot be switched to
    /// that mode, so also supply <paramref name="noRedirectHttpClient"/>, configured like it but with automatic redirects
    /// disabled. Without it, such a request throws <see cref="InvalidOperationException"/> rather than follow a redirect
    /// with the API token.
    /// </remarks>
    /// <param name="httpClient">An optional pre-configured HTTP client to use.</param>
    /// <param name="noRedirectHttpClient">
    /// An optional pre-configured HTTP client that does not follow redirects. Required for requests that must not follow
    /// them when <paramref name="httpClient"/> is supplied.
    /// </param>
    public HttpClientTransport(HttpClient? httpClient = null, HttpClient? noRedirectHttpClient = null)
    {
        _httpClient = httpClient ?? CreateOwnedClient(allowAutoRedirect: true);
        _ownsClient = httpClient is null;

        // An owned main client gets a matching no-redirect one; a caller's client has to be given it.
        _noRedirectClient = noRedirectHttpClient ?? (httpClient is null ? CreateOwnedClient(allowAutoRedirect: false) : null);
        _ownsNoRedirectClient = noRedirectHttpClient is null && httpClient is null;
    }

    private static HttpClient CreateOwnedClient(bool allowAutoRedirect)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = ConnectTimeout,
            AllowAutoRedirect = allowAutoRedirect,
            // The API compresses responses of the dataset-items and record endpoints. Automatic decompression sends the matching
            // `Accept-Encoding` and inflates the response, like the reference client. .NET has no zstd decompression, so zstd is not advertised.
            AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        // The client's retry loop owns the per-request timeout.
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
