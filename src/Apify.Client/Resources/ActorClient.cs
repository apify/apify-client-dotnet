using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>
/// A client for a specific Actor.
/// </summary>
/// <remarks>
/// It provides CRUD methods plus convenience helpers to start/call the Actor, build it, and access its
/// runs, builds, versions and webhooks.
/// </remarks>
public sealed class ActorClient
{
    private readonly ApifyClient _root;
    private readonly HttpClientCore _http;
    private readonly string _baseUrl;
    private readonly ResourceContext _ctx;

    internal ActorClient(ApifyClient root, HttpClientCore http, string baseUrl, string id)
    {
        _root = root;
        _http = http;
        _baseUrl = baseUrl;
        Id = id;
        _ctx = ResourceContext.Single(http, baseUrl, "actors", id);
    }

    /// <summary>The Actor's ID (or <c>username~name</c>) as provided.</summary>
    public string Id { get; }

    /// <summary>Fetches the Actor object, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Actor?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new Actor(obj) : null;
    }

    /// <summary>Updates the Actor with the given fields and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Actor> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new Actor(await _ctx.UpdateResourceAsync("", newFields, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the Actor.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", cancellationToken);

    /// <summary>Starts the Actor and returns immediately with the created run.</summary>
    /// <param name="input">Any JSON-serializable value (or <c>null</c> for no input).</param>
    /// <param name="options">Optional run-start options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> StartAsync(object? input = null, ActorStartOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ActorStartOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        var body = input is null ? null : Json.Encode(input);
        return new ActorRun(await _ctx.PostWithBodyAsync("runs", q, body, options.ContentTypeOrDefault(), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Starts the Actor and waits (client-side polling) for it to finish.</summary>
    /// <param name="input">Any JSON-serializable value (or <c>null</c> for no input).</param>
    /// <param name="options">Optional run-start options.</param>
    /// <param name="waitSecs">Bounds the wait; <c>null</c> waits indefinitely.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> CallAsync(
        object? input = null,
        ActorStartOptions? options = null,
        int? waitSecs = null,
        CancellationToken cancellationToken = default)
    {
        var run = await StartAsync(input, options, cancellationToken).ConfigureAwait(false);
        return await _root.Run(run.Id ?? string.Empty).WaitForFinishAsync(waitSecs, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Validates <paramref name="input"/> against the Actor's input schema and returns whether it is valid.</summary>
    /// <param name="input">Any JSON-serializable value (or <c>null</c>).</param>
    /// <param name="options">Optional validation options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<bool> ValidateInputAsync(object? input = null, ValidateInputOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ValidateInputOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        var body = input is null ? null : Json.Encode(input);
        // The validate-input endpoint returns a bare {"valid": <bool>} object, not the standard
        // {"data": ...} envelope, so parse it without unwrapping.
        var result = await _ctx.PostWithBodyNoEnvelopeAsync("validate-input", q, body, options.ContentTypeOrDefault(), cancellationToken).ConfigureAwait(false);
        return result is JsonObject obj && obj.TryGetPropertyValue("valid", out var valid)
            && valid?.GetValueKind() == System.Text.Json.JsonValueKind.True;
    }

    /// <summary>Builds the given version of the Actor and returns the created build.</summary>
    /// <param name="versionNumber">The version to build (e.g. <c>0.0</c>).</param>
    /// <param name="options">Optional build options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Build> BuildAsync(string versionNumber, ActorBuildOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddString("version", versionNumber);
        (options ?? new ActorBuildOptions()).AppendTo(q);
        return new Build(await _ctx.PostWithBodyAsync("builds", q, null, ResourceContext.ContentTypeJson, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Resolves the Actor's default build and returns a client for it. <paramref name="waitForFinish"/>
    /// optionally bounds how long (seconds) the API waits for the build to finish before responding.
    /// </summary>
    /// <param name="waitForFinish">Optional server-side wait in seconds.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<BuildClient> DefaultBuildAsync(int? waitForFinish = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddInt("waitForFinish", waitForFinish);
        var data = await _ctx.GetResourceRequiredAsync("builds/default", q, cancellationToken).ConfigureAwait(false);
        var build = new Build(data as JsonObject ?? new JsonObject());
        return new BuildClient(_http, _baseUrl, build.Id ?? string.Empty);
    }

    /// <summary>Returns a client for the last run of this Actor, optionally filtered by status and/or origin.</summary>
    /// <param name="options">Optional last-run filters.</param>
    public RunClient LastRun(LastRunOptions? options = null)
    {
        var client = new RunClient(_http, _ctx.SubUrl(""), "runs", "last");
        client.SetLastRunParams(options ?? new LastRunOptions());
        return client;
    }

    /// <summary>A client for this Actor's build collection.</summary>
    public BuildCollectionClient Builds() => new(_http, _ctx.SubUrl(""), "builds");

    /// <summary>A client for this Actor's run collection.</summary>
    public RunCollectionClient Runs() => new(_http, _ctx.SubUrl(""), "runs");

    /// <summary>A client for a specific version of this Actor.</summary>
    /// <param name="versionNumber">The version identifier (e.g. <c>0.1</c>).</param>
    public ActorVersionClient Version(string versionNumber) => new(_http, _ctx.SubUrl(""), versionNumber);

    /// <summary>A client for this Actor's version collection.</summary>
    public ActorVersionCollectionClient Versions() => new(_http, _ctx.SubUrl(""));

    /// <summary>A read-only client for this Actor's webhook collection (<c>GET /v2/actors/{id}/webhooks</c>).</summary>
    public NestedWebhookCollectionClient Webhooks() => new(_http, _ctx.SubUrl(""));
}
