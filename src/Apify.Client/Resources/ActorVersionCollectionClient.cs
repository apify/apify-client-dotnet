using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for an Actor's version collection (<c>GET/POST /v2/actors/{actorId}/versions</c>).</summary>
public sealed class ActorVersionCollectionClient
{
    private readonly ResourceContext _ctx;

    internal ActorVersionCollectionClient(HttpClientCore http, string actorUrl)
    {
        _ctx = ResourceContext.Collection(http, actorUrl, "versions");
    }

    /// <summary>Lists the Actor's versions.</summary>
    /// <param name="options">Optional pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<ActorVersion>> ListAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new ListOptions()).AppendTo(q);
        return _ctx.ListResourceAsync("", q, static d => new ActorVersion(d), cancellationToken);
    }

    /// <summary>Creates a new Actor version.</summary>
    /// <param name="version">Any JSON-serializable version definition.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorVersion> CreateAsync(object version, CancellationToken cancellationToken = default)
    {
        return new ActorVersion(await _ctx.CreateResourceAsync(new QueryParams(), version, cancellationToken).ConfigureAwait(false));
    }
}
