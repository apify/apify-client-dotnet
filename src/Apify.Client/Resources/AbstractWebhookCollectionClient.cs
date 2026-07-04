using System.Collections.Generic;
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

    /// <summary>Lazily iterates over all webhooks across pages, fetching each page on demand.</summary>
    /// <param name="options">Optional listing filters; <c>Offset</c>/<c>Limit</c> bound where iteration
    /// starts and the total number of items yielded.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<Webhook> IterateAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ListOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        return Ctx.IterateListAsync("", q, options.Offset ?? 0, options.Limit, static d => new Webhook(d), cancellationToken);
    }

}
