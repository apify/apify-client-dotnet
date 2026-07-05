using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Resources;

/// <summary>
/// A client for a specific Actor version
/// (<c>GET/PUT/DELETE /v2/actors/{actorId}/versions/{versionNumber}</c>).
/// </summary>
public sealed class ActorVersionClient
{
    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;
    private readonly string _versionUrl;

    internal ActorVersionClient(HttpClientCore http, string actorUrl, string versionNumber)
    {
        _http = http;
        _ctx = ResourceContext.Single(http, actorUrl, "versions", versionNumber);
        _versionUrl = _ctx.SubUrl("");
    }

    /// <summary>Fetches the version, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorVersion?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new ActorVersion(obj) : null;
    }

    /// <summary>Updates the version with the given fields and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorVersion> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new ActorVersion(await _ctx.UpdateResourceAsync("", newFields, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the version.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", cancellationToken);

    /// <summary>A client for a specific environment variable of this version.</summary>
    /// <param name="name">The environment variable name.</param>
    public ActorEnvVarClient EnvVar(string name) => new(_http, _versionUrl, name);

    /// <summary>A client for this version's environment variable collection.</summary>
    public ActorEnvVarCollectionClient EnvVars() => new(_http, _versionUrl);
}
