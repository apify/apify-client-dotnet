using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Apify.Client.Http;

/// <summary>
/// The replaceable transport contract of the client.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are responsible only for sending a single, fully-prepared <see cref="HttpRequestMessage"/>
/// and returning the raw <see cref="HttpResponseMessage"/>. Authentication, the <c>User-Agent</c> header,
/// retries and (de)serialization are handled by the client, so a backend only needs to perform one
/// network round-trip.
/// </para>
/// <para>
/// A non-2xx HTTP status is <b>not</b> an error at this layer — return it as a normal response. Only
/// transport-level failures (connection refused, DNS, timeout) should be thrown, as an
/// <see cref="Apify.Client.Exceptions.ApifyTransportException"/>.
/// </para>
/// <para>
/// Swap the default implementation (<see cref="HttpClientTransport"/>) via
/// <see cref="Apify.Client.ApifyClientOptions.HttpTransport"/> to share a connection pool, customize
/// TLS/proxy settings, or inject a mock in tests.
/// </para>
/// </remarks>
public interface IHttpTransport
{
    /// <summary>
    /// Sends a single request with a per-attempt timeout and returns the response.
    /// </summary>
    /// <param name="request">The fully-prepared request (headers and body already set).</param>
    /// <param name="timeout">The per-attempt timeout budget.</param>
    /// <param name="streaming">
    /// When <c>true</c>, return as soon as the response headers arrive so the body can be consumed as a
    /// live stream (used by log streaming); otherwise the whole response may be buffered.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        TimeSpan timeout,
        bool streaming,
        CancellationToken cancellationToken);
}
