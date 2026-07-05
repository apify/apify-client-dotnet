using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for the dataset collection (<c>GET/POST /v2/datasets</c>).</summary>
public sealed class DatasetCollectionClient
{
    private readonly ResourceContext _ctx;

    internal DatasetCollectionClient(HttpClientCore http, string baseUrl)
    {
        _ctx = ResourceContext.Collection(http, baseUrl, "datasets");
    }

    /// <summary>Lists datasets.</summary>
    /// <param name="options">Optional listing filters and pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<Dataset>> ListAsync(StorageListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new StorageListOptions()).AppendTo(q);
        return _ctx.ListResourceAsync("", q, static d => new Dataset(d), cancellationToken);
    }

    /// <summary>
    /// Gets the dataset with the given name, creating it if it does not exist. An empty/<c>null</c> name
    /// creates a new unnamed dataset. An optional <paramref name="schema"/> is sent when creating the
    /// dataset, mirroring the reference client's <c>getOrCreate(name, { schema })</c>.
    /// </summary>
    /// <param name="name">The dataset name, or <c>null</c> for a new unnamed dataset.</param>
    /// <param name="schema">An optional dataset schema to send on creation.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Dataset> GetOrCreateAsync(string? name = null, JsonNode? schema = null, CancellationToken cancellationToken = default)
    {
        return new Dataset(await _ctx.GetOrCreateNamedAsync(name, schema, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Lazily iterates over all datasets across pages, fetching each page on demand.</summary>
    /// <param name="options">Optional listing filters; <c>Offset</c>/<c>Limit</c> bound where iteration
    /// starts and the total number of items yielded.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<Dataset> IterateAsync(StorageListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new StorageListOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        return _ctx.IterateListAsync("", q, options.Offset ?? 0, options.Limit, static d => new Dataset(d), cancellationToken);
    }

}
