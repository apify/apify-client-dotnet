using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for the key-value store collection (<c>GET/POST /v2/key-value-stores</c>).</summary>
public sealed class KeyValueStoreCollectionClient
{
    private readonly ResourceContext _ctx;

    internal KeyValueStoreCollectionClient(HttpClientCore http, string baseUrl)
    {
        _ctx = ResourceContext.Collection(http, baseUrl, "key-value-stores");
    }

    /// <summary>Lists key-value stores.</summary>
    /// <param name="options">Optional listing filters and pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<KeyValueStore>> ListAsync(StorageListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new StorageListOptions()).AppendTo(q);
        return _ctx.ListResourceAsync("", q, static d => new KeyValueStore(d), cancellationToken);
    }

    /// <summary>
    /// Gets the store with the given name, creating it if it does not exist. An empty/<c>null</c> name
    /// creates a new unnamed store. An optional <paramref name="schema"/> is sent when creating the store.
    /// </summary>
    /// <param name="name">The store name, or <c>null</c> for a new unnamed store.</param>
    /// <param name="schema">An optional store schema to send on creation.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<KeyValueStore> GetOrCreateAsync(string? name = null, JsonNode? schema = null, CancellationToken cancellationToken = default)
    {
        return new KeyValueStore(await _ctx.GetOrCreateNamedAsync(name, schema, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Lazily iterates over all key-value stores across pages, fetching each page on demand.</summary>
    /// <param name="options">Optional listing filters; <c>Offset</c>/<c>Limit</c> bound where iteration
    /// starts and the total number of items yielded.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<KeyValueStore> IterateAsync(StorageListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new StorageListOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        return _ctx.IterateListAsync("", q, options.Offset ?? 0, options.Limit, static d => new KeyValueStore(d), cancellationToken);
    }

}
