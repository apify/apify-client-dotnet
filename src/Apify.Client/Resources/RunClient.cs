using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>
/// A client for a specific Actor run: get, update, delete, abort, metamorph, reboot, resurrect, charge and wait for
/// it to finish, plus accessors for its default storages and log.
/// </summary>
public sealed class RunClient
{
    /// <summary>Header the API uses to deduplicate charge requests.</summary>
    private const string ChargeIdempotencyHeader = "idempotency-key";

    private readonly ApifyClient _root;
    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;
    private readonly string _id;

    internal RunClient(ApifyClient root, HttpClientCore http, string baseUrl, string resourcePath, string id)
    {
        _root = root;
        _http = http;
        _id = id;
        _ctx = ResourceContext.Single(http, baseUrl, resourcePath, id);
    }

    /// <summary>
    /// Pins the <c>status</c> and <c>origin</c> query parameters of every call on this client, for the last-run
    /// accessors. Empty values are skipped.
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
    /// Fetches the run, optionally asking the API to wait up to <paramref name="waitForFinishSecs"/> seconds (at most
    /// 60) for it to finish. Returns <c>null</c> if the run does not exist.
    /// </summary>
    /// <param name="waitForFinishSecs">Optional server-side wait in seconds.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun?> GetAsync(int? waitForFinishSecs = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddInt("waitForFinish", _ctx.ClampServerWait(waitForFinishSecs));
        // Long tier: the server may hold the connection for most of the wait.
        var data = await _ctx.GetResourceAsync("", q, RequestTier.Long, cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new ActorRun(obj) : null;
    }

    /// <summary>Updates the run with the given fields and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new ActorRun(await _ctx.UpdateResourceAsync("", newFields, RequestTier.Short, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the run.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", RequestTier.Short, cancellationToken);

    /// <summary>
    /// Aborts the run. <c>true</c> lets it finish the current request first, <c>false</c> aborts at once and <c>null</c>
    /// leaves the choice to the server (an immediate abort).
    /// </summary>
    /// <param name="gracefully">Whether to abort gracefully, or <c>null</c> for the server default.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> AbortAsync(bool? gracefully = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddBool("gracefully", gracefully);
        return new ActorRun(await _ctx.PostWithBodyAsync("abort", q, null, "", RequestTier.Medium, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Transforms the run into a run of another Actor with a new input.</summary>
    /// <param name="targetActorId">The Actor to metamorph into.</param>
    /// <param name="input">The new input: <c>byte[]</c> is sent as it is, anything else as JSON, <c>null</c> for none.</param>
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

        ResourceContext.EncodeInputBody(input, out var body, out var bodyBytes);
        return new ActorRun(await _ctx.PostWithBodyAsync("metamorph", q, body, options.ContentTypeOrDefault(), RequestTier.Medium, cancellationToken, bodyBytes).ConfigureAwait(false));
    }

    /// <summary>Restarts the run's container, keeping the same run.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> RebootAsync(CancellationToken cancellationToken = default)
    {
        return new ActorRun(await _ctx.PostWithBodyAsync("reboot", new QueryParams(), null, "", RequestTier.Medium, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Restarts a finished run (<c>SUCCEEDED</c>, <c>FAILED</c>, <c>ABORTED</c> or <c>TIMED-OUT</c>) in its container,
    /// with the same storages.
    /// </summary>
    /// <param name="options">Optional resurrect options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<ActorRun> ResurrectAsync(RunResurrectOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new RunResurrectOptions()).AppendTo(q);
        return new ActorRun(await _ctx.PostWithBodyAsync("resurrect", q, null, "", RequestTier.Medium, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Charges for events of a pay-per-event Actor run.</summary>
    /// <remarks>
    /// The request always carries an idempotency key (generated when not given), so a charge the transport retries is
    /// applied once.
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
        if (!string.IsNullOrEmpty(options.RequestId))
        {
            body["requestId"] = options.RequestId;
        }

        using var response = await _http.CallAsync(
            HttpMethod.Post,
            _ctx.SubUrl("charge"),
            Json.Encode(body),
            ResourceContext.ContentTypeJson,
            timeout: _ctx.RequestTimeout,
            extraHeaders: new Dictionary<string, string> { [ChargeIdempotencyHeader] = idempotencyKey },
            tier: RequestTier.Short,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds an idempotency key <c>{runId}-{eventName}-{millis}-{random}</c>; it only has to be unique, not secure.</summary>
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
    /// Polls until the run reaches a terminal state or <paramref name="waitSecs"/> elapses (<c>null</c> waits
    /// indefinitely) and returns the latest run.
    /// </summary>
    /// <param name="waitSecs">The wait budget in seconds, or <c>null</c> to wait indefinitely.</param>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    public async Task<ActorRun> WaitForFinishAsync(int? waitSecs = null, CancellationToken cancellationToken = default)
    {
        var data = await _ctx.WaitForFinishAsync(waitSecs, "run", static d => new ActorRun(d).IsTerminal, cancellationToken).ConfigureAwait(false);
        return new ActorRun(data);
    }

    /// <summary>
    /// Builds the default log sink of the reference client: lines prefixed with <c>&lt;Actor name&gt; runId:&lt;id&gt; -&gt; </c>.
    /// </summary>
    private async Task<Action<string>> CreateDefaultSinkAsync(CancellationToken cancellationToken)
    {
        var run = await GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var actorId = run?.ActId ?? string.Empty;
        var actor = actorId.Length == 0 ? null : await _root.Actor(actorId).GetAsync(cancellationToken).ConfigureAwait(false);
        return StreamedLog.CreatePrefixedSink(run?.Id ?? _id, actor?.Name);
    }

    /// <summary>Waits for the run to finish, redirecting its live log to <paramref name="log"/> meanwhile.</summary>
    internal async Task<ActorRun> WaitForFinishWithLogAsync(int? waitSecs, Action<string>? log, CancellationToken cancellationToken)
    {
        if (log is null)
        {
            return await WaitForFinishAsync(waitSecs, cancellationToken).ConfigureAwait(false);
        }

        if (ReferenceEquals(log, StreamedLog.DefaultConsole))
        {
            log = await CreateDefaultSinkAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Lazily iterates the items of the run's default dataset while the run is still producing them, as the reference
    /// client's <c>iterateDatasetItems()</c> does.
    /// </summary>
    /// <remarks>
    /// While the run is active, each poll yields the rows below the dataset's <c>itemCount</c>, then waits up to
    /// <see cref="RunIterateDatasetItemsOptions.PollIntervalSecs"/> for the run to finish. <c>itemCount</c> lags a few
    /// seconds behind the pushed items, so after the run finishes the remaining rows are read page by page. A last-run
    /// client sticks to the run its first request resolved to.
    /// </remarks>
    /// <param name="options">Optional pagination, filtering and polling options.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public async IAsyncEnumerable<JsonNode?> IterateDatasetItemsAsync(
        RunIterateDatasetItemsOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new RunIterateDatasetItemsOptions();
        PageSizing.ValidateChunkSize(options.ChunkSize, nameof(RunIterateDatasetItemsOptions.ChunkSize));
        var chunkSize = options.ChunkSize ?? RunIterateDatasetItemsOptions.DefaultChunkSize;
        var pollIntervalSecs = options.PollIntervalSecs ?? RunIterateDatasetItemsOptions.DefaultPollIntervalSecs;
        ArgumentOutOfRangeException.ThrowIfNegative(pollIntervalSecs, nameof(RunIterateDatasetItemsOptions.PollIntervalSecs));
        ArgumentOutOfRangeException.ThrowIfNegative(options.Offset ?? 0, nameof(RunIterateDatasetItemsOptions.Offset));
        ArgumentOutOfRangeException.ThrowIfNegative(options.Limit ?? 0, nameof(RunIterateDatasetItemsOptions.Limit));

        var position = options.Offset ?? 0;
        // Wider than int: Offset + Limit can exceed int.MaxValue.
        long? end = options.Limit is > 0 ? (long)position + options.Limit.Value : null;

        var run = await GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        // A last-run client resolves `runs/last` on each request; pin the run of the first one so a newer run cannot swap the dataset.
        var runClient = this;
        if (run?.Id is { } resolvedId && resolvedId != _id)
        {
            runClient = _root.Run(resolvedId);
            if (_ctx.RequestTimeout is { } timeout)
            {
                runClient.WithTimeout(timeout);
            }
        }

        var datasetClient = runClient.Dataset();

        while (true)
        {
            var isFinished = run is null || run.IsTerminal;
            var dataset = await datasetClient.GetAsync(cancellationToken).ConfigureAwait(false);
            var itemCount = dataset?.ItemCount ?? 0;
            if (end is not null)
            {
                itemCount = Math.Min(itemCount, end.Value);
            }

            while (position < itemCount)
            {
                var pageLimit = (int)Math.Min(chunkSize, itemCount - position);
                var page = await datasetClient.FetchItemsPageAsync(ItemsPageOptions(options, position, pageLimit), cancellationToken).ConfigureAwait(false);
                foreach (var item in page.Items)
                {
                    yield return item;
                }

                position += pageLimit;
            }

            if (end is not null && position >= end)
            {
                yield break;
            }

            if (isFinished)
            {
                break;
            }

            run = await runClient.WaitForFinishAsync(pollIntervalSecs, cancellationToken).ConfigureAwait(false);
        }

        // The run is finished, but `itemCount` lags the pushed items: read the remaining rows page by page.
        while (true)
        {
            var pageLimit = end is not null ? (int)Math.Min(chunkSize, end.Value - position) : chunkSize;
            var page = await datasetClient.FetchItemsPageAsync(ItemsPageOptions(options, position, pageLimit), cancellationToken).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                yield return item;
            }

            // Only a page that returned nothing and scanned no rows ends the iteration, since filters can shorten a full page.
            // When a filter could have hidden every row past a lagging `itemCount`, a plain one-row read confirms nothing is left.
            if (page.Count == 0
                && (!options.HasRowFilters || await IsDatasetExhaustedAsync(datasetClient, position, cancellationToken).ConfigureAwait(false)))
            {
                yield break;
            }

            position += pageLimit;
            if (end is not null && position >= end)
            {
                yield break;
            }
        }
    }

    /// <summary>Builds the dataset-items options for one page of <see cref="IterateDatasetItemsAsync"/>.</summary>
    private static DatasetListItemsOptions ItemsPageOptions(RunIterateDatasetItemsOptions options, int offset, int limit) => new()
    {
        Offset = offset,
        Limit = limit,
        Clean = options.Clean,
        Fields = options.Fields,
        Omit = options.Omit,
        Unwind = options.Unwind,
        SkipEmpty = options.SkipEmpty,
        SkipHidden = options.SkipHidden,
    };

    /// <summary>Checks, without any filter, whether the dataset has no row at or after <paramref name="position"/>.</summary>
    private static async Task<bool> IsDatasetExhaustedAsync(DatasetClient datasetClient, int position, CancellationToken cancellationToken)
    {
        var plain = new DatasetListItemsOptions { Offset = position, Limit = 1 };
        var page = await datasetClient.FetchItemsPageAsync(plain, cancellationToken).ConfigureAwait(false);
        return page.Items.Count == 0;
    }

    // Nested accessors inherit this client's params and timeout, so last-run filters resolve the intended run.

    /// <summary>A client for this run's default dataset.</summary>
    public DatasetClient Dataset() => DatasetClient.Nested(_http, _ctx.SubUrl(""), "dataset", _ctx.BaseParams, _ctx.RequestTimeout);

    /// <summary>A client for this run's default key-value store.</summary>
    public KeyValueStoreClient KeyValueStore() => KeyValueStoreClient.Nested(_http, _ctx.SubUrl(""), "key-value-store", _ctx.BaseParams, _ctx.RequestTimeout);

    /// <summary>A client for this run's default request queue.</summary>
    public RequestQueueClient RequestQueue() => RequestQueueClient.Nested(_http, _ctx.SubUrl(""), "request-queue", _ctx.BaseParams, _ctx.RequestTimeout);

    /// <summary>A client for accessing this run's log.</summary>
    public LogClient Log() => LogClient.Nested(_http, _ctx.SubUrl(""), _ctx.BaseParams, _ctx.RequestTimeout);

    /// <summary>
    /// Opens a live stream of the run's raw log bytes; the caller disposes it. Prefer <see cref="GetStreamedLog"/> to
    /// redirect the log to a sink.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Stream> GetStreamedLogAsync(CancellationToken cancellationToken = default)
    {
        // Log() never resolves a 404 to null, since the run itself may be missing, so the stream is not null.
        var stream = await Log().StreamAsync(new LogOptions { Raw = true }, cancellationToken).ConfigureAwait(false);
        return stream!;
    }

    /// <summary>
    /// Creates a <see cref="StreamedLog"/> that redirects this run's live log to <paramref name="toLog"/>, one complete
    /// message at a time. Call <see cref="StreamedLog.Start"/> to begin and <see cref="StreamedLog.StopAsync"/> (or
    /// dispose it) to end.
    /// </summary>
    /// <param name="toLog">
    /// The sink for each complete message, used as it is. Unlike in <c>CallAsync</c> there is no prefix; use
    /// <see cref="StreamedLog.CreatePrefixedSink"/> for the <c>&lt;Actor name&gt; runId:&lt;id&gt; -&gt; </c> prefix.
    /// </param>
    /// <param name="fromStart">
    /// If <c>true</c> (default), redirect the whole log; if <c>false</c>, skip messages older than the moment of this call.
    /// </param>
    public StreamedLog GetStreamedLog(Action<string> toLog, bool fromStart = true)
        => new(Log(), toLog, fromStart);

    /// <summary>
    /// Returns this client with the timeout of every subsequent call set to <paramref name="timeout"/>, overriding the
    /// tier default (see "Timeout tiers" in docs/README.md). <see cref="TimeSpan.Zero"/> means no timeout. A value above
    /// <see cref="ApifyClientOptions.TimeoutSecs"/> is capped at it.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public RunClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
