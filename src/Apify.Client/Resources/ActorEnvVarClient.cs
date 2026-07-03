using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Resources;

/// <summary>
/// A client for a single environment variable
/// (<c>GET/PUT/DELETE /v2/actors/{actorId}/versions/{versionNumber}/env-vars/{name}</c>).
/// </summary>
public sealed class ActorEnvVarClient
{
    private readonly ResourceContext _ctx;

    internal ActorEnvVarClient(HttpClientCore http, string versionUrl, string name)
    {
        _ctx = ResourceContext.Single(http, versionUrl, "env-vars", name);
    }

    /// <summary>Fetches the environment variable, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorEnvVar?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? ActorEnvVar.FromJsonObject(obj) : null;
    }

    /// <summary>Updates the environment variable and returns the updated object.</summary>
    /// <param name="envVar">The new environment variable state.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorEnvVar> UpdateAsync(ActorEnvVar envVar, CancellationToken cancellationToken = default)
    {
        return ActorEnvVar.FromJsonObject(
            await _ctx.UpdateResourceAsync("", envVar.ToJsonObject(), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the environment variable.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", cancellationToken);
}
