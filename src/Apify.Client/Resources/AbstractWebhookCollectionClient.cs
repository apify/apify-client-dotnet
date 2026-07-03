using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>
/// Shared read-only behavior for webhook collections. Both the account-wide collection
/// (<see cref="WebhookCollectionClient"/>) and the read-only collections nested under an Actor or task
/// (<see cref="NestedWebhookCollectionClient"/>) can list webhooks; only the account-wide collection can
/// create them.
/// </summary>
public abstract class AbstractWebhookCollectionClient
{
    private protected readonly ResourceContext Ctx;

    private protected AbstractWebhookCollectionClient(HttpClientCore http, string baseUrl)
    {
        Ctx = ResourceContext.Collection(http, baseUrl, "webhooks");
    }

    /// <summary>Lists webhooks.</summary>
    /// <param name="options">Optional pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<Webhook>> ListAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new ListOptions()).AppendTo(q);
        return Ctx.ListResourceAsync("", q, static d => new Webhook(d), cancellationToken);
    }
}
