using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Apify.Client.Http;

/// <summary>The replaceable transport of the client.</summary>
/// <remarks>
/// <para>
/// An implementation sends one fully prepared <see cref="HttpRequestMessage"/> and returns the raw
/// <see cref="HttpResponseMessage"/>. The client handles authentication, <c>User-Agent</c>, retries and
/// (de)serialization.
/// </para>
/// <para>
/// A non-2xx status is not an error at this layer: return it as a response. Throw only transport failures (connection
/// refused, DNS, timeout), as an <see cref="Apify.Client.Exceptions.ApifyTransportException"/>.
/// </para>
/// <para>
/// A request with <see cref="ApifyRequestOptions.FollowRedirects"/> set to <c>false</c> in
/// <see cref="HttpRequestMessage.Options"/> carries the API token in a header that must not follow a redirect to
/// another host, so return a redirect response as it is. The client sets it on requests to an Actor's server.
/// </para>
/// <para>
/// Replace the default <see cref="HttpClientTransport"/> through <see cref="Apify.Client.ApifyClientOptions.HttpTransport"/>
/// to share a connection pool, configure TLS or a proxy, or mock the API in tests.
/// </para>
/// </remarks>
public interface IHttpTransport
{
    /// <summary>Sends one request with a per-attempt timeout and returns the response.</summary>
    /// <param name="request">The prepared request.</param>
    /// <param name="timeout">The per-attempt timeout.</param>
    /// <param name="streaming">
    /// When <c>true</c>, return as soon as the headers arrive so the body can be read as a live stream; otherwise the
    /// response may be buffered.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        TimeSpan timeout,
        bool streaming,
        CancellationToken cancellationToken);
}
