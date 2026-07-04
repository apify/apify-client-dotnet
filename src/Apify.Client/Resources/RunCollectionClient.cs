using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>
/// A client for a run collection: the account-wide collection (<c>GET /v2/actor-runs</c>), an Actor's
/// runs (<c>GET /v2/actors/{id}/runs</c>), or a task's runs (<c>GET /v2/actor-tasks/{id}/runs</c>).
/// </summary>
public sealed class RunCollectionClient
{
    private readonly ResourceContext _ctx;

    internal RunCollectionClient(HttpClientCore http, string baseUrl, string resourcePath)
    {
        _ctx = ResourceContext.Collection(http, baseUrl, resourcePath);
    }

    /// <summary>Lists runs, applying the standard pagination and the run-specific filters.</summary>
    /// <param name="options">Optional pagination.</param>
    /// <param name="filter">Optional run-specific filters.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<ActorRun>> ListAsync(
        ListOptions? options = null,
        RunListOptions? filter = null,
        CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new ListOptions()).AppendTo(q);
        (filter ?? new RunListOptions()).AppendTo(q);
        return _ctx.ListResourceAsync("", q, static d => new ActorRun(d), cancellationToken);
    }

    /// <summary>Lazily iterates over all runs across pages, fetching each page on demand.</summary>
    /// <param name="options">Optional pagination; <c>Offset</c>/<c>Limit</c> bound where iteration starts
    /// and the total number of runs yielded.</param>
    /// <param name="filter">Optional run-specific filters.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<ActorRun> IterateAsync(
        ListOptions? options = null,
        RunListOptions? filter = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ListOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        (filter ?? new RunListOptions()).AppendTo(q);
        return _ctx.IterateListAsync("", q, options.Offset ?? 0, options.Limit, static d => new ActorRun(d), cancellationToken);
    }

}
