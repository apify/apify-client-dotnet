using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Resources;

/// <summary>A client for a specific Actor build (<c>/v2/actor-builds/{buildId}</c>).</summary>
public sealed class BuildClient
{
    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;

    internal BuildClient(HttpClientCore http, string baseUrl, string id)
    {
        _http = http;
        _ctx = ResourceContext.Single(http, baseUrl, "actor-builds", id);
    }

    /// <summary>
    /// Fetches the build, optionally asking the API to wait up to <paramref name="waitForFinishSecs"/>
    /// seconds (max 60) for the build to finish before responding. Returns <c>null</c> if it does not exist.
    /// </summary>
    /// <param name="waitForFinishSecs">Optional server-side wait in seconds.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Build?> GetAsync(int? waitForFinishSecs = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        // Clamp to the client's per-request timeout so a short custom timeout doesn't abort the call.
        q.AddInt("waitForFinish", _ctx.ClampServerWait(waitForFinishSecs));
        var data = await _ctx.GetResourceAsync("", q, cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new Build(obj) : null;
    }

    /// <summary>Aborts the build and returns its updated state.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Build> AbortAsync(CancellationToken cancellationToken = default)
    {
        return new Build(await _ctx.PostWithBodyAsync("abort", new QueryParams(), null, "", cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the build.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", cancellationToken);

    /// <summary>
    /// Polls until the build reaches a terminal state or <paramref name="waitSecs"/> elapses (<c>null</c>
    /// waits indefinitely). Returns the latest build.
    /// </summary>
    /// <param name="waitSecs">The wait budget in seconds, or <c>null</c> to wait indefinitely.</param>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    public async Task<Build> WaitForFinishAsync(int? waitSecs = null, CancellationToken cancellationToken = default)
    {
        var data = await _ctx.WaitForFinishAsync(waitSecs, "build", static d => new Build(d).IsTerminal, cancellationToken).ConfigureAwait(false);
        return new Build(data);
    }

    /// <summary>Returns the OpenAPI definition generated for the build, or <c>null</c> if unavailable.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<JsonObject?> GetOpenApiDefinitionAsync(CancellationToken cancellationToken = default)
    {
        var body = await _ctx.GetRawAsync("openapi.json", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return body is null ? null : Json.Decode(body) as JsonObject;
    }

    /// <summary>A client for accessing this build's log.</summary>
    public LogClient Log() => LogClient.Nested(_http, _ctx.SubUrl(""));
}
