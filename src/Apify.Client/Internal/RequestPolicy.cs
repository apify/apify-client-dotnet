using System.Collections.Generic;
using Apify.Client.Http;

namespace Apify.Client.Internal;

/// <summary>
/// Per-request overrides of the shared HTTP behavior for calls that are not plain API calls. The default is the
/// behavior of an API call; <see cref="Resources.ActorServerClient"/> overrides it to call an Actor's server, as the
/// reference client's per-request <c>ApifyRequestConfig</c> does.
/// </summary>
internal sealed class RequestPolicy
{
    /// <summary>The behaviour of an ordinary API call.</summary>
    public static readonly RequestPolicy Default = new();

    /// <summary>
    /// The header that carries the token, or <c>null</c> for <c>Authorization</c>. Actor servers accept
    /// <c>X-Apify-Authorization</c>, which the platform removes, so <c>Authorization</c> stays free for the Actor's own authentication.
    /// </summary>
    public string? AuthorizationHeader { get; init; }

    /// <summary>
    /// Whether an error status throws <see cref="Exceptions.ApifyApiException"/>. When <c>false</c>, the error response is
    /// returned once no retry follows, and a 429 is not counted in the client's statistics of the API's rate limits.
    /// </summary>
    public bool ThrowOnErrorStatus { get; init; } = true;

    /// <summary>The statuses to retry, or <c>null</c> for the default of rate limits (429) and server errors (5xx).</summary>
    public IReadOnlyCollection<int>? RetryStatusCodes { get; init; }

    /// <summary>
    /// Whether transport errors, timeouts included, are retried. When <c>false</c>, the first one fails the call, since the
    /// request may have reached the server.
    /// </summary>
    public bool RetryTransportErrors { get; init; } = true;

    /// <summary>
    /// Whether the transport follows redirects. When <c>false</c>, a redirect is returned like an error status (see
    /// <see cref="ApifyRequestOptions.FollowRedirects"/>), so the token never goes to a host the server picked.
    /// </summary>
    public bool FollowRedirects { get; init; } = true;

    /// <summary>Whether a large request body may be compressed at all.</summary>
    public bool CompressBody { get; init; } = true;

    /// <summary>The compressor for this request's body, or <c>null</c> for the client's own one.</summary>
    public HttpCompressor? Compressor { get; init; }

    /// <summary>Whether the response body is returned unread, to be consumed as a stream.</summary>
    public bool StreamResponse { get; init; }
}
