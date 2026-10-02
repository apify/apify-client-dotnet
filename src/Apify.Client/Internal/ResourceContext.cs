using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Exceptions;
using Apify.Client.Models;

namespace Apify.Client.Internal;

/// <summary>
/// The resolved context for a resource client: its base URL and the shared HTTP client. The methods
/// here implement the CRUD primitives once, so each resource client stays small and consistent (DRY).
/// </summary>
internal sealed class ResourceContext
{
    public const string ContentTypeJson = "application/json";
    public const string ContentTypeJsonCharset = "application/json; charset=utf-8";

    /// <summary>How long to wait between polls while waiting for a run/build to finish, in seconds.</summary>
    private const double WaitPollIntervalSecs = 0.25;

    /// <summary>Server-side waitForFinish chunk size (the API caps server waiting at 60 seconds).</summary>
    private const int WaitRequestSecs = 60;

    /// <summary>
    /// Safety margin subtracted from the configured per-request timeout when choosing the server-side
    /// <c>waitForFinish</c> value, so the server responds before the client's socket timeout fires.
    /// </summary>
    private const int WaitTimeoutMarginSecs = 5;

    /// <summary>
    /// Finite upper bound used when the caller asks to wait "indefinitely" (<c>waitSecs == null</c>). The
    /// API will not accept "Infinity" and an unbounded loop can spin forever on a transient 404; 999999s
    /// (~11.5 days) is effectively indefinite while guaranteeing termination.
    /// </summary>
    private const int MaxWaitForFinishSecs = 999999;

    private readonly string _apiOrigin;
    private string _publicOrigin;
    private TimeSpan? _requestTimeout;
    private bool _ambiguousNotFound;

    private ResourceContext(HttpClientCore http, string url, string baseUrl)
    {
        Http = http;
        Url = url;
        BaseParams = new QueryParams();
        _apiOrigin = OriginOf(baseUrl);
        _publicOrigin = _apiOrigin;
    }

    /// <summary>The shared orchestrating HTTP client.</summary>
    public HttpClientCore Http { get; }

    /// <summary>Fully-qualified base URL of the resource, e.g. https://api.apify.com/v2/actors/ID.</summary>
    public string Url { get; }

    /// <summary>Query parameters inherited by every call made through this context.</summary>
    public QueryParams BaseParams { get; }

    /// <summary>The per-context request timeout, or <c>null</c> to use the client-wide default.</summary>
    public TimeSpan? RequestTimeout => _requestTimeout;

    /// <summary>
    /// Creates a context for a collection endpoint: <c>{base}/{resourcePath}</c>. Any
    /// <paramref name="inheritedParams"/> are copied into <see cref="BaseParams"/> so they are applied to
    /// every call on the nested resource (used to forward the last-run <c>status</c>/<c>origin</c> filters
    /// to a run's nested storage and log clients, matching the reference client).
    /// </summary>
    public static ResourceContext Collection(HttpClientCore http, string baseUrl, string resourcePath, QueryParams? inheritedParams = null)
    {
        var ctx = new ResourceContext(http, baseUrl + "/" + resourcePath, baseUrl);
        ctx.BaseParams.Extend(inheritedParams);
        return ctx;
    }

    /// <summary>Creates a context for a single resource: <c>{base}/{resourcePath}/{safeId}</c>.</summary>
    public static ResourceContext Single(HttpClientCore http, string baseUrl, string resourcePath, string id)
        => new(http, baseUrl + "/" + resourcePath + "/" + ToSafeId(id), baseUrl);

    /// <summary>
    /// Creates a context for a resource addressed without its own id: a run/build's nested default dataset,
    /// key-value store, request queue or log (<c>{base}/{resourcePath}</c>, same shape as
    /// <see cref="Collection"/>). A 404 through <see cref="GetResourceAsync"/>/<see cref="DeleteResourceAsync"/>
    /// on such a context is ambiguous — it could mean the parent resource (e.g. the run) or the nested one is
    /// missing — so those methods propagate it instead of resolving to <c>null</c>, matching the reference
    /// client's <c>catchNotFoundForResourceOrThrow()</c> distinction between id-addressed and chained clients.
    /// </summary>
    public static ResourceContext NestedSingleton(HttpClientCore http, string baseUrl, string resourcePath, QueryParams? inheritedParams = null)
    {
        var ctx = Collection(http, baseUrl, resourcePath, inheritedParams);
        ctx._ambiguousNotFound = true;
        return ctx;
    }

    /// <summary>Sets an overall per-request timeout for every call made through this context.</summary>
    public ResourceContext WithTimeout(TimeSpan? timeout)
    {
        _requestTimeout = timeout;
        return this;
    }

    /// <summary>Overrides the origin used when building public URLs.</summary>
    public ResourceContext WithPublicOrigin(string publicBaseUrl)
    {
        _publicOrigin = OriginOf(publicBaseUrl);
        return this;
    }

    /// <summary>This resource's URL with an optional extra path segment appended.</summary>
    public string SubUrl(string subPath = "") => subPath.Length == 0 ? Url : Url + "/" + subPath;

    /// <summary>The public (shareable) form of this resource's URL, swapping the API origin for the public one.</summary>
    public string PublicUrl(string subPath)
    {
        var apiUrl = SubUrl(subPath);
        if (string.Equals(_publicOrigin, _apiOrigin, StringComparison.Ordinal))
        {
            return apiUrl;
        }

        return apiUrl.StartsWith(_apiOrigin, StringComparison.Ordinal)
            ? string.Concat(_publicOrigin, apiUrl.AsSpan(_apiOrigin.Length))
            : apiUrl;
    }

    /// <summary>Merges the inherited base params with per-call params.</summary>
    public QueryParams MergedParams(QueryParams? p) => BaseParams.Copy().Extend(p);

    // ---- CRUD primitives ------------------------------------------------------

    /// <summary>
    /// GET a single resource, returning its decoded <c>data</c>. A 404 resolves to <c>null</c>, unless
    /// <paramref name="subPath"/> addresses this context's own resource (empty) <em>and</em> the context was
    /// built with <see cref="NestedSingleton"/> — then the 404 is ambiguous (it could mean the parent
    /// resource or the nested one is missing) and propagates instead. A non-empty <paramref name="subPath"/>
    /// (e.g. a request looked up by its own id) is never ambiguous regardless of how this context was built.
    /// </summary>
    public async Task<JsonNode?> GetResourceAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        if (_ambiguousNotFound && subPath.Length == 0)
        {
            return await GetResourceRequiredAsync(subPath, p, tier, ct).ConfigureAwait(false);
        }

        try
        {
            return await GetResourceRequiredAsync(subPath, p, tier, ct).ConfigureAwait(false);
        }
        catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
        {
            return null;
        }
    }

    /// <summary>GET a single resource, returning its decoded <c>data</c> (propagates errors).</summary>
    public async Task<JsonNode?> GetResourceRequiredAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Get, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return Json.DecodeData(body);
    }

    /// <summary>PUT to update a resource with a JSON-serializable body, returning the decoded <c>data</c>.</summary>
    public async Task<JsonObject> UpdateResourceAsync(string subPath, object? body, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(new QueryParams()).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Put, url, Json.Encode(body), ContentTypeJson, _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return AsObject(Json.DecodeData(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)));
    }

    /// <summary>
    /// Performs a DELETE. A not-found is treated as a successful no-op, unless <paramref name="subPath"/>
    /// addresses this context's own resource (empty) <em>and</em> the context was built with
    /// <see cref="NestedSingleton"/> — then the 404 is ambiguous (the parent resource or the nested one could
    /// be missing) and propagates instead.
    /// </summary>
    public async Task DeleteResourceAsync(string subPath, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(new QueryParams()).ApplyToUrl(SubUrl(subPath));
        if (_ambiguousNotFound && subPath.Length == 0)
        {
            using var ambiguousResponse = await Http.CallAsync(HttpMethod.Delete, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        try
        {
            using var response = await Http.CallAsync(HttpMethod.Delete, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
        {
            // A missing resource is a successful no-op for delete.
        }
    }

    /// <summary>GET a paginated listing and build a <see cref="PaginationList{T}"/> with each item hydrated.</summary>
    public async Task<PaginationList<T>> ListResourceAsync<T>(string subPath, QueryParams p, Func<JsonObject, T> hydrate, RequestTier tier, CancellationToken ct)
    {
        var data = await GetResourceRequiredAsync(subPath, p, tier, ct).ConfigureAwait(false);
        return PaginationList<T>.FromData(data, hydrate);
    }

    /// <summary>
    /// Lazily iterates every item of an offset/limit-paginated listing, fetching pages on demand. Mirrors
    /// the reference client's auto-paging list iterators: it starts at <paramref name="startOffset"/> and,
    /// if <paramref name="limit"/> is set, yields at most that many items in total. <paramref name="baseQuery"/>
    /// carries the caller's filters; its <c>offset</c>/<c>limit</c> are overwritten per page.
    /// </summary>
    public async IAsyncEnumerable<T> IterateListAsync<T>(
        string subPath,
        QueryParams baseQuery,
        int startOffset,
        int? limit,
        Func<JsonObject, T> hydrate,
        RequestTier tier,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var offset = Math.Max(startOffset, 0);
        var yielded = 0;
        while (true)
        {
            var q = baseQuery.Copy().Set("offset", offset);
            if (limit is not null)
            {
                q.Set("limit", Math.Max(limit.Value - yielded, 0));
            }

            var page = await ListResourceAsync(subPath, q, hydrate, tier, ct).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                yield return item;
                yielded++;
            }

            offset += (int)page.Count;
            if (page.Count == 0 || offset >= page.Total || (limit is not null && yielded >= limit.Value))
            {
                yield break;
            }
        }
    }

    /// <summary>POST to create a resource with a JSON-serializable body, returning the decoded <c>data</c>.</summary>
    public async Task<JsonObject> CreateResourceAsync(QueryParams p, object? body, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(string.Empty));
        using var response = await Http.CallAsync(HttpMethod.Post, url, Json.Encode(body), ContentTypeJson, _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return AsObject(Json.DecodeData(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)));
    }

    /// <summary>
    /// POST that gets-or-creates a named resource (<c>POST {collection}?name=...</c>). An optional
    /// <paramref name="schema"/> is sent as <c>{"schema": ...}</c>, matching the reference client.
    /// </summary>
    public async Task<JsonObject> GetOrCreateNamedAsync(string? name, JsonNode? schema, CancellationToken ct)
    {
        var p = new QueryParams();
        if (!string.IsNullOrEmpty(name))
        {
            p.AddString("name", name);
        }

        var url = p.ApplyToUrl(SubUrl(string.Empty));
        using var response = schema is not null
            ? await Http.CallAsync(HttpMethod.Post, url, Json.Encode(new JsonObject { ["schema"] = schema.DeepClone() }), ContentTypeJson, _requestTimeout, tier: RequestTier.Short, cancellationToken: ct).ConfigureAwait(false)
            : await Http.CallAsync(HttpMethod.Post, url, timeout: _requestTimeout, tier: RequestTier.Short, cancellationToken: ct).ConfigureAwait(false);
        return AsObject(Json.DecodeData(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)));
    }

    /// <summary>
    /// POST with an optional raw body and content type, unwrapping the data envelope. Pass
    /// <paramref name="bodyBytes"/> instead of <paramref name="body"/> to send raw bytes (e.g. an Actor's raw
    /// byte input, see <see cref="EncodeInputBody"/>) without re-encoding them as a JSON string.
    /// </summary>
    public async Task<JsonObject> PostWithBodyAsync(string subPath, QueryParams p, string? body, string contentType, RequestTier tier, CancellationToken ct, byte[]? bodyBytes = null)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Post, url, body, contentType, _requestTimeout, bodyBytes: bodyBytes, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return AsObject(Json.DecodeData(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)));
    }

    /// <summary>
    /// POST with a raw body, parsing the response directly <em>without</em> unwrapping a data envelope.
    /// Used by endpoints (e.g. actor input validation) whose response is a plain object. Pass
    /// <paramref name="bodyBytes"/> instead of <paramref name="body"/> to send raw bytes (see
    /// <see cref="EncodeInputBody"/>) without re-encoding them as a JSON string.
    /// </summary>
    public async Task<JsonNode?> PostWithBodyNoEnvelopeAsync(string subPath, QueryParams p, string? body, string contentType, RequestTier tier, CancellationToken ct, byte[]? bodyBytes = null)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Post, url, body, contentType, _requestTimeout, bodyBytes: bodyBytes, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return Json.Decode(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Encodes an Actor/task run input for the request body: raw bytes (e.g. a pre-serialized or binary
    /// payload, typically paired with an explicit content type) are sent as-is, and anything else is
    /// JSON-encoded, matching the reference client's <c>ActorInput</c> (a plain object/array, or raw bytes).
    /// </summary>
    /// <param name="input">The input value, or <c>null</c> for no input.</param>
    /// <param name="body">The JSON-encoded body, or <c>null</c> when <paramref name="bodyBytes"/> is used.</param>
    /// <param name="bodyBytes">The raw bytes to send, or <c>null</c> when <paramref name="body"/> is used.</param>
    public static void EncodeInputBody(object? input, out string? body, out byte[]? bodyBytes)
    {
        if (input is byte[] raw)
        {
            body = null;
            bodyBytes = raw;
            return;
        }

        body = input is null ? null : Json.Encode(input);
        bodyBytes = null;
    }

    /// <summary>DELETE with a JSON body (used for batch request deletion), unwrapping the data envelope.</summary>
    public async Task<JsonObject> DeleteWithBodyAsync(string subPath, QueryParams p, object? body, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Delete, url, Json.Encode(body), ContentTypeJson, _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return AsObject(Json.DecodeData(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)));
    }

    /// <summary>
    /// GET returning the raw response body (no data envelope). Returns <c>null</c> on not-found.
    /// </summary>
    public async Task<string?> GetRawAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        try
        {
            using var response = await Http.CallAsync(HttpMethod.Get, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
        {
            return null;
        }
    }

    /// <summary>
    /// GET returning the raw response body (no data envelope); propagates a 404 instead of swallowing it.
    /// Used by sub-path endpoints whose parent resource must already exist for the call to make sense (e.g.
    /// dataset statistics, a schedule's log, a task's input), so a missing parent surfaces as an error rather
    /// than as an ambiguous empty result.
    /// </summary>
    public async Task<string> GetRawRequiredAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Get, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    /// <summary>HEAD request; returns whether the resource exists.</summary>
    public async Task<bool> HeadExistsAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        try
        {
            using var response = await Http.CallAsync(HttpMethod.Head, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
            return true;
        }
        catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
        {
            return false;
        }
    }

    /// <summary>PUT with raw bytes and a content type, with an explicit per-request timeout and retry control.</summary>
    public async Task PutRawAsync(string subPath, QueryParams p, byte[] body, string contentType, TimeSpan? timeout, bool doNotRetryTimeouts, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Put, url, null, contentType, timeout ?? _requestTimeout, doNotRetryTimeouts, bodyBytes: body, tier: tier, cancellationToken: ct).ConfigureAwait(false);
    }

    // ---- Wait-for-finish ------------------------------------------------------

    /// <summary>
    /// The largest server-side <c>waitForFinish</c> value that is safe to send: below the configured
    /// per-request timeout by a safety margin (or the API's 60s cap when no finite timeout is set).
    /// </summary>
    private int ServerWaitCapSecs()
    {
        var configured = (int)Http.RequestTimeoutSecs;
        return configured > 0 ? Math.Max(0, configured - WaitTimeoutMarginSecs) : WaitRequestSecs;
    }

    /// <summary>
    /// Clamps a caller-supplied server-side <c>waitForFinish</c> value (seconds) to the server wait cap,
    /// so a synchronous get/wait never asks the server to hold the connection longer than the client's own
    /// per-request timeout. Returns <c>null</c> for a <c>null</c> input.
    /// </summary>
    public int? ClampServerWait(int? waitForFinishSecs)
    {
        if (waitForFinishSecs is null)
        {
            return null;
        }

        return Math.Min(Math.Max(0, waitForFinishSecs.Value), ServerWaitCapSecs());
    }

    /// <summary>
    /// Polls a GET endpoint with <c>waitForFinish</c> until the resource reaches a terminal state or the
    /// wait budget elapses. <paramref name="waitSecs"/> == <c>null</c> means "wait indefinitely",
    /// implemented as a finite but very large bound so the loop always terminates. A transient 404 (replica
    /// lag) is treated as "not yet available".
    /// </summary>
    public async Task<JsonObject> WaitForFinishAsync(int? waitSecs, string resourceName, Func<JsonObject, bool> isTerminal, CancellationToken ct)
    {
        var effectiveWaitSecs = waitSecs is not null
            ? Math.Min(Math.Max(waitSecs.Value, 0), MaxWaitForFinishSecs)
            : MaxWaitForFinishSecs;
        var budgetMillis = (long)effectiveWaitSecs * 1000;
        var stopwatch = Stopwatch.StartNew();
        var serverWaitCap = ServerWaitCapSecs();

        JsonObject? resource = null;

        while (true)
        {
            var elapsed = stopwatch.ElapsedMilliseconds;
            var remainingSecs = (int)((budgetMillis - elapsed) / 1000);
            var requestSecs = Math.Min(Math.Min(Math.Max(remainingSecs, 0), WaitRequestSecs), serverWaitCap);

            var p = new QueryParams();
            p.AddInt("waitForFinish", requestSecs);

            // Long tier (the overall per-request budget): the server may hold the connection open for up to
            // requestSecs (derived from that same budget via ServerWaitCapSecs), so the HTTP timeout for this
            // call must cover the full budget, not a shorter tier.
            var data = await GetResourceAsync(string.Empty, p, RequestTier.Long, ct).ConfigureAwait(false);
            if (data is JsonObject obj)
            {
                resource = obj;
                if (isTerminal(obj))
                {
                    return obj;
                }
            }

            if (stopwatch.ElapsedMilliseconds >= budgetMillis)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(WaitPollIntervalSecs), ct).ConfigureAwait(false);
        }

        if (resource is not null)
        {
            return resource;
        }

        throw new InvalidOperationException(
            $"waiting for {resourceName} to finish failed: cannot fetch {resourceName} details from the server");
    }

    /// <summary>
    /// Coerces a decoded value to a JSON object. Endpoints that return a resource object always decode to
    /// an object; a non-object (e.g. an unexpected <c>null</c> data field) becomes an empty object so model
    /// construction stays type-safe.
    /// </summary>
    private static JsonObject AsObject(JsonNode? value) => value as JsonObject ?? new JsonObject();

    // ---- URL / id helpers -----------------------------------------------------

    /// <summary>
    /// Encodes a resource id so it is safe to embed in a URL path. Apify uses the
    /// <c>username~resourcename</c> form, so the first <c>/</c> of an id is replaced with <c>~</c>.
    /// </summary>
    public static string ToSafeId(string id)
    {
        var slash = id.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? id : string.Concat(id.AsSpan(0, slash), "~", id.AsSpan(slash + 1));
    }

    /// <summary>
    /// Percent-encodes a single URL path segment, so values interpolated into the path (record keys,
    /// request IDs) cannot break out of the segment.
    /// </summary>
    public static string EncodePathSegment(string input) => Uri.EscapeDataString(input);

    /// <summary>Extracts the origin (<c>scheme://host[:port]</c>) from a URL, dropping any path.</summary>
    public static string OriginOf(string rawUrl)
    {
        var rest = rawUrl;
        var scheme = string.Empty;
        var pos = rest.IndexOf("://", StringComparison.Ordinal);
        if (pos >= 0)
        {
            scheme = rest.Substring(0, pos + 3);
            rest = rest.Substring(pos + 3);
        }

        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        if (slash >= 0)
        {
            rest = rest.Substring(0, slash);
        }

        return scheme + rest;
    }
}
