using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Resources;

/// <summary>A client for a specific webhook dispatch (<c>/v2/webhook-dispatches/{dispatchId}</c>).</summary>
public sealed class WebhookDispatchClient
{
    private readonly ResourceContext _ctx;

    internal WebhookDispatchClient(HttpClientCore http, string baseUrl, string id)
    {
        _ctx = ResourceContext.Single(http, baseUrl, "webhook-dispatches", id);
    }

    /// <summary>Fetches the dispatch, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<WebhookDispatch?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new WebhookDispatch(obj) : null;
    }
}
