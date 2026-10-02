using System;
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

    /// <summary>
    /// Returns this client with every subsequent call's timeout set to <paramref name="timeout"/>,
    /// overriding the tier default (see the "Timeout tiers" section of the top-level README). Pass
    /// <see cref="TimeSpan.Zero"/> for no timeout, matching the reference client's <c>'noTimeout'</c>.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public NestedWebhookCollectionClient WithTimeout(TimeSpan timeout)
    {
        Ctx.WithTimeout(timeout);
        return this;
    }
}
