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
    private readonly bool _ownsClient;

    /// <summary>
    /// Creates a transport, optionally wrapping a caller-supplied <see cref="HttpClient"/> (e.g. one
    /// configured with a proxy or custom TLS). When none is given, an internal client is created and
    /// disposed with this instance.
    /// </summary>
    /// <param name="httpClient">An optional pre-configured HTTP client to use.</param>
    public HttpClientTransport(HttpClient? httpClient = null)
    {
        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsClient = false;
        }
        else
        {
            var handler = new SocketsHttpHandler
            {
                ConnectTimeout = ConnectTimeout,
                AllowAutoRedirect = true,
                // The API advertises brotli/gzip/deflate response compression on the dataset-items and
                // key-value-store record endpoints. Enabling automatic decompression makes the handler send
                // the matching `Accept-Encoding` request header and transparently inflate the response, so
                // compressed payloads are handled the same way as in the reference JS client.
                AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };
            // The client-side retry orchestrator owns the per-request timeout, so disable HttpClient's own.
            _httpClient = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            _ownsClient = true;
        }
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
            return await _httpClient.SendAsync(request, completion, timeoutCts.Token).ConfigureAwait(false);
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

    /// <summary>Disposes the internally-created <see cref="HttpClient"/> (a supplied one is left alone).</summary>
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
