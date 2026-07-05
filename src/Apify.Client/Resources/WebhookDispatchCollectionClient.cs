using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>
/// A client for a webhook dispatch collection: the account-wide collection
/// (<c>GET /v2/webhook-dispatches</c>) or dispatches nested under a webhook.
/// </summary>
public sealed class WebhookDispatchCollectionClient
{
    private readonly ResourceContext _ctx;

    internal WebhookDispatchCollectionClient(HttpClientCore http, string baseUrl, string resourcePath)
    {
        _ctx = ResourceContext.Collection(http, baseUrl, resourcePath);
    }

    /// <summary>Lists webhook dispatches.</summary>
    /// <param name="options">Optional pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<WebhookDispatch>> ListAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new ListOptions()).AppendTo(q);
        return _ctx.ListResourceAsync("", q, static d => new WebhookDispatch(d), cancellationToken);
    }

    /// <summary>Lazily iterates over all webhook dispatches across pages, fetching each page on demand.</summary>
    /// <param name="options">Optional listing filters; <c>Offset</c>/<c>Limit</c> bound where iteration
    /// starts and the total number of items yielded.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<WebhookDispatch> IterateAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ListOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        return _ctx.IterateListAsync("", q, options.Offset ?? 0, options.Limit, static d => new WebhookDispatch(d), cancellationToken);
    }

}
