using System;
using Apify.Client.Internal;

namespace Apify.Client.Resources;

/// <summary>
/// A read-only client for the webhooks nested under an Actor or task. These endpoints only list; create webhooks with
/// the account-wide <see cref="WebhookCollectionClient"/>, targeting the Actor or task in the webhook's <c>condition</c>.
/// </summary>
public sealed class NestedWebhookCollectionClient : AbstractWebhookCollectionClient
{
    internal NestedWebhookCollectionClient(HttpClientCore http, string baseUrl)
        : base(http, baseUrl)
    {
    }

    /// <summary>
    /// Returns this client with the timeout of every subsequent call set to <paramref name="timeout"/>, overriding the
    /// tier default (see "Timeout tiers" in docs/README.md). <see cref="TimeSpan.Zero"/> means no timeout. A value above
    /// <see cref="ApifyClientOptions.TimeoutSecs"/> is capped at it.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public NestedWebhookCollectionClient WithTimeout(TimeSpan timeout)
    {
        Ctx.WithTimeout(timeout);
        return this;
    }
}
