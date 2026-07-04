using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for the request queue collection (<c>GET/POST /v2/request-queues</c>).</summary>
public sealed class RequestQueueCollectionClient
{
    private readonly ResourceContext _ctx;

    internal RequestQueueCollectionClient(HttpClientCore http, string baseUrl)
    {
        _ctx = ResourceContext.Collection(http, baseUrl, "request-queues");
    }

    /// <summary>Lists request queues.</summary>
    /// <param name="options">Optional listing filters and pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<RequestQueue>> ListAsync(StorageListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new StorageListOptions()).AppendTo(q);
        return _ctx.ListResourceAsync("", q, static d => new RequestQueue(d), cancellationToken);
    }

    /// <summary>
    /// Gets the queue with the given name, creating it if it does not exist. An empty/<c>null</c> name
    /// creates a new unnamed queue.
    /// </summary>
    /// <param name="name">The queue name, or <c>null</c> for a new unnamed queue.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueue> GetOrCreateAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        return new RequestQueue(await _ctx.GetOrCreateNamedAsync(name, null, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Lazily iterates over all request queues across pages, fetching each page on demand.</summary>
    /// <param name="options">Optional listing filters; <c>Offset</c>/<c>Limit</c> bound where iteration
    /// starts and the total number of items yielded.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<RequestQueue> IterateAsync(StorageListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new StorageListOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        return _ctx.IterateListAsync("", q, options.Offset ?? 0, options.Limit, static d => new RequestQueue(d), cancellationToken);
    }

}
