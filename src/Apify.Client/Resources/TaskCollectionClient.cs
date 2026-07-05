using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for the Actor task collection (<c>GET/POST /v2/actor-tasks</c>).</summary>
public sealed class TaskCollectionClient
{
    private readonly ResourceContext _ctx;

    internal TaskCollectionClient(HttpClientCore http, string baseUrl)
    {
        _ctx = ResourceContext.Collection(http, baseUrl, "actor-tasks");
    }

    /// <summary>Lists the account's tasks.</summary>
    /// <param name="options">Optional pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<ActorTask>> ListAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new ListOptions()).AppendTo(q);
        return _ctx.ListResourceAsync("", q, static d => new ActorTask(d), cancellationToken);
    }

    /// <summary>Creates a new task.</summary>
    /// <param name="task">Any JSON-serializable task definition.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorTask> CreateAsync(object task, CancellationToken cancellationToken = default)
    {
        return new ActorTask(await _ctx.CreateResourceAsync(new QueryParams(), task, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Lazily iterates over all tasks across pages, fetching each page on demand.</summary>
    /// <param name="options">Optional listing filters; <c>Offset</c>/<c>Limit</c> bound where iteration
    /// starts and the total number of items yielded.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<ActorTask> IterateAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ListOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        return _ctx.IterateListAsync("", q, options.Offset ?? 0, options.Limit, static d => new ActorTask(d), cancellationToken);
    }

}
