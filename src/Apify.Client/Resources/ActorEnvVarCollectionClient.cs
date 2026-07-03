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
}
