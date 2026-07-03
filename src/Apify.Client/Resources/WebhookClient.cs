using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Resources;

/// <summary>A client for a specific webhook (<c>/v2/webhooks/{webhookId}</c>).</summary>
public sealed class WebhookClient
{
    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;

    internal WebhookClient(HttpClientCore http, string baseUrl, string id)
    {
        _http = http;
        _ctx = ResourceContext.Single(http, baseUrl, "webhooks", id);
    }

    /// <summary>Fetches the webhook, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Webhook?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new Webhook(obj) : null;
    }

    /// <summary>Updates the webhook with the given fields and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Webhook> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new Webhook(await _ctx.UpdateResourceAsync("", newFields, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the webhook.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", cancellationToken);

    /// <summary>Dispatches the webhook immediately and returns the resulting dispatch.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<WebhookDispatch> TestAsync(CancellationToken cancellationToken = default)
    {
        return new WebhookDispatch(await _ctx.PostWithBodyAsync("test", new QueryParams(), null, "", cancellationToken).ConfigureAwait(false));
    }

    /// <summary>A client for this webhook's dispatch collection.</summary>
    public WebhookDispatchCollectionClient Dispatches() => new(_http, _ctx.SubUrl(""), "dispatches");
}
