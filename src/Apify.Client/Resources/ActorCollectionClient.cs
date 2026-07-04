using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for the Actor collection (<c>GET/POST /v2/actors</c>).</summary>
public sealed class ActorCollectionClient
{
    private readonly ResourceContext _ctx;

    internal ActorCollectionClient(HttpClientCore http, string baseUrl)
    {
        _ctx = ResourceContext.Collection(http, baseUrl, "actors");
    }

    /// <summary>Lists the account's Actors.</summary>
    /// <param name="options">Optional listing filters and pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<Actor>> ListAsync(ActorListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new ActorListOptions()).AppendTo(q);
        return _ctx.ListResourceAsync("", q, static d => new Actor(d), cancellationToken);
    }

    /// <summary>
    /// Lazily iterates over all of the account's Actors across pages, fetching each page on demand.
    /// </summary>
    /// <param name="options">Optional listing filters; <c>Offset</c>/<c>Limit</c> bound where iteration
    /// starts and the total number of Actors yielded.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<Actor> IterateAsync(ActorListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ActorListOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        return _ctx.IterateListAsync("", q, options.Offset ?? 0, options.Limit, static d => new Actor(d), cancellationToken);
    }

    /// <summary>Creates a new Actor.</summary>
    /// <param name="actor">Any JSON-serializable Actor definition.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Actor> CreateAsync(object actor, CancellationToken cancellationToken = default)
    {
        return new Actor(await _ctx.CreateResourceAsync(new QueryParams(), actor, cancellationToken).ConfigureAwait(false));
    }
}
