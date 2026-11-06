using System.Net.Http;

namespace Apify.Client.Http;

/// <summary>
/// Keys of the per-request settings the client passes to an <see cref="IHttpTransport"/> in
/// <see cref="HttpRequestMessage.Options"/>.
/// </summary>
/// <remarks>
/// A custom transport has to honour them, or it can send the API token somewhere it must not go. The built-in
/// <see cref="HttpClientTransport"/> does.
/// </remarks>
public static class ApifyRequestOptions
{
    /// <summary>
    /// Set to <c>false</c> on a request that must not follow redirects: the transport returns a redirect
    /// response as it is. The client sets it on requests to an Actor's server (see
    /// <see cref="Resources.ActorClient.Server"/>), because they carry the API token in a header that an
    /// automatic redirect would send to a host the Actor's code picked. A request without the option follows
    /// redirects as usual.
    /// </summary>
    public static readonly HttpRequestOptionsKey<bool> FollowRedirects = new("Apify.FollowRedirects");
}
