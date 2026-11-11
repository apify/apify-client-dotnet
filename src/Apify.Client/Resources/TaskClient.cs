using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>
/// A client for a specific Actor task, a pre-configured Actor run with stored input: get, update, delete, publish,
/// start, call, and access to its input, runs, and webhooks.
/// </summary>
public sealed class TaskClient
{
    private readonly ApifyClient _root;
    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;

    internal TaskClient(ApifyClient root, HttpClientCore http, string baseUrl, string id)
    {
        _root = root;
        _http = http;
        _ctx = ResourceContext.Single(http, baseUrl, "actor-tasks", id);
    }

    /// <summary>Fetches the task object, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorTask?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new ActorTask(obj) : null;
    }

    /// <summary>Updates the task and returns the updated task.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorTask> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new ActorTask(await _ctx.UpdateResourceAsync("", newFields, RequestTier.Short, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the task.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", RequestTier.Short, cancellationToken);

    /// <summary>Publishes the task on its public landing page in Apify Store, by setting <c>isPublic</c>.</summary>
    /// <remarks>
    /// The task's Actor must be public, <see cref="ActorTask.PublicConfig"/> must have <c>InputSchemaFields</c> and
    /// <c>DatasetView</c>, and the Actor must have fewer than 10 published tasks (100 per account). Otherwise the request
    /// fails and <c>isPublic</c> is unchanged. Needs write permission to the Actor. Publishing a published task does nothing.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<ActorTask> PublishAsync(CancellationToken cancellationToken = default) =>
        UpdateAsync(new { isPublic = true }, cancellationToken);

    /// <summary>Unpublishes the task by clearing <c>isPublic</c>.</summary>
    /// <remarks>
    /// <see cref="ActorTask.PublicConfig"/> is kept, so the task can be published again. Needs write permission to the
    /// Actor. Unpublishing an unpublished task does nothing.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<ActorTask> UnpublishAsync(CancellationToken cancellationToken = default) =>
        UpdateAsync(new { isPublic = false }, cancellationToken);

    /// <summary>Starts the task and returns the created run without waiting.</summary>
    /// <remarks>
    /// Unlike <see cref="ActorClient.StartAsync"/>, <paramref name="input"/> cannot be raw bytes: the input always
    /// replaces the stored input as JSON, as in the reference client.
    /// </remarks>
    /// <param name="input">
    /// Overrides the task's stored input: any JSON-serializable object or array, or <c>null</c> to use the stored input.
    /// </param>
    /// <param name="options">Optional run-start options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<ActorRun> StartAsync(object? input = null, TaskStartOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new TaskStartOptions();
        return ResourceLimitRetry.StartAsync(
            () => StartOnceAsync(input, options, cancellationToken),
            options.WaitForResources,
            cancellationToken);
    }

    private async Task<ActorRun> StartOnceAsync(object? input, TaskStartOptions options, CancellationToken cancellationToken)
    {
        var q = new QueryParams();
        options.AppendTo(q);
        var body = input is null ? null : Json.Encode(input);
        // Long: options.WaitForFinish can make the server hold the connection for up to 60s, and it is not clamped here.
        return new ActorRun(await _ctx.PostWithBodyAsync("runs", q, body, ResourceContext.ContentTypeJson, RequestTier.Long, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Starts the task and waits for it to finish by polling.</summary>
    /// <param name="input">Optionally overrides the task's stored input.</param>
    /// <param name="options">
    /// Optional run-start options. Time spent retrying <see cref="TaskStartOptions.WaitForResources"/> does not count
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
        TaskStartOptions? options = null,
        int? waitSecs = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var run = await StartAsync(input, options, cancellationToken).ConfigureAwait(false);
        return await _root.Run(run.Id ?? string.Empty).WaitForFinishWithLogAsync(waitSecs, log, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches the task's stored input; an empty JSON object if there is none. Throws if the task does not exist, as in the
    /// reference client.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<JsonNode?> GetInputAsync(CancellationToken cancellationToken = default)
    {
        return await _ctx.GetJsonRequiredAsync("input", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Replaces the task's stored input and returns the updated input.</summary>
    /// <param name="input">Any JSON-serializable value.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<JsonNode?> UpdateInputAsync(object input, CancellationToken cancellationToken = default)
    {
        using var response = await _http.CallAsync(
            HttpMethod.Put,
            _ctx.SubUrl("input"),
            Json.Encode(input),
            ResourceContext.ContentTypeJson,
            timeout: _ctx.RequestTimeout,
            tier: RequestTier.Short,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return await response.ReadJsonAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns a client for the last run of this task, optionally filtered by status and origin.</summary>
    /// <param name="options">Optional last-run filters.</param>
    public RunClient LastRun(LastRunOptions? options = null)
    {
        var client = new RunClient(_root, _http, _ctx.SubUrl(""), "runs", "last");
        client.SetLastRunParams(options ?? new LastRunOptions());
        return client;
    }

    /// <summary>A client for this task's run collection.</summary>
    public RunCollectionClient Runs() => new(_http, _ctx.SubUrl(""), "runs");

    /// <summary>A read-only client for this task's webhooks.</summary>
    public NestedWebhookCollectionClient Webhooks() => new(_http, _ctx.SubUrl(""));

    /// <summary>
    /// Returns this client with the timeout of every subsequent call set to <paramref name="timeout"/>, overriding the
    /// tier default (see "Timeout tiers" in docs/README.md). <see cref="TimeSpan.Zero"/> means no timeout. A value above
    /// <see cref="ApifyClientOptions.TimeoutSecs"/> is capped at it.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public TaskClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
