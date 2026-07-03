using Apify.Client.Internal;

namespace Apify.Client.Resources;

/// <summary>
/// A read-only client for the webhooks nested under an Actor (<c>GET /v2/actors/{id}/webhooks</c>) or a
/// task (<c>GET /v2/actor-tasks/{id}/webhooks</c>). These endpoints only support listing; webhooks are
/// created through the account-wide <see cref="WebhookCollectionClient"/> (which targets an Actor or task
/// via the webhook's <c>condition</c>), so <c>Create</c> is intentionally not exposed.
/// </summary>
public sealed class NestedWebhookCollectionClient : AbstractWebhookCollectionClient
{
    internal NestedWebhookCollectionClient(HttpClientCore http, string baseUrl)
        : base(http, baseUrl)
    {
    }
}
