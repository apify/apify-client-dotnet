using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for browsing the Apify Store (<c>GET /v2/store</c>).</summary>
public sealed class StoreCollectionClient
{
    private readonly ResourceContext _ctx;

    internal StoreCollectionClient(HttpClientCore http, string baseUrl)
    {
        _ctx = ResourceContext.Collection(http, baseUrl, "store");
    }

    /// <summary>Returns a single page of Store Actors matching the options.</summary>
    /// <param name="options">Optional listing filters and pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<ActorStoreListItem>> ListAsync(StoreListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new StoreListOptions()).AppendTo(q);
        return _ctx.ListResourceAsync("", q, static d => new ActorStoreListItem(d), RequestTier.Medium, cancellationToken);
    }

    /// <summary>Lazily iterates the Store Actors matching the options across pages, fetching each page on demand.</summary>
    /// <param name="options">
    /// Optional listing filters. <c>Offset</c> and <c>Limit</c> bound where iteration starts and the total number of Actors,
    /// and <c>ChunkSize</c> sets the page size (at most 10 with <c>IncludeInputSchema</c>).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<ActorStoreListItem> IterateAsync(StoreListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new StoreListOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        return _ctx.IterateListAsync("", q, options.Offset ?? 0, options.Limit, options.IterationChunkSize(), static d => new ActorStoreListItem(d), RequestTier.Medium, cancellationToken);
    }

    /// <summary>
    /// Returns this client with the timeout of every subsequent call set to <paramref name="timeout"/>, overriding the
    /// tier default (see "Timeout tiers" in docs/README.md). <see cref="TimeSpan.Zero"/> means no timeout. A value above
    /// <see cref="ApifyClientOptions.TimeoutSecs"/> is capped at it.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public StoreCollectionClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
