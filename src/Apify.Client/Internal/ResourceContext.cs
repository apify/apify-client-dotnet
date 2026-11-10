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
/// The base URL and shared HTTP client of a resource client, with the CRUD primitives the resource clients build on.
/// </summary>
internal sealed class ResourceContext
{
    public const string ContentTypeJson = "application/json";
    public const string ContentTypeJsonCharset = "application/json; charset=utf-8";

    /// <summary>The URL path segment that refers to the current directory; never a valid resource id or key.</summary>
    private const string CurrentDirectorySegment = ".";

    /// <summary>The URL path segment that refers to the parent directory; never a valid resource id or key.</summary>
    private const string ParentDirectorySegment = "..";

    /// <summary>How long to wait between polls while waiting for a run/build to finish, in seconds.</summary>
    private const double WaitPollIntervalSecs = 0.25;

    /// <summary>Longest server-side <c>waitForFinish</c> the API accepts, in seconds.</summary>
    private const int WaitRequestSecs = 60;

    /// <summary>
    /// Subtracted from the request timeout when choosing the server-side <c>waitForFinish</c>, so the server answers
    /// before the client times out.
    /// </summary>
    private const int WaitTimeoutMarginSecs = 5;

    /// <summary>
    /// Upper bound for waiting indefinitely (<c>waitSecs</c> is <c>null</c>), about 11.5 days: the API rejects "Infinity",
    /// and an unbounded loop could spin forever on a transient 404.
    /// </summary>
    private const int MaxWaitForFinishSecs = 999999;

    /// <summary>
    /// How long <see cref="WaitForFinishAsync"/> keeps polling a resource that keeps answering 404 (replica lag right
    /// after creation) before it reports the resource as missing. As in the reference client.
    /// </summary>
    private const int MaxWaitWhenJobNotExistMillis = 3000;

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
    /// Creates a context for a collection endpoint, <c>{base}/{resourcePath}</c>. <paramref name="inheritedParams"/> are
    /// applied to every call, which is how a run's nested storages and log get the last-run <c>status</c> and
    /// <c>origin</c> filters.
    /// </summary>
    public static ResourceContext Collection(HttpClientCore http, string baseUrl, string resourcePath, QueryParams? inheritedParams = null)
    {
        var ctx = new ResourceContext(http, baseUrl + "/" + resourcePath, baseUrl);
        ctx.BaseParams.Extend(inheritedParams);
        return ctx;
    }

    /// <summary>Creates a context for a single resource: <c>{base}/{resourcePath}/{safeId}</c>.</summary>
    public static ResourceContext Single(HttpClientCore http, string baseUrl, string resourcePath, string id, string paramName = "id")
        => new(http, baseUrl + "/" + resourcePath + "/" + ToSafeId(id, paramName), baseUrl);

    /// <summary>
    /// Creates a context for a resource addressed without its own id, such as a run's default dataset or log. A 404 for
    /// such a resource is ambiguous (the parent or the nested resource may be missing), so
    /// <see cref="GetResourceAsync"/> and <see cref="DeleteResourceAsync"/> propagate it. The reference client makes the
    /// same distinction.
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

    /// <summary>
    /// GET a single resource and return its decoded <c>data</c>. A 404 resolves to <c>null</c>, except for the context's
    /// own resource (empty <paramref name="subPath"/>) on a <see cref="NestedSingleton"/>, where it is ambiguous and propagates.
    /// </summary>
    public async Task<JsonObject?> GetResourceAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
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

    /// <summary>GET a single resource, returning its decoded <c>data</c> object (propagates errors).</summary>
    public async Task<JsonObject> GetResourceRequiredAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Get, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return await response.ReadDataObjectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>PUT to update a resource with a JSON-serializable body, returning the decoded <c>data</c>.</summary>
    public async Task<JsonObject> UpdateResourceAsync(string subPath, object? body, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(new QueryParams()).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Put, url, Json.Encode(body), ContentTypeJson, _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return await response.ReadDataObjectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// DELETE a resource. A 404 is a no-op, except for the context's own resource (empty <paramref name="subPath"/>) on a
    /// <see cref="NestedSingleton"/>, where it is ambiguous and propagates.
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
    /// Lazily iterates an offset/limit-paginated listing, fetching pages on demand. <paramref name="limit"/> caps the
    /// total number of items (<c>null</c> or <c>0</c> for no cap), <paramref name="chunkSize"/> sizes each page and
    /// <paramref name="baseQuery"/> carries the filters.
    /// </summary>
    public IAsyncEnumerable<T> IterateListAsync<T>(
        string subPath,
        QueryParams baseQuery,
        int startOffset,
        int? limit,
        int? chunkSize,
        Func<JsonObject, T> hydrate,
        RequestTier tier,
        CancellationToken ct)
    {
        return PagedIteration.IterateAsync(
            startOffset,
            limit,
            chunkSize,
            limitCountsRows: false,
            async (offset, pageLimit, token) =>
            {
                var page = await ListResourceAsync(subPath, baseQuery.PageQuery(offset, pageLimit), hydrate, tier, token).ConfigureAwait(false);
                return new FetchedPage<T>(page.Items, page.Count);
            },
            ct);
    }

    /// <summary>POST to create a resource with a JSON-serializable body, returning the decoded <c>data</c>.</summary>
    public async Task<JsonObject> CreateResourceAsync(QueryParams p, object? body, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(string.Empty));
        using var response = await Http.CallAsync(HttpMethod.Post, url, Json.Encode(body), ContentTypeJson, _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return await response.ReadDataObjectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// POST that gets or creates a named resource (<c>POST {collection}?name=...</c>), with an optional <c>schema</c>.
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
        return await response.ReadDataObjectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// POST with an optional raw body, returning the decoded <c>data</c>. <paramref name="bodyBytes"/> sends raw bytes
    /// instead of <paramref name="body"/>.
    /// </summary>
    public async Task<JsonObject> PostWithBodyAsync(string subPath, QueryParams p, string? body, string contentType, RequestTier tier, CancellationToken ct, byte[]? bodyBytes = null)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Post, url, body, contentType, _requestTimeout, bodyBytes: bodyBytes, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return await response.ReadDataObjectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// POST with an optional raw body, returning the response without unwrapping a <c>data</c> envelope, for endpoints
    /// (such as Actor input validation) that answer with a plain object.
    /// </summary>
    public async Task<JsonNode?> PostWithBodyNoEnvelopeAsync(string subPath, QueryParams p, string? body, string contentType, RequestTier tier, CancellationToken ct, byte[]? bodyBytes = null)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Post, url, body, contentType, _requestTimeout, bodyBytes: bodyBytes, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return await response.ReadJsonAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Encodes a run input: <c>byte[]</c> is sent as it is and anything else as JSON, as in the reference client.
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

    /// <summary>DELETE with a JSON body, returning the decoded <c>data</c>.</summary>
    public async Task<JsonObject> DeleteWithBodyAsync(string subPath, QueryParams p, object? body, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Delete, url, Json.Encode(body), ContentTypeJson, _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return await response.ReadDataObjectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>GET the raw response body; <c>null</c> on 404.</summary>
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
    /// GET the raw response body, propagating a 404. For sub-paths of a resource that must exist, such as dataset
    /// statistics or a task's input.
    /// </summary>
    public async Task<string> GetRawRequiredAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Get, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    /// <summary>GET a JSON body without a <c>data</c> envelope, propagating a 404; <c>null</c> for an empty body.</summary>
    public async Task<JsonNode?> GetJsonRequiredAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Get, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return await response.ReadJsonAsync(ct).ConfigureAwait(false);
    }

    /// <summary>GET a JSON body without a <c>data</c> envelope; <c>null</c> on 404.</summary>
    public async Task<JsonNode?> GetJsonAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        try
        {
            return await GetJsonRequiredAsync(subPath, p, tier, ct).ConfigureAwait(false);
        }
        catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
        {
            return null;
        }
    }

    /// <summary>GET a <c>data</c> object, propagating a 404.</summary>
    public async Task<JsonObject> GetDataObjectRequiredAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Get, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return await response.ReadDataObjectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>GET the raw response body as bytes; <c>null</c> on 404.</summary>
    public async Task<byte[]?> GetRawBytesAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        try
        {
            using var response = await Http.CallAsync(HttpMethod.Get, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
            return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }
        catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
        {
            return null;
        }
    }

    /// <summary>GET the raw response body as bytes, propagating a 404.</summary>
    public async Task<byte[]> GetRawBytesRequiredAsync(string subPath, QueryParams p, RequestTier tier, CancellationToken ct)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Get, url, timeout: _requestTimeout, tier: tier, cancellationToken: ct).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
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

    /// <summary>
    /// PUT raw bytes with a content type. A <paramref name="contentEncoding"/> is sent as the <c>Content-Encoding</c>
    /// header and skips the client's own compression, for an already compressed value.
    /// </summary>
    public async Task PutRawAsync(string subPath, QueryParams p, byte[] body, string contentType, TimeSpan? timeout, bool doNotRetryTimeouts, RequestTier tier, CancellationToken ct, string? contentEncoding = null)
    {
        var url = MergedParams(p).ApplyToUrl(SubUrl(subPath));
        using var response = await Http.CallAsync(HttpMethod.Put, url, null, contentType, timeout ?? _requestTimeout, doNotRetryTimeouts, bodyBytes: body, tier: tier, explicitContentEncoding: contentEncoding, cancellationToken: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The largest <c>waitForFinish</c> to send: below the request timeout by a margin, or the API's cap when the timeout is not finite.
    /// </summary>
    private int ServerWaitCapSecs()
    {
        var configured = (int)Http.RequestTimeoutSecs;
        return configured > 0 ? Math.Max(0, configured - WaitTimeoutMarginSecs) : WaitRequestSecs;
    }

    /// <summary>
    /// Clamps a server-side <c>waitForFinish</c> (seconds) to <see cref="ServerWaitCapSecs"/>; <c>null</c> stays <c>null</c>.
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
    /// Polls a resource with <c>waitForFinish</c> until it reaches a terminal state or <paramref name="waitSecs"/> runs
    /// out (<c>null</c> waits for <see cref="MaxWaitForFinishSecs"/>). A 404 is retried for up to
    /// <see cref="MaxWaitWhenJobNotExistMillis"/>, then reported as a missing resource.
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
        long? notFoundSinceMillis = null;
        ApifyApiException? notFoundError = null;

        while (true)
        {
            var elapsed = stopwatch.ElapsedMilliseconds;
            var remainingSecs = (int)((budgetMillis - elapsed) / 1000);
            var requestSecs = Math.Min(Math.Min(Math.Max(remainingSecs, 0), WaitRequestSecs), serverWaitCap);

            var p = new QueryParams();
            p.AddInt("waitForFinish", requestSecs);

            var isNotFoundNow = false;
            try
            {
                // Long tier: the server may hold the connection for up to the whole wait budget, so the HTTP timeout must cover it.
                var data = await GetResourceRequiredAsync(string.Empty, p, RequestTier.Long, ct).ConfigureAwait(false);
                if (data is JsonObject obj)
                {
                    resource = obj;
                    notFoundSinceMillis = null;
                    notFoundError = null;
                    if (isTerminal(obj))
                    {
                        return obj;
                    }
                }
            }
            catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
            {
                isNotFoundNow = true;
                notFoundSinceMillis ??= stopwatch.ElapsedMilliseconds;
                notFoundError = e;
            }

            if (isNotFoundNow && stopwatch.ElapsedMilliseconds - notFoundSinceMillis!.Value >= MaxWaitWhenJobNotExistMillis)
            {
                throw notFoundError!;
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

        if (notFoundError is not null)
        {
            throw notFoundError;
        }

        throw new InvalidOperationException(
            $"waiting for {resourceName} to finish failed: cannot fetch {resourceName} details from the server");
    }

    /// <summary>
    /// Turns a resource id into one URL path segment: <c>username/name</c> becomes <c>username~name</c>, then the id is
    /// percent-encoded by <see cref="EncodePathSegment"/>.
    /// </summary>
    /// <param name="id">The raw resource id.</param>
    /// <param name="paramName">The name of the caller's parameter, reported by the argument exception.</param>
    /// <exception cref="ArgumentException">The id is empty or a dot segment.</exception>
    public static string ToSafeId(string id, string paramName = "id") => EncodePathSegment(id.Replace('/', '~'), paramName);

    /// <summary>
    /// Percent-encodes a URL path segment so a resource id, record key or request ID cannot break out of it. Empty
    /// values and the dot segments <c>.</c> and <c>..</c> are rejected, because URL parsers resolve <c>%2E%2E</c> like <c>..</c>.
    /// </summary>
    /// <param name="input">The raw, caller-supplied path segment.</param>
    /// <param name="paramName">The name of the caller's parameter, reported by the argument exception.</param>
    /// <exception cref="ArgumentException">The segment is empty or a dot segment.</exception>
    public static string EncodePathSegment(string input, string paramName)
    {
        if (input.Length == 0)
        {
            throw new ArgumentException("URL path segment must not be empty", paramName);
        }

        if (input is CurrentDirectorySegment or ParentDirectorySegment)
        {
            throw new ArgumentException("URL path segment must not be a dot segment", paramName);
        }

        return Uri.EscapeDataString(input);
    }

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
