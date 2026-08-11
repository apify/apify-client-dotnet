using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Exceptions;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for a specific request queue (and run-nested variants).</summary>
public sealed class RequestQueueClient
{
    /// <summary>The API limit on requests per batch call; larger inputs are split into chunks of this size.</summary>
    private const int MaxRequestsPerBatch = 25;

    /// <summary>
    /// The API's maximum accepted request payload size (9 MiB). Batches are additionally split so no single
    /// batch call exceeds this, matching the reference client's <c>sliceArrayByByteLength</c>.
    /// </summary>
    private const int MaxPayloadSizeBytes = 9 * 1024 * 1024;

    /// <summary>Safety margin (0.01%) subtracted from the payload limit, matching the reference client.</summary>
    private const double PayloadSafetyBufferPercent = 0.0001;

    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;
    private readonly string? _clientKey;
    private readonly TimeSpan? _timeout;

    private RequestQueueClient(HttpClientCore http, ResourceContext ctx, string? clientKey, TimeSpan? timeout)
    {
        _http = http;
        _ctx = ctx;
        _clientKey = clientKey;
        _timeout = timeout;
    }

    internal static RequestQueueClient ForId(HttpClientCore http, string baseUrl, string id, RequestQueueClientOptions? options)
    {
        var ctx = ResourceContext.Single(http, baseUrl, "request-queues", id);
        var timeout = options?.TimeoutSecs is not null ? TimeSpan.FromSeconds(options.TimeoutSecs.Value) : (TimeSpan?)null;
        ctx.WithTimeout(timeout);
        return new RequestQueueClient(http, ctx, options?.ClientKey, timeout);
    }

    internal static RequestQueueClient Nested(HttpClientCore http, string baseUrl, string subPath, QueryParams? inheritedParams = null)
        => new(http, ResourceContext.Collection(http, baseUrl, subPath, inheritedParams), null, null);

    /// <summary>
    /// Returns a copy of the client that identifies its requests with <paramref name="clientKey"/>. A
    /// stable client key is required to operate on locks the client itself created, and lets the API detect
    /// whether multiple clients access a queue.
    /// </summary>
    /// <param name="clientKey">The stable client key.</param>
    public RequestQueueClient WithClientKey(string clientKey) => new(_http, _ctx, clientKey, _timeout);

    /// <summary>Fetches the queue metadata, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueue?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new RequestQueue(obj) : null;
    }

    /// <summary>Updates the queue metadata (e.g. name) and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueue> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new RequestQueue(await _ctx.UpdateResourceAsync("", newFields, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the queue.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", cancellationToken);

    /// <summary>
    /// Returns the requests at the head (front) of the queue, up to <paramref name="limit"/> (<c>null</c>
    /// for the server default).
    /// </summary>
    /// <param name="limit">The maximum number of requests to return.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueueHead> ListHeadAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddInt("limit", limit);
        ApplyClientKey(q);
        return RequestQueueHead.FromData(await _ctx.GetResourceRequiredAsync("head", q, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Adds a request to the queue. If <paramref name="forefront"/> is true, it is added to the front.</summary>
    /// <param name="request">The request to add.</param>
    /// <param name="forefront">Whether to add to the front of the queue.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueueOperationInfo> AddRequestAsync(RequestQueueRequest request, bool forefront = false, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddBool("forefront", forefront);
        ApplyClientKey(q);
        var data = await _ctx.PostWithBodyAsync("requests", q, Json.Encode(request.ToJsonObject()), ResourceContext.ContentTypeJson, cancellationToken).ConfigureAwait(false);
        return new RequestQueueOperationInfo(data);
    }

    /// <summary>Fetches a request by ID, or <c>null</c> if it does not exist.</summary>
    /// <param name="id">The request ID.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueueRequest?> GetRequestAsync(string id, CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("requests/" + ResourceContext.EncodePathSegment(id), new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? RequestQueueRequest.FromJsonObject(obj) : null;
    }

    /// <summary>
    /// Updates an existing request (identified by its ID field) and returns the operation info. If
    /// <paramref name="forefront"/> is true, the request is moved to the front of the queue.
    /// </summary>
    /// <param name="request">The request to update (must have an ID).</param>
    /// <param name="forefront">Whether to move the request to the front.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueueOperationInfo> UpdateRequestAsync(RequestQueueRequest request, bool forefront = false, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddBool("forefront", forefront);
        ApplyClientKey(q);
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("requests/" + ResourceContext.EncodePathSegment(request.Id ?? string.Empty)));
        using var response = await _http.CallAsync(HttpMethod.Put, url, Json.Encode(request.ToJsonObject()), ResourceContext.ContentTypeJson, _timeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        var data = Json.DecodeData(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return new RequestQueueOperationInfo(data as JsonObject ?? new JsonObject());
    }

    /// <summary>Deletes a request by ID.</summary>
    /// <param name="id">The request ID.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task DeleteRequestAsync(string id, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        ApplyClientKey(q);
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("requests/" + ResourceContext.EncodePathSegment(id)));
        try
        {
            using var response = await _http.CallAsync(HttpMethod.Delete, url, timeout: _timeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
        {
            // A missing request is a successful no-op for delete.
        }
    }

    /// <summary>
    /// Atomically returns and locks up to <paramref name="limit"/> requests from the head of the queue for
    /// <paramref name="lockSecs"/> seconds.
    /// </summary>
    /// <param name="lockSecs">How long to lock the returned requests, in seconds.</param>
    /// <param name="limit">The maximum number of requests to lock.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<LockedRequestQueueHead> ListAndLockHeadAsync(int lockSecs, int? limit = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddInt("lockSecs", lockSecs).AddInt("limit", limit);
        ApplyClientKey(q);
        var data = await _ctx.PostWithBodyAsync("head/lock", q, null, "", cancellationToken).ConfigureAwait(false);
        return LockedRequestQueueHead.FromData(data);
    }

    /// <summary>
    /// Adds multiple requests to the queue. If <paramref name="forefront"/> is true, they are added to the
    /// front.
    /// </summary>
    /// <remarks>
    /// The input is automatically split into chunks of at most 25 requests (the API count limit) that
    /// additionally respect the API's ~9 MiB payload-size limit. Chunks are dispatched using up to
    /// <see cref="BatchAddRequestsOptions.MaxParallel"/> concurrent API calls (set it to 1 for sequential
    /// dispatch). Requests the API returns as unprocessed in a successful response (typically rate-limited)
    /// are retried with exponential backoff; the per-chunk results are merged in input order. Every request
    /// must carry a non-empty <c>UniqueKey</c>. Consistent with the reference client, this method does not
    /// throw on API errors: if a batch call fails and the transport did not retry, that chunk's
    /// not-yet-processed requests are returned in <see cref="BatchAddResult.UnprocessedRequests"/>. Invalid
    /// input (empty uniqueKey, oversized request) is rejected up front with <see cref="ArgumentException"/>.
    /// </remarks>
    /// <param name="requests">The requests to add.</param>
    /// <param name="forefront">Whether to add to the front of the queue.</param>
    /// <param name="options">Optional batch-add tuning.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<BatchAddResult> BatchAddRequestsAsync(
        IReadOnlyList<RequestQueueRequest> requests,
        bool forefront = false,
        BatchAddRequestsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BatchAddRequestsOptions();
        var list = new List<RequestQueueRequest>(requests);

        for (var i = 0; i < list.Count; i++)
        {
            if (string.IsNullOrEmpty(list[i].UniqueKey))
            {
                throw new ArgumentException(
                    $"BatchAddRequests: the request at index {i} is missing a non-empty UniqueKey", nameof(requests));
            }
        }

        var payloadSizeLimitBytes = MaxPayloadSizeBytes - (int)Math.Ceiling(MaxPayloadSizeBytes * PayloadSafetyBufferPercent);

        // Pre-compute all chunks up front (bounded first by the count limit of 25, then by payload byte size)
        // so they can be dispatched sequentially or with bounded parallelism.
        var chunks = new List<List<RequestQueueRequest>>();
        var index = 0;
        while (index < list.Count)
        {
            var countSlice = list.GetRange(index, Math.Min(MaxRequestsPerBatch, list.Count - index));
            var chunk = SliceByByteLength(countSlice, payloadSizeLimitBytes, index);
            chunks.Add(chunk);
            index += chunk.Count;
        }

        var merged = new BatchAddResult();
        if (chunks.Count == 0)
        {
            return merged;
        }

        // Sequential path when parallelism is disabled or there is only a single chunk.
        if (options.MaxParallel <= 1 || chunks.Count == 1)
        {
            foreach (var chunk in chunks)
            {
                merged.Merge(await BatchAddChunkWithRetriesAsync(chunk, forefront, options, cancellationToken).ConfigureAwait(false));
            }

            return merged;
        }

        // Bounded-parallel dispatch: at most MaxParallel chunk calls run concurrently, gated by a semaphore.
        // Results are merged in chunk (input) order so the output stays deterministic regardless of which
        // chunk finishes first.
        using var gate = new SemaphoreSlim(options.MaxParallel);
        var tasks = new List<Task<BatchAddResult>>(chunks.Count);
        foreach (var chunk in chunks)
        {
            tasks.Add(DispatchChunkAsync(chunk, forefront, options, gate, cancellationToken));
        }

        foreach (var result in await Task.WhenAll(tasks).ConfigureAwait(false))
        {
            merged.Merge(result);
        }

        return merged;
    }

    /// <summary>Runs one chunk's add-with-retries under the concurrency gate.</summary>
    private async Task<BatchAddResult> DispatchChunkAsync(
        List<RequestQueueRequest> chunk,
        bool forefront,
        BatchAddRequestsOptions options,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await BatchAddChunkWithRetriesAsync(chunk, forefront, options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Returns the longest leading run of <paramref name="requests"/> whose combined JSON payload stays
    /// under <paramref name="maxByteLength"/>, always keeping at least one request so iteration makes
    /// progress. Ports the reference client's <c>sliceArrayByByteLength</c>.
    /// </summary>
    private static List<RequestQueueRequest> SliceByByteLength(List<RequestQueueRequest> requests, int maxByteLength, int startIndex)
    {
        var payloads = ToPayload(requests);

        if (Encoding.UTF8.GetByteCount(Json.Encode(payloads)) < maxByteLength)
        {
            return requests;
        }

        var sliced = new List<RequestQueueRequest>();
        var byteLength = 2; // the two bytes of an empty array "[]"
        for (var i = 0; i < requests.Count; i++)
        {
            var itemBytes = Encoding.UTF8.GetByteCount(Json.Encode(requests[i].ToJsonObject()));
            if (itemBytes > maxByteLength)
            {
                throw new ArgumentException(
                    $"BatchAddRequests: the request at index {startIndex + i} exceeds the maximum payload size ({maxByteLength} bytes)");
            }

            if (byteLength + itemBytes >= maxByteLength)
            {
                break;
            }

            byteLength += itemBytes;
            sliced.Add(requests[i]);
        }

        // Guarantee forward progress: keep at least the first request (it fits under the hard max).
        if (sliced.Count == 0)
        {
            sliced.Add(requests[0]);
        }

        return sliced;
    }

    private async Task<BatchAddResult> BatchAddChunkWithRetriesAsync(List<RequestQueueRequest> chunk, bool forefront, BatchAddRequestsOptions options, CancellationToken cancellationToken)
    {
        var maxRetries = options.MaxUnprocessedRequestsRetries;
        var minDelayMillis = options.MinDelayBetweenUnprocessedRequestsRetriesMillis;

        var remaining = chunk;
        var processed = new List<RequestQueueOperationInfo>();
        var unprocessed = new List<RequestQueueRequest>();

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            BatchAddResult response;
            try
            {
                response = await BatchAddChunkAsync(remaining, forefront, cancellationToken).ConfigureAwait(false);
            }
            catch (ApifyApiException)
            {
                // Matches the JS reference: when the HTTP call fails and the transport did not (or was told
                // not to) retry, the requests not yet processed in THIS chunk are reported as unprocessed and
                // we stop — keeping the method's non-throwing contract so a multi-chunk call still returns
                // every earlier chunk's already-merged results instead of aborting the whole operation.
                unprocessed = RequestsNotYetProcessed(chunk, processed);
                break;
            }

            processed.AddRange(response.ProcessedRequests);
            // Only requests the API reports as unprocessed in this SUCCESSFUL response are retried.
            unprocessed = new List<RequestQueueRequest>(response.UnprocessedRequests);
            remaining = RequestsNotYetProcessed(chunk, processed);
            if (remaining.Count == 0)
            {
                break;
            }

            if (attempt < maxRetries)
            {
                await SleepBackoffAsync(attempt, minDelayMillis, cancellationToken).ConfigureAwait(false);
            }
        }

        var result = new BatchAddResult();
        result.SetProcessedRequests(processed);
        result.SetUnprocessedRequests(unprocessed);
        return result;
    }

    private async Task<BatchAddResult> BatchAddChunkAsync(List<RequestQueueRequest> requests, bool forefront, CancellationToken cancellationToken)
    {
        var q = new QueryParams();
        q.AddBool("forefront", forefront);
        ApplyClientKey(q);
        var data = await _ctx.PostWithBodyAsync("requests/batch", q, Json.Encode(ToPayload(requests)), ResourceContext.ContentTypeJson, cancellationToken).ConfigureAwait(false);

        var processed = new List<RequestQueueOperationInfo>();
        if (data.TryGetPropertyValue("processedRequests", out var pNode) && pNode is JsonArray pArray)
        {
            foreach (var item in pArray)
            {
                processed.Add(new RequestQueueOperationInfo(item as JsonObject ?? new JsonObject()));
            }
        }

        var unprocessed = new List<RequestQueueRequest>();
        if (data.TryGetPropertyValue("unprocessedRequests", out var uNode) && uNode is JsonArray uArray)
        {
            foreach (var item in uArray)
            {
                unprocessed.Add(RequestQueueRequest.FromJsonObject(item as JsonObject ?? new JsonObject()));
            }
        }

        return new BatchAddResult(processed, unprocessed);
    }

    private static List<RequestQueueRequest> RequestsNotYetProcessed(List<RequestQueueRequest> chunk, List<RequestQueueOperationInfo> processed)
    {
        var processedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var info in processed)
        {
            if (info.UniqueKey is not null)
            {
                processedKeys.Add(info.UniqueKey);
            }
        }

        var remaining = new List<RequestQueueRequest>();
        foreach (var request in chunk)
        {
            if (!processedKeys.Contains(request.UniqueKey ?? string.Empty))
            {
                remaining.Add(request);
            }
        }

        return remaining;
    }

    private static Task SleepBackoffAsync(int attempt, int minDelayMillis, CancellationToken cancellationToken)
    {
        if (minDelayMillis <= 0)
        {
            return Task.CompletedTask;
        }

        // (1 + random) * 2^attempt * minDelay — exponential backoff with jitter, matching the reference.
        var factor = (1 + Random.Shared.NextDouble()) * Math.Pow(2, attempt);
        var delayMillis = (int)Math.Floor(factor * minDelayMillis);
        return Task.Delay(delayMillis, cancellationToken);
    }

    /// <summary>
    /// Deletes multiple requests in a single call. Each entry must have its <see cref="RequestQueueRequest.Id"/>
    /// and/or <see cref="RequestQueueRequest.UniqueKey"/> set to identify the request to delete; other fields
    /// are ignored.
    /// </summary>
    /// <param name="requests">The requests to delete, identified by <c>Id</c> and/or <c>UniqueKey</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<BatchDeleteResult> BatchDeleteRequestsAsync(IReadOnlyList<RequestQueueRequest> requests, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        ApplyClientKey(q);
        var data = await _ctx.DeleteWithBodyAsync("requests/batch", q, ToPayload(requests), cancellationToken).ConfigureAwait(false);
        return BatchDeleteResult.FromData(data);
    }

    /// <summary>Encodes a list of requests as their raw JSON objects, for a batch request body.</summary>
    private static List<JsonObject> ToPayload(IEnumerable<RequestQueueRequest> requests)
    {
        var payload = new List<JsonObject>();
        foreach (var r in requests)
        {
            payload.Add(r.ToJsonObject());
        }

        return payload;
    }

    /// <summary>Lists the queue's requests with pagination.</summary>
    /// <param name="options">Optional listing filters and pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueueRequestsPage> ListRequestsAsync(ListRequestsOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ListRequestsOptions();
        options.Validate();
        var q = new QueryParams();
        options.AppendTo(q);
        ApplyClientKey(q);
        var data = await _ctx.GetResourceRequiredAsync("requests", q, cancellationToken).ConfigureAwait(false);
        return RequestQueueRequestsPage.FromData(data);
    }

    /// <summary>
    /// Extends the lock on a request by <paramref name="lockSecs"/> seconds. If <paramref name="forefront"/>
    /// is true, the request is moved to the front when its lock expires.
    /// </summary>
    /// <param name="id">The request ID.</param>
    /// <param name="lockSecs">How much longer to hold the lock, in seconds.</param>
    /// <param name="forefront">Whether to move the request to the front when the lock expires.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestLockInfo> ProlongRequestLockAsync(string id, int lockSecs, bool forefront = false, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddInt("lockSecs", lockSecs).AddBool("forefront", forefront);
        ApplyClientKey(q);
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("requests/" + ResourceContext.EncodePathSegment(id) + "/lock"));
        using var response = await _http.CallAsync(HttpMethod.Put, url, null, "", _timeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        var data = Json.DecodeData(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return RequestLockInfo.FromData(data);
    }

    /// <summary>
    /// Releases the lock on a request. If <paramref name="forefront"/> is true, the request is moved to the
    /// front of the queue.
    /// </summary>
    /// <param name="id">The request ID.</param>
    /// <param name="forefront">Whether to move the request to the front.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task DeleteRequestLockAsync(string id, bool forefront = false, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddBool("forefront", forefront);
        ApplyClientKey(q);
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("requests/" + ResourceContext.EncodePathSegment(id) + "/lock"));
        try
        {
            using var response = await _http.CallAsync(HttpMethod.Delete, url, timeout: _timeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
        {
            // A missing lock is a successful no-op.
        }
    }

    /// <summary>Releases all locks the client holds on this queue's requests.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<UnlockRequestsResult> UnlockRequestsAsync(CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        ApplyClientKey(q);
        var data = await _ctx.PostWithBodyAsync("requests/unlock", q, null, "", cancellationToken).ConfigureAwait(false);
        return UnlockRequestsResult.FromData(data);
    }

    /// <summary>
    /// Lazily iterates over the queue's requests, transparently following pagination.
    /// </summary>
    /// <remarks>
    /// With no options it fetches pages of up to <see cref="PaginateRequestsOptions.DefaultMaxPageLimit"/>
    /// requests until the queue is exhausted. The options mirror the reference client: <c>Limit</c> caps the
    /// total number of requests yielded across all pages, <c>MaxPageLimit</c> caps the page size,
    /// <c>ExclusiveStartId</c>/<c>Cursor</c> choose the starting point (first page only), and <c>Filter</c>
    /// restricts to locked/pending requests.
    /// </remarks>
    /// <param name="options">Optional iteration options.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public async IAsyncEnumerable<RequestQueueRequest> PaginateRequestsAsync(
        PaginateRequestsOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new PaginateRequestsOptions();
        options.Validate();

        var maxPageLimit = options.MaxPageLimit ?? PaginateRequestsOptions.DefaultMaxPageLimit;
        var limit = options.Limit; // total across all pages; null = unbounded
        var nextCursor = options.Cursor;
        var nextExclusiveStartId = options.ExclusiveStartId; // used for the first page only
        var iterated = 0;

        while (true)
        {
            var pageLimit = limit is not null ? Math.Min(maxPageLimit, limit.Value - iterated) : maxPageLimit;

            var page = await ListRequestsAsync(
                new ListRequestsOptions
                {
                    Limit = pageLimit,
                    ExclusiveStartId = nextExclusiveStartId,
                    Cursor = nextCursor,
                    Filter = options.Filter,
                },
                cancellationToken).ConfigureAwait(false);

            if (page.Items.Count == 0)
            {
                yield break;
            }

            foreach (var item in page.Items)
            {
                yield return item;
            }

            iterated += page.Items.Count;

            nextCursor = page.NextCursor;
            if ((limit is not null && iterated >= limit.Value) || string.IsNullOrEmpty(nextCursor))
            {
                yield break;
            }

            // After the first page, paginate purely by cursor.
            nextExclusiveStartId = null;
        }
    }

    private void ApplyClientKey(QueryParams q)
    {
        if (!string.IsNullOrEmpty(_clientKey))
        {
            q.AddString("clientKey", _clientKey);
        }
    }
}
