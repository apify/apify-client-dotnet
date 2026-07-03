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
}
