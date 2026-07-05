using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>
/// A client for a specific Actor run.
/// </summary>
/// <remarks>
/// It provides CRUD methods plus convenience helpers (abort, metamorph, reboot, resurrect, charge,
/// wait-for-finish) and accessors for the run's default storages and log.
/// </remarks>
public sealed class RunClient
{
    /// <summary>Header the API uses to deduplicate charge requests.</summary>
    private const string ChargeIdempotencyHeader = "idempotency-key";

    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;
    private readonly string _id;

    internal RunClient(HttpClientCore http, string baseUrl, string resourcePath, string id)
    {
        _http = http;
        _id = id;
        _ctx = ResourceContext.Single(http, baseUrl, resourcePath, id);
    }

    /// <summary>
    /// Pins the <c>status</c>/<c>origin</c> query parameters inherited by all calls on this client (used by
    /// the last-run accessors). Empty values are skipped.
    /// </summary>
    internal void SetLastRunParams(LastRunOptions options)
    {
        if (!string.IsNullOrEmpty(options.Status))
        {
            _ctx.BaseParams.AddRaw("status", options.Status);
        }

        if (!string.IsNullOrEmpty(options.Origin))
        {
            _ctx.BaseParams.AddRaw("origin", options.Origin);
        }
    }

    /// <summary>
    /// Fetches the run, optionally asking the API to wait up to <paramref name="waitForFinishSecs"/>
    /// seconds (max 60) for the run to reach a terminal state. Returns <c>null</c> if it does not exist.
    /// </summary>
    /// <param name="waitForFinishSecs">Optional server-side wait in seconds.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun?> GetAsync(int? waitForFinishSecs = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddInt("waitForFinish", _ctx.ClampServerWait(waitForFinishSecs));
        var data = await _ctx.GetResourceAsync("", q, cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new ActorRun(obj) : null;
    }

    /// <summary>Updates the run with the given fields and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new ActorRun(await _ctx.UpdateResourceAsync("", newFields, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the run.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", cancellationToken);

    /// <summary>
    /// Aborts the run. If <paramref name="gracefully"/> is <c>true</c>, the run is signalled so it can
    /// finish the current request before terminating; <c>false</c> aborts immediately. <c>null</c> omits
    /// the parameter and lets the server apply its default (immediate abort).
    /// </summary>
    /// <param name="gracefully">Whether to abort gracefully, or <c>null</c> for the server default.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> AbortAsync(bool? gracefully = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddBool("gracefully", gracefully);
        return new ActorRun(await _ctx.PostWithBodyAsync("abort", q, null, "", cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Transforms the run into a run of another Actor with a new input.</summary>
    /// <param name="targetActorId">The Actor to metamorph into.</param>
    /// <param name="input">The new input (<c>null</c> for none).</param>
    /// <param name="options">Optional metamorph options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> MetamorphAsync(
        string targetActorId,
        object? input = null,
        MetamorphOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MetamorphOptions();
        var q = new QueryParams();
        q.AddString("targetActorId", targetActorId);
        if (!string.IsNullOrEmpty(options.Build))
        {
            q.AddString("build", options.Build);
        }

        var body = input is null ? null : Json.Encode(input);
        return new ActorRun(await _ctx.PostWithBodyAsync("metamorph", q, body, options.ContentTypeOrDefault(), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Reboots the run (restarts its container while keeping the same run).</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> RebootAsync(CancellationToken cancellationToken = default)
    {
        return new ActorRun(await _ctx.PostWithBodyAsync("reboot", new QueryParams(), null, "", cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Resurrects a finished run, starting it again from the beginning.</summary>
    /// <param name="options">Optional resurrect options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> ResurrectAsync(RunResurrectOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new RunResurrectOptions()).AppendTo(q);
        return new ActorRun(await _ctx.PostWithBodyAsync("resurrect", q, null, "", cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Charges for a pay-per-event Actor run: records occurrences of a named event. Only meaningful for
    /// runs of pay-per-event Actors.
    /// </summary>
    /// <remarks>
    /// An idempotency key is always sent (auto-generated if not provided), so a charge that is retried by
    /// the transport is applied at most once, matching the reference client.
    /// </remarks>
    /// <param name="options">The charge event details.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task ChargeAsync(RunChargeOptions options, CancellationToken cancellationToken = default)
    {
        if (options.EventName.Length == 0)
        {
            throw new ArgumentException("RunChargeOptions.EventName is required and must not be empty", nameof(options));
        }

        var idempotencyKey = options.IdempotencyKey;
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            idempotencyKey = GenerateIdempotencyKey(options.EventName);
        }

        var body = new JsonObject
        {
            ["eventName"] = options.EventName,
            ["count"] = options.CountValue(),
        };
        using var response = await _http.CallAsync(
            HttpMethod.Post,
            _ctx.SubUrl("charge"),
            Json.Encode(body),
            ResourceContext.ContentTypeJson,
            extraHeaders: new Dictionary<string, string> { [ChargeIdempotencyHeader] = idempotencyKey },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a per-charge idempotency key of the form <c>"{runId}-{eventName}-{millis}-{random}"</c>. It
    /// need not be cryptographically secure, only unique enough to avoid collisions within a request.
    /// </summary>
    private string GenerateIdempotencyKey(string eventName)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}-{1}-{2}-{3}",
            _id,
            eventName,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Random.Shared.Next(0, 1000000));
    }

    /// <summary>
    /// Polls until the run reaches a terminal state or <paramref name="waitSecs"/> elapses (<c>null</c>
    /// waits indefinitely). Returns the latest run.
    /// </summary>
    /// <param name="waitSecs">The wait budget in seconds, or <c>null</c> to wait indefinitely.</param>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    public async Task<ActorRun> WaitForFinishAsync(int? waitSecs = null, CancellationToken cancellationToken = default)
    {
        var data = await _ctx.WaitForFinishAsync(waitSecs, "run", static d => new ActorRun(d).IsTerminal, cancellationToken).ConfigureAwait(false);
        return new ActorRun(data);
    }

    /// <summary>
    /// Waits for the run to finish, optionally redirecting its live log to <paramref name="log"/> for the
    /// duration of the wait. Shared by <c>Actor.Call</c>/<c>Task.Call</c>'s log option.
    /// </summary>
    internal async Task<ActorRun> WaitForFinishWithLogAsync(int? waitSecs, Action<string>? log, CancellationToken cancellationToken)
    {
        if (log is null)
        {
            return await WaitForFinishAsync(waitSecs, cancellationToken).ConfigureAwait(false);
        }

        var streamedLog = GetStreamedLog(log);
        streamedLog.Start();
        try
        {
            return await WaitForFinishAsync(waitSecs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await streamedLog.StopAsync().ConfigureAwait(false);
        }
    }

    // Nested accessors inherit this client's params so last-run status/origin filters (see
    // SetLastRunParams) resolve the intended run's storage/log rather than the latest run's.

    /// <summary>A client for this run's default dataset.</summary>
    public DatasetClient Dataset() => DatasetClient.Nested(_http, _ctx.SubUrl(""), "dataset", _ctx.BaseParams);

    /// <summary>A client for this run's default key-value store.</summary>
    public KeyValueStoreClient KeyValueStore() => KeyValueStoreClient.Nested(_http, _ctx.SubUrl(""), "key-value-store", _ctx.BaseParams);

    /// <summary>A client for this run's default request queue.</summary>
    public RequestQueueClient RequestQueue() => RequestQueueClient.Nested(_http, _ctx.SubUrl(""), "request-queue", _ctx.BaseParams);

    /// <summary>A client for accessing this run's log.</summary>
    public LogClient Log() => LogClient.Nested(_http, _ctx.SubUrl(""), _ctx.BaseParams);

    /// <summary>
    /// Opens a live stream of this run's raw log bytes. The caller reads (and disposes) the returned
    /// stream. For automatic redirection into a sink, prefer <see cref="GetStreamedLog"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<Stream> GetStreamedLogAsync(CancellationToken cancellationToken = default)
        => Log().StreamAsync(new LogOptions { Raw = true }, cancellationToken);

    /// <summary>
    /// Creates a <see cref="StreamedLog"/> that redirects this run's live log to <paramref name="toLog"/>,
    /// one complete message at a time. Call <see cref="StreamedLog.Start"/> to begin and
    /// <see cref="StreamedLog.StopAsync"/> (or dispose it) to end. Consistent with the reference client's
    /// run-log redirection convenience.
    /// </summary>
    /// <param name="toLog">The sink each complete log message is written to.</param>
    /// <param name="fromStart">
    /// If <c>true</c> (default), redirect the whole log including messages from before this call; if
    /// <c>false</c>, skip messages older than the moment the helper is created.
    /// </param>
    public StreamedLog GetStreamedLog(Action<string> toLog, bool fromStart = true)
        => new(Log(), toLog, fromStart);
}
