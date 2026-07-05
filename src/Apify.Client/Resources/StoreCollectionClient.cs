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
        return _ctx.ListResourceAsync("", q, static d => new ActorStoreListItem(d), cancellationToken);
    }

    /// <summary>
    /// Lazily iterates over all Store Actors matching the options, fetching pages on demand. The options'
    /// <c>Limit</c> (if set) is used as the per-page size.
    /// </summary>
    /// <param name="options">Optional listing filters; <c>Limit</c> is used as the page size.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public async IAsyncEnumerable<ActorStoreListItem> IterateAsync(
        StoreListOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new StoreListOptions();
        var offset = options.Offset ?? 0;
        while (true)
        {
            var page = await ListAsync(options.WithOffset(offset), cancellationToken).ConfigureAwait(false);
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
}
