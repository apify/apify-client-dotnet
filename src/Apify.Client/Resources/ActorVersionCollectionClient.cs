using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Resources;

/// <summary>A client for an Actor's version collection (<c>GET/POST /v2/actors/{actorId}/versions</c>).</summary>
public sealed class ActorVersionCollectionClient
{
    private readonly ResourceContext _ctx;

    internal ActorVersionCollectionClient(HttpClientCore http, string actorUrl)
    {
        _ctx = ResourceContext.Collection(http, actorUrl, "versions");
    }

    /// <summary>Lists the Actor's versions. The endpoint returns all of them in one response, so this takes no paging options.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<ActorVersion>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _ctx.ListResourceAsync("", new QueryParams(), static d => new ActorVersion(d), RequestTier.Short, cancellationToken);
    }

    /// <summary>Creates a new Actor version.</summary>
    /// <param name="version">Any JSON-serializable version definition.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorVersion> CreateAsync(object version, CancellationToken cancellationToken = default)
    {
        return new ActorVersion(await _ctx.CreateResourceAsync(new QueryParams(), version, RequestTier.Short, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Iterates the Actor's versions. The endpoint returns the whole list in one page, so this yields that page's items; it
    /// exists for parity with the other collection iterators and the reference client.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public async IAsyncEnumerable<ActorVersion> IterateAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var page = await ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in page.Items)
        {
            yield return item;
        }
    }

    /// <summary>
    /// Returns this client with the timeout of every subsequent call set to <paramref name="timeout"/>, overriding the
    /// tier default (see "Timeout tiers" in docs/README.md). <see cref="TimeSpan.Zero"/> means no timeout. A value above
    /// <see cref="ApifyClientOptions.TimeoutSecs"/> is capped at it.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public ActorVersionCollectionClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
