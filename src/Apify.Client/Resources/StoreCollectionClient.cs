using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

    /// <summary>
    /// Lazily iterates over all Store Actors matching the options across pages, fetching each page on
    /// demand.
    /// </summary>
    /// <param name="options">Optional listing filters; <c>Offset</c>/<c>Limit</c> bound where iteration
    /// starts and the total number of Actors yielded, and <c>ChunkSize</c> sets the page size.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<ActorStoreListItem> IterateAsync(StoreListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new StoreListOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        return _ctx.IterateListAsync("", q, options.Offset ?? 0, options.Limit, options.ChunkSize, static d => new ActorStoreListItem(d), RequestTier.Medium, cancellationToken);
    }

    /// <summary>
    /// Returns this client with every subsequent call's timeout set to <paramref name="timeout"/>,
    /// overriding the tier default (see the "Timeout tiers" section of the top-level README). Pass
    /// <see cref="TimeSpan.Zero"/> for no timeout, matching the reference client's <c>'noTimeout'</c>. A
    /// value above <see cref="ApifyClientOptions.TimeoutSecs"/> (the overall budget) is capped at it —
    /// raise <see cref="ApifyClientOptions.TimeoutSecs"/> itself to allow a longer per-call timeout.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public StoreCollectionClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
