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

/// <summary>A client for a specific request queue.</summary>
public sealed class RequestQueueClient
{
    /// <summary>The API limit on requests per batch call.</summary>
    private const int MaxRequestsPerBatch = 25;

    /// <summary>
    /// The API's maximum request payload size (9 MiB). Batches are also split to stay below it, as in the reference
    /// client's <c>splitIntoJsonArrayBatches</c>.
    /// </summary>
    private const int MaxPayloadSizeBytes = 9 * 1024 * 1024;

    /// <summary>Safety margin (0.01%) subtracted from the payload limit, as in the reference client.</summary>
    private const double PayloadSafetyBufferPercent = 0.0001;

    /// <summary>The brackets that wrap a JSON array body.</summary>
    private const int JsonArrayBracketBytes = 2;

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

    internal static RequestQueueClient Nested(HttpClientCore http, string baseUrl, string subPath, QueryParams? inheritedParams = null, TimeSpan? timeout = null)
        => new(http, ResourceContext.NestedSingleton(http, baseUrl, subPath, inheritedParams).WithTimeout(timeout), null, timeout);

    /// <summary>
    /// Returns a copy of the client that identifies its requests with <paramref name="clientKey"/>. A stable key is
    /// needed to operate on locks the client itself created.
    /// </summary>
    /// <param name="clientKey">The stable client key.</param>
    public RequestQueueClient WithClientKey(string clientKey) => new(_http, _ctx, clientKey, _timeout);

    /// <summary>Fetches the queue metadata, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueue?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new RequestQueue(obj) : null;
    }

    /// <summary>Updates the queue metadata, such as its name, and returns the updated queue.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueue> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new RequestQueue(await _ctx.UpdateResourceAsync("", newFields, RequestTier.Short, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the queue.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", RequestTier.Short, cancellationToken);

    /// <summary>
    /// Returns up to <paramref name="limit"/> requests from the head of the queue (<c>null</c> for the server default).
    /// </summary>
    /// <param name="limit">The maximum number of requests to return.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueueHead> ListHeadAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddInt("limit", limit);
        ApplyClientKey(q);
        return RequestQueueHead.FromData(await _ctx.GetResourceRequiredAsync("head", q, RequestTier.Short, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Adds a request to the queue, to the front if <paramref name="forefront"/> is true.</summary>
    /// <param name="request">The request to add.</param>
    /// <param name="forefront">Whether to add to the front of the queue.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueueOperationInfo> AddRequestAsync(RequestQueueRequest request, bool forefront = false, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddBool("forefront", forefront);
        ApplyClientKey(q);
        var data = await _ctx.PostWithBodyAsync("requests", q, Json.Encode(request.ToJsonObject()), ResourceContext.ContentTypeJson, RequestTier.Short, cancellationToken).ConfigureAwait(false);
        return new RequestQueueOperationInfo(data);
    }

    /// <summary>Fetches a request by ID, or <c>null</c> if it does not exist.</summary>
    /// <param name="id">The request ID.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueueRequest?> GetRequestAsync(string id, CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("requests/" + ResourceContext.EncodePathSegment(id, nameof(id)), new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? RequestQueueRequest.FromJsonObject(obj) : null;
    }

    /// <summary>
    /// Updates an existing request (identified by its ID) and moves it to the front of the queue if
    /// <paramref name="forefront"/> is true.
    /// </summary>
    /// <param name="request">The request to update (must have an ID).</param>
    /// <param name="forefront">Whether to move the request to the front.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<RequestQueueOperationInfo> UpdateRequestAsync(RequestQueueRequest request, bool forefront = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.Id))
        {
            throw new ArgumentException("the request to update must have an Id", nameof(request));
        }

        var q = new QueryParams();
        q.AddBool("forefront", forefront);
        ApplyClientKey(q);
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("requests/" + ResourceContext.EncodePathSegment(request.Id, nameof(request))));
        using var response = await _http.CallAsync(HttpMethod.Put, url, Json.Encode(request.ToJsonObject()), ResourceContext.ContentTypeJson, _timeout, tier: RequestTier.Medium, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new RequestQueueOperationInfo(await response.ReadDataObjectAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes a request by ID.</summary>
    /// <param name="id">The request ID.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task DeleteRequestAsync(string id, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        ApplyClientKey(q);
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("requests/" + ResourceContext.EncodePathSegment(id, nameof(id))));
        try
        {
            using var response = await _http.CallAsync(HttpMethod.Delete, url, timeout: _timeout, tier: RequestTier.Short, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
        {
            // A missing resource is a no-op.
        }
    }

    /// <summary>
    /// Returns up to <paramref name="limit"/> requests from the head of the queue and locks them for
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
        var data = await _ctx.PostWithBodyAsync("head/lock", q, null, "", RequestTier.Medium, cancellationToken).ConfigureAwait(false);
        return LockedRequestQueueHead.FromData(data);
    }

    /// <summary>Adds multiple requests to the queue, to the front if <paramref name="forefront"/> is true.</summary>
    /// <remarks>
    /// The input is split into chunks of at most 25 requests that also stay below the API's payload limit. Up to
    /// <see cref="BatchAddRequestsOptions.MaxParallel"/> chunks are sent concurrently (1 for sequential). Requests the API
    /// returns as unprocessed are retried with exponential backoff, and the results are merged in input order. Every
    /// request needs a non-empty <c>UniqueKey</c>; invalid input throws <see cref="ArgumentException"/> before anything
    /// is sent. As in the reference client, an API error does not throw: the requests of the failed chunk are returned in
    /// <see cref="BatchAddResult.UnprocessedRequests"/>.
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

        // Requests are serialized once: the byte lengths decide the batch boundaries and the same strings form the batch
        // bodies. All chunks are planned first, so an oversized request is rejected before any batch is sent.
        var serialized = new List<SerializedRequest>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var json = Json.Encode(list[i].ToJsonObject());
            var byteLength = Encoding.UTF8.GetByteCount(json);
            if (byteLength + JsonArrayBracketBytes > payloadSizeLimitBytes)
            {
                throw new ArgumentException(
                    $"BatchAddRequests: the request at index {i} exceeds the maximum payload size ({payloadSizeLimitBytes} bytes)", nameof(requests));
            }

            serialized.Add(new SerializedRequest(list[i], json, byteLength));
        }

        var chunks = SplitIntoJsonArrayBatches(serialized, payloadSizeLimitBytes);

        var merged = new BatchAddResult();
        if (chunks.Count == 0)
        {
            return merged;
        }

        // Sequential when parallelism is off or there is a single chunk.
        if (options.MaxParallel <= 1 || chunks.Count == 1)
        {
            foreach (var chunk in chunks)
            {
                merged.Merge(await BatchAddChunkWithRetriesAsync(chunk, forefront, options, cancellationToken).ConfigureAwait(false));
            }

            return merged;
        }

        // At most MaxParallel chunks run at once. Results are merged in input order, whichever chunk finishes first.
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

    /// <summary>Adds one chunk with retries, under the concurrency gate.</summary>
    private async Task<BatchAddResult> DispatchChunkAsync(
        List<SerializedRequest> chunk,
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

    /// <summary>A request with its JSON and the UTF-8 size of that JSON.</summary>
    private readonly record struct SerializedRequest(RequestQueueRequest Request, string Json, int ByteLength);

    /// <summary>
    /// Splits serialized requests into batches of at most <see cref="MaxRequestsPerBatch"/> requests and
    /// <paramref name="maxByteLength"/> bytes of JSON array, with at least one request per batch. Ports the reference
    /// client's <c>splitIntoJsonArrayBatches</c>.
    /// </summary>
    private static List<List<SerializedRequest>> SplitIntoJsonArrayBatches(List<SerializedRequest> requests, int maxByteLength)
    {
        var batches = new List<List<SerializedRequest>>();
        var batch = new List<SerializedRequest>();
        // One byte for the opening bracket; each request adds its bytes plus the comma or closing bracket after it.
        var byteLength = 1;
        foreach (var request in requests)
        {
            if (batch.Count > 0 && (batch.Count >= MaxRequestsPerBatch || byteLength + request.ByteLength + 1 > maxByteLength))
            {
                batches.Add(batch);
                batch = new List<SerializedRequest>();
                byteLength = 1;
            }

            batch.Add(request);
            byteLength += request.ByteLength + 1;
        }

        if (batch.Count > 0)
        {
            batches.Add(batch);
        }

        return batches;
    }

    private async Task<BatchAddResult> BatchAddChunkWithRetriesAsync(List<SerializedRequest> chunk, bool forefront, BatchAddRequestsOptions options, CancellationToken cancellationToken)
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
                // As in the reference client, when the call fails and the transport did not retry, the unprocessed requests of this
                // chunk are returned and the loop stops, so earlier chunks' results are kept.
                unprocessed = RequestsNotYetProcessed(chunk, processed).ConvertAll(static serialized => serialized.Request);
                break;
            }

            processed.AddRange(response.ProcessedRequests);
            // Only requests reported as unprocessed in a successful response are retried.
            unprocessed = new List<RequestQueueRequest>(response.UnprocessedRequests);
            if (unprocessed.Count != 0)
            {
                // As in the reference client, a response with unprocessed requests counts as rate limited.
                _http.Stats.AddRateLimitError(attempt + 1);
            }

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

    private async Task<BatchAddResult> BatchAddChunkAsync(List<SerializedRequest> requests, bool forefront, CancellationToken cancellationToken)
    {
        var q = new QueryParams();
        q.AddBool("forefront", forefront);
        ApplyClientKey(q);
        var data = await _ctx.PostWithBodyAsync("requests/batch", q, JoinAsJsonArray(requests), ResourceContext.ContentTypeJson, RequestTier.Medium, cancellationToken).ConfigureAwait(false);

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

    /// <summary>Joins serialized requests into a JSON array body.</summary>
    private static string JoinAsJsonArray(List<SerializedRequest> requests)
        => "[" + string.Join(",", requests.ConvertAll(static serialized => serialized.Json)) + "]";

    private static List<SerializedRequest> RequestsNotYetProcessed(List<SerializedRequest> chunk, List<RequestQueueOperationInfo> processed)
    {
        var processedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var info in processed)
        {
            if (info.UniqueKey is not null)
            {
                processedKeys.Add(info.UniqueKey);
            }
        }

        var remaining = new List<SerializedRequest>();
        foreach (var serialized in chunk)
        {
            if (!processedKeys.Contains(serialized.Request.UniqueKey ?? string.Empty))
            {
                remaining.Add(serialized);
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

        // Exponential backoff with jitter, as in the reference client: (1 + random) * 2^attempt * minDelay.
        var factor = (1 + Random.Shared.NextDouble()) * Math.Pow(2, attempt);
        var delayMillis = (int)Math.Floor(factor * minDelayMillis);
        return Task.Delay(delayMillis, cancellationToken);
    }

    /// <summary>
    /// Deletes multiple requests in one call. Each entry needs its <see cref="RequestQueueRequest.Id"/> or
    /// <see cref="RequestQueueRequest.UniqueKey"/>; other fields are ignored.
    /// </summary>
    /// <param name="requests">The requests to delete, identified by <c>Id</c> and/or <c>UniqueKey</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<BatchDeleteResult> BatchDeleteRequestsAsync(IReadOnlyList<RequestQueueRequest> requests, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        ApplyClientKey(q);
        var data = await _ctx.DeleteWithBodyAsync("requests/batch", q, ToPayload(requests), RequestTier.Short, cancellationToken).ConfigureAwait(false);
        return BatchDeleteResult.FromData(data);
    }

    /// <summary>Encodes requests as their raw JSON objects for a batch body.</summary>
    private static List<JsonObject> ToPayload(IReadOnlyCollection<RequestQueueRequest> requests)
    {
        var payload = new List<JsonObject>(requests.Count);
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
        var data = await _ctx.GetResourceRequiredAsync("requests", q, RequestTier.Medium, cancellationToken).ConfigureAwait(false);
        return RequestQueueRequestsPage.FromData(data);
    }

    /// <summary>
    /// Extends the lock on a request by <paramref name="lockSecs"/> seconds. With <paramref name="forefront"/>, the
    /// request moves to the front when the lock expires.
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
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("requests/" + ResourceContext.EncodePathSegment(id, nameof(id)) + "/lock"));
        using var response = await _http.CallAsync(HttpMethod.Put, url, null, "", _timeout, tier: RequestTier.Medium, cancellationToken: cancellationToken).ConfigureAwait(false);
        return RequestLockInfo.FromData(await response.ReadDataObjectAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Releases the lock on a request, moving it to the front of the queue if <paramref name="forefront"/> is true.</summary>
    /// <param name="id">The request ID.</param>
    /// <param name="forefront">Whether to move the request to the front.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task DeleteRequestLockAsync(string id, bool forefront = false, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddBool("forefront", forefront);
        ApplyClientKey(q);
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("requests/" + ResourceContext.EncodePathSegment(id, nameof(id)) + "/lock"));
        try
        {
            using var response = await _http.CallAsync(HttpMethod.Delete, url, timeout: _timeout, tier: RequestTier.Short, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
        {
            // A missing resource is a no-op.
        }
    }

    /// <summary>Releases all locks the client holds on this queue's requests.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<UnlockRequestsResult> UnlockRequestsAsync(CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        ApplyClientKey(q);
        var data = await _ctx.PostWithBodyAsync("requests/unlock", q, null, "", RequestTier.Long, cancellationToken).ConfigureAwait(false);
        return UnlockRequestsResult.FromData(data);
    }

    /// <summary>Lazily iterates the queue's requests, following pagination.</summary>
    /// <remarks>
    /// Without options it fetches pages of up to <see cref="PaginateRequestsOptions.DefaultMaxPageLimit"/> requests until
    /// the queue is exhausted. <c>Limit</c> caps the total, <c>ChunkSize</c> (or its older name <c>MaxPageLimit</c>) the
    /// page size, <c>ExclusiveStartId</c> and <c>Cursor</c> choose the starting point and <c>Filter</c> restricts the
    /// requests to locked or pending ones.
    /// </remarks>
    /// <param name="options">Optional iteration options.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public async IAsyncEnumerable<RequestQueueRequest> PaginateRequestsAsync(
        PaginateRequestsOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new PaginateRequestsOptions();
        options.Validate();

        var maxPageLimit = options.ChunkSize ?? options.MaxPageLimit ?? PaginateRequestsOptions.DefaultMaxPageLimit;
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

            // After the first page, paginate by cursor only.
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
