using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>
/// A client for a build collection: the account-wide collection (<c>GET /v2/actor-builds</c>) or an
/// Actor's builds (<c>GET /v2/actors/{id}/builds</c>).
/// </summary>
public sealed class BuildCollectionClient
{
    private readonly ResourceContext _ctx;

    internal BuildCollectionClient(HttpClientCore http, string baseUrl, string resourcePath)
    {
        _ctx = ResourceContext.Collection(http, baseUrl, resourcePath);
    }

    /// <summary>Lists builds.</summary>
    /// <param name="options">Optional pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<Build>> ListAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new ListOptions()).AppendTo(q);
        return _ctx.ListResourceAsync("", q, static d => new Build(d), cancellationToken);
    }

    /// <summary>Lazily iterates over all builds across pages, fetching each page on demand.</summary>
    /// <param name="options">Optional listing filters; <c>Offset</c>/<c>Limit</c> bound where iteration
    /// starts and the total number of items yielded.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<Build> IterateAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ListOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        return _ctx.IterateListAsync("", q, options.Offset ?? 0, options.Limit, static d => new Build(d), cancellationToken);
    }

}
