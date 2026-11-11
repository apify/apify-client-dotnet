using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>
/// A client for a specific Actor: get, update, delete, start, call, build, and access to its runs, builds, versions,
/// webhooks and server interface.
/// </summary>
public sealed class ActorClient
{
    private readonly ApifyClient _root;
    private readonly HttpClientCore _http;
    private readonly string _baseUrl;
    private readonly ResourceContext _ctx;
    private ActorServerClient? _serverClient;

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
        var data = await _ctx.GetResourceAsync("", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new Actor(obj) : null;
    }

    /// <summary>Updates the Actor and returns the updated Actor.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Actor> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new Actor(await _ctx.UpdateResourceAsync("", newFields, RequestTier.Short, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the Actor.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", RequestTier.Short, cancellationToken);

    /// <summary>Starts the Actor and returns the created run without waiting.</summary>
    /// <param name="input">
    /// <c>byte[]</c> is sent as it is (typically with <see cref="ActorStartOptions.ContentType"/>), anything else as JSON,
    /// and <c>null</c> sends no input.
    /// </param>
    /// <param name="options">Optional run-start options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<ActorRun> StartAsync(object? input = null, ActorStartOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ActorStartOptions();
        return ResourceLimitRetry.StartAsync(
            () => StartOnceAsync(input, options, cancellationToken),
            options.WaitForResources,
            cancellationToken);
    }

    private async Task<ActorRun> StartOnceAsync(object? input, ActorStartOptions options, CancellationToken cancellationToken)
    {
        var q = new QueryParams();
        options.AppendTo(q);
        ResourceContext.EncodeInputBody(input, out var body, out var bodyBytes);
        // Long: options.WaitForFinish can make the server hold the connection for up to 60s, and it is not clamped here.
        return new ActorRun(await _ctx.PostWithBodyAsync("runs", q, body, options.ContentTypeOrDefault(), RequestTier.Long, cancellationToken, bodyBytes).ConfigureAwait(false));
    }

    /// <summary>Starts the Actor and waits for it to finish by polling.</summary>
    /// <param name="input">Any JSON-serializable value, or <c>null</c> for no input.</param>
    /// <param name="options">
    /// Optional run-start options. Time spent retrying <see cref="ActorStartOptions.WaitForResources"/> does not count
    /// toward <paramref name="waitSecs"/>.
    /// </param>
    /// <param name="waitSecs">Bounds the wait; <c>null</c> waits indefinitely.</param>
    /// <param name="log">
    /// Redirects the run's live log to this sink, one complete message per call, while waiting. Unlike the reference
    /// client, <c>null</c> redirects nothing; pass <see cref="StreamedLog.DefaultConsole"/> for the reference's default
    /// redirection with the <c>&lt;Actor name&gt; runId:&lt;id&gt; -&gt; </c> prefix.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> CallAsync(
        object? input = null,
        ActorStartOptions? options = null,
        int? waitSecs = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var run = await StartAsync(input, options, cancellationToken).ConfigureAwait(false);
        return await _root.Run(run.Id ?? string.Empty).WaitForFinishWithLogAsync(waitSecs, log, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checks <paramref name="input"/> against the Actor's input schema and returns whether it is valid.</summary>
    /// <param name="input"><c>byte[]</c> is sent as it is, anything else as JSON, <c>null</c> sends no input.</param>
    /// <param name="options">Optional validation options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<bool> ValidateInputAsync(object? input = null, ValidateInputOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ValidateInputOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        ResourceContext.EncodeInputBody(input, out var body, out var bodyBytes);
        // The endpoint returns a bare {"valid": bool} without the usual "data" envelope.
        var result = await _ctx.PostWithBodyNoEnvelopeAsync("validate-input", q, body, options.ContentTypeOrDefault(), RequestTier.Short, cancellationToken, bodyBytes).ConfigureAwait(false);
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
        return new Build(await _ctx.PostWithBodyAsync("builds", q, null, ResourceContext.ContentTypeJson, RequestTier.Short, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Returns a client for the Actor's default build. <paramref name="waitForFinish"/> bounds how long (seconds) the API
    /// waits for the build to finish.
    /// </summary>
    /// <param name="waitForFinish">Optional server-side wait in seconds.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<BuildClient> DefaultBuildAsync(int? waitForFinish = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddInt("waitForFinish", waitForFinish);
        // Long: waitForFinish is not clamped.
        var data = await _ctx.GetResourceRequiredAsync("builds/default", q, RequestTier.Long, cancellationToken).ConfigureAwait(false);
        var build = new Build(data);
        return new BuildClient(_http, _baseUrl, build.Id ?? string.Empty);
    }

    /// <summary>Returns a client for the last run of this Actor, optionally filtered by status and origin.</summary>
    /// <param name="options">Optional last-run filters.</param>
    public RunClient LastRun(LastRunOptions? options = null)
    {
        var client = new RunClient(_root, _http, _ctx.SubUrl(""), "runs", "last");
        client.SetLastRunParams(options ?? new LastRunOptions());
        return client;
    }

    /// <summary>A client for this Actor's build collection.</summary>
    public BuildCollectionClient Builds() => new(_http, _ctx.SubUrl(""), "builds");

    /// <summary>A client for this Actor's run collection.</summary>
    public RunCollectionClient Runs() => new(_http, _ctx.SubUrl(""), "runs");

    /// <summary>A client for a specific version of this Actor.</summary>
    /// <param name="versionNumber">The version, such as <c>0.1</c>. Must not be empty.</param>
    public ActorVersionClient Version(string versionNumber)
    {
        if (versionNumber.Length == 0)
        {
            throw new ArgumentException("versionNumber must not be empty", nameof(versionNumber));
        }

        return new ActorVersionClient(_http, _ctx.SubUrl(""), versionNumber);
    }

    /// <summary>A client for this Actor's version collection.</summary>
    public ActorVersionCollectionClient Versions() => new(_http, _ctx.SubUrl(""));

    /// <summary>
    /// A client for the server interface of this Actor. A Server Actor (formerly Actor Standby) runs a web server that is
    /// called over HTTP without starting a run per call. Every call returns the same client, which reuses the server URL.
    /// </summary>
    public ActorServerClient Server()
        => LazyInitializer.EnsureInitialized(ref _serverClient, () => new ActorServerClient(_http, _baseUrl, Id));

    /// <summary>A read-only client for this Actor's webhooks.</summary>
    public NestedWebhookCollectionClient Webhooks() => new(_http, _ctx.SubUrl(""));

    /// <summary>
    /// Returns this client with the timeout of every subsequent call set to <paramref name="timeout"/>, overriding the
    /// tier default (see "Timeout tiers" in docs/README.md). <see cref="TimeSpan.Zero"/> means no timeout. A value above
    /// <see cref="ApifyClientOptions.TimeoutSecs"/> is capped at it.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public ActorClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
