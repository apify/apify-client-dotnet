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
    /// Lazily iterates over all Store Actors matching the options, fetching pages on demand. The page size
    /// is the options' <c>ChunkSize</c>, falling back to <c>Limit</c> (which here is the per-page size, not
    /// a total cap), falling back to the API default.
    /// </summary>
    /// <param name="options">Optional listing filters; <c>ChunkSize</c> (or else <c>Limit</c>) is the page size.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public async IAsyncEnumerable<ActorStoreListItem> IterateAsync(
        StoreListOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new StoreListOptions();
        PageSizing.ValidateChunkSize(options.ChunkSize, nameof(StoreListOptions.ChunkSize));
        var pageSize = options.ChunkSize ?? options.Limit;
        var offset = options.Offset ?? 0;
        while (true)
        {
            var page = await ListAsync(options.WithPage(offset, pageSize), cancellationToken).ConfigureAwait(false);
            var items = page.Items;
            foreach (var item in items)
            {
                yield return item;
            }

            offset += items.Count;
            if (items.Count == 0 || offset >= page.Total)
            {
                yield break;
            }
        }
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
