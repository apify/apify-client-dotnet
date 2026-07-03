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
}
