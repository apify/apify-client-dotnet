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
/// A client for a specific Actor task.
/// </summary>
/// <remarks>
/// Tasks are pre-configured Actor runs with stored input. The client provides CRUD methods plus
/// convenience helpers to start/call the task and access its input, runs and webhooks.
/// </remarks>
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
        var data = await _ctx.GetResourceAsync("", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new ActorTask(obj) : null;
    }

    /// <summary>Updates the task with the given fields and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorTask> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new ActorTask(await _ctx.UpdateResourceAsync("", newFields, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the task.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", cancellationToken);

    /// <summary>
    /// Publishes the task on its public landing page in Apify Store, by setting <c>isPublic</c>
    /// through <see cref="UpdateAsync"/>.
    /// </summary>
    /// <remarks>
    /// The task's Actor must be public and the task must already have its public display
    /// configuration (<see cref="ActorTask.PublicConfig"/>) set up. Requires write permission to both
    /// the task and its Actor. Publishing an already published task does nothing.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<ActorTask> PublishAsync(CancellationToken cancellationToken = default) =>
        UpdateAsync(new { isPublic = true }, cancellationToken);

    /// <summary>
    /// Unpublishes the task from its public landing page, by setting <c>isPublic</c> through
    /// <see cref="UpdateAsync"/>.
    /// </summary>
    /// <remarks>
    /// The public display configuration (<see cref="ActorTask.PublicConfig"/>) is preserved, so the
    /// task can be published again without re-entering it. Requires write permission to the task only
    /// (unlike <see cref="PublishAsync"/>, it does not require permission to the task's Actor).
    /// Unpublishing a task that is not published does nothing.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<ActorTask> UnpublishAsync(CancellationToken cancellationToken = default) =>
        UpdateAsync(new { isPublic = false }, cancellationToken);

    /// <summary>Starts the task and returns immediately with the created run.</summary>
    /// <param name="input">Optionally overrides the task's stored input (<c>null</c> to use it).</param>
    /// <param name="options">Optional run-start options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> StartAsync(object? input = null, TaskStartOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new TaskStartOptions()).AppendTo(q);
        var body = input is null ? null : Json.Encode(input);
        return new ActorRun(await _ctx.PostWithBodyAsync("runs", q, body, ResourceContext.ContentTypeJson, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Starts the task and waits (client-side polling) for it to finish.</summary>
    /// <param name="input">Optionally overrides the task's stored input.</param>
    /// <param name="options">Optional run-start options.</param>
    /// <param name="waitSecs">Bounds the wait; <c>null</c> waits indefinitely.</param>
    /// <param name="log">
    /// If provided, the run's live log is redirected to this sink (one complete message per call) for the
    /// duration of the wait, matching the reference client's <c>log</c> call option. <c>null</c> disables
    /// redirection.
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

    /// <summary>Fetches the task's stored input, or <c>null</c> if none is set.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<JsonNode?> GetInputAsync(CancellationToken cancellationToken = default)
    {
        var body = await _ctx.GetRawAsync("input", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return body is null ? null : Json.Decode(body);
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
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return Json.Decode(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Returns a client for the last run of this task, optionally filtered by status and/or origin.</summary>
    /// <param name="options">Optional last-run filters.</param>
    public RunClient LastRun(LastRunOptions? options = null)
    {
        var client = new RunClient(_http, _ctx.SubUrl(""), "runs", "last");
        client.SetLastRunParams(options ?? new LastRunOptions());
        return client;
    }

    /// <summary>A client for this task's run collection.</summary>
    public RunCollectionClient Runs() => new(_http, _ctx.SubUrl(""), "runs");

    /// <summary>A read-only client for this task's webhook collection (<c>GET /v2/actor-tasks/{id}/webhooks</c>).</summary>
    public NestedWebhookCollectionClient Webhooks() => new(_http, _ctx.SubUrl(""));
}
