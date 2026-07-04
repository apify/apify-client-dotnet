using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Resources;

/// <summary>
/// A client for an Actor version's environment variable collection
/// (<c>GET/POST /v2/actors/{actorId}/versions/{versionNumber}/env-vars</c>).
/// </summary>
public sealed class ActorEnvVarCollectionClient
{
    private readonly ResourceContext _ctx;

    internal ActorEnvVarCollectionClient(HttpClientCore http, string versionUrl)
    {
        _ctx = ResourceContext.Collection(http, versionUrl, "env-vars");
    }

    /// <summary>Lists the version's environment variables.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<ActorEnvVar>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _ctx.ListResourceAsync("", new QueryParams(), static d => ActorEnvVar.FromJsonObject(d), cancellationToken);
    }

    /// <summary>Creates a new environment variable.</summary>
    /// <param name="envVar">The environment variable to create.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorEnvVar> CreateAsync(ActorEnvVar envVar, CancellationToken cancellationToken = default)
    {
        return ActorEnvVar.FromJsonObject(
            await _ctx.CreateResourceAsync(new QueryParams(), envVar.ToJsonObject(), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lazily iterates over the version's environment variables. The env-var endpoint returns the whole
    /// list in a single page, so this yields that page's items; it exists for API parity with the other
    /// collection iterators and the reference client.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public async IAsyncEnumerable<ActorEnvVar> IterateAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var page = await ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in page.Items)
        {
            yield return item;
        }
    }

}
