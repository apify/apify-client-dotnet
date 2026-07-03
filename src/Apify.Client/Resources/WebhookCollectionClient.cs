using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Resources;

/// <summary>
/// A client for the account-wide webhook collection (<c>GET/POST /v2/webhooks</c>), supporting both
/// listing and creation. Webhooks nested under an Actor or task are read-only and use
/// <see cref="NestedWebhookCollectionClient"/> instead.
/// </summary>
public sealed class WebhookCollectionClient : AbstractWebhookCollectionClient
{
    internal WebhookCollectionClient(HttpClientCore http, string baseUrl)
        : base(http, baseUrl)
    {
    }

    /// <summary>Creates a new webhook.</summary>
    /// <param name="webhook">Any JSON-serializable webhook definition.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Webhook> CreateAsync(object webhook, CancellationToken cancellationToken = default)
    {
        return new Webhook(await Ctx.CreateResourceAsync(new QueryParams(), webhook, cancellationToken).ConfigureAwait(false));
    }
}
