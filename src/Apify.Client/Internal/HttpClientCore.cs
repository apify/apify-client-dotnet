using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Http;

namespace Apify.Client.Internal;

/// <summary>
/// The HTTP client shared by every resource client. It owns the transport, token, <c>User-Agent</c>, and the
/// retry and timeout policy, and applies them to every request.
/// </summary>
internal sealed class HttpClientCore
{
    /// <summary>Status of a rate limited request.</summary>
    public const int RateLimitExceeded = 429;

    /// <summary>Statuses at or above this value are retried as server errors.</summary>
    private const int MinServerError = 500;

    /// <summary>Statuses below this value are a success.</summary>
    public const int MaxSuccessStatus = 300;

    /// <summary>Multiplier of the delay between retries.</summary>
    private const int BackoffFactor = 2;

    private const int NotFound = 404;

    /// <summary>Prefix of the headers that describe a request body.</summary>
    private const string ContentHeaderPrefix = "Content-";

    /// <summary>The header that carries the media type of a request body.</summary>
    public const string ContentTypeHeader = "Content-Type";

    /// <summary>The header that carries the encoding (compression) of a request body.</summary>
    public const string ContentEncodingHeader = "Content-Encoding";

    /// <summary>Content headers the client derives from the body, so a caller-supplied value is ignored.</summary>
    private static readonly string[] BodyDerivedContentHeaders = { ContentTypeHeader, ContentEncodingHeader, "Content-Length" };

    /// <summary>Bodies of at least this many bytes are compressed, as in the reference client.</summary>
    private const int MinCompressBytes = 1024;

    /// <summary>Media type prefixes whose payloads carry their own compression.</summary>
    private static readonly string[] AlreadyCompressedMediaTypePrefixes = { "audio/", "image/", "video/" };

    /// <summary>Media types whose payloads carry their own compression (archives, packages, fonts).</summary>
    private static readonly HashSet<string> AlreadyCompressedMediaTypes = new(StringComparer.Ordinal)
    {
        "application/epub+zip",
        "application/gzip",
        "application/java-archive",
        "application/vnd.android.package-archive",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.rar",
        "application/x-7z-compressed",
        "application/x-bzip",
        "application/x-bzip2",
        "application/x-gzip",
        "application/x-rar-compressed",
        "application/x-xz",
        "application/x-zip-compressed",
        "application/zip",
        "application/zstd",
        "font/woff",
        "font/woff2",
    };

    /// <summary>Media types under an already-compressed prefix that are uncompressed and still benefit from compression.</summary>
    private static readonly HashSet<string> CompressibleMediaTypes = new(StringComparer.Ordinal)
    {
        "audio/aiff",
        "audio/basic",
        "audio/l16",
        "audio/l24",
        "audio/midi",
        "audio/vnd.wave",
        "audio/wav",
        "audio/wave",
        "audio/x-aiff",
        "audio/x-wav",
        "image/bmp",
        "image/tiff",
        "image/vnd.adobe.photoshop",
        "image/vnd.microsoft.icon",
        "image/x-icon",
        "image/x-ms-bmp",
    };

    /// <summary>Suffixes that mark a media type as text even under an already-compressed prefix, such as <c>image/svg+xml</c>.</summary>
    private static readonly string[] CompressibleMediaTypeSuffixes = { "+json", "+xml" };

    private readonly IHttpTransport _transport;
    private readonly string? _token;
    private readonly RetryConfig _retry;
    private readonly HttpCompressor _compressor;
    private readonly IReadOnlyDictionary<string, string>? _defaultHeaders;
    private readonly Statistics _stats;

    public HttpClientCore(
        IHttpTransport transport,
        string? token,
        string userAgent,
        RetryConfig retry,
        HttpCompressor compressor,
        Statistics stats,
        IReadOnlyDictionary<string, string>? defaultHeaders = null)
    {
        _transport = transport;
        _token = token;
        _retry = retry;
        _compressor = compressor;
        _stats = stats;
        ValidateDefaultHeaders(defaultHeaders);
        _defaultHeaders = defaultHeaders;
        UserAgent = FindHeader(defaultHeaders, "User-Agent") ?? userAgent;
    }

    /// <summary>The request statistics shared with <see cref="ApifyClient.Stats"/>.</summary>
    public Statistics Stats => _stats;

    /// <summary>The <c>User-Agent</c> this client sends; a <c>User-Agent</c> default header wins over the built-in one.</summary>
    public string UserAgent { get; }

    private static string? FindHeader(IReadOnlyDictionary<string, string>? headers, string name)
    {
        if (headers is null)
        {
            return null;
        }

        foreach (var header in headers)
        {
            if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return null;
    }

    /// <summary>The overall per-request timeout budget, in seconds.</summary>
    public double RequestTimeoutSecs => _retry.TimeoutSecs;

    /// <summary>
    /// Sends a request with auth, <c>User-Agent</c>, and the retry policy, and returns the successful response, which the
    /// caller disposes. A <paramref name="policy"/> other than <see cref="RequestPolicy.Default"/> changes the
    /// authentication, the retried responses and whether an error response is returned instead of thrown.
    /// </summary>
    public async Task<HttpResponseMessage> CallAsync(
        HttpMethod method,
        string url,
        string? body = null,
        string contentType = "",
        TimeSpan? timeout = null,
        bool doNotRetryTimeouts = false,
        byte[]? bodyBytes = null,
        IReadOnlyDictionary<string, string>? extraHeaders = null,
        RequestTier tier = RequestTier.Long,
        string? explicitContentEncoding = null,
        RequestPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        policy ??= RequestPolicy.Default;
        _stats.AddCall();
        var delayMillis = _retry.MinDelayMillis;
        var maxAttempts = _retry.MaxRetries + 1;
        var path = ExtractPath(url);
        // An explicit timeout (such as a WithTimeout override) is the base that doubles per attempt; otherwise it is the
        // tier's duration. Either way the growth is capped at the overall budget (ApifyClientOptions.TimeoutSecs), as in the
        // reference client.
        var baseTimeout = timeout ?? TimeSpan.FromSeconds(_retry.TierSecs(tier));
        var overallCap = TimeSpan.FromSeconds(_retry.TimeoutSecs);
        // Prepare (and, when large enough, compress) the body once, so retries reuse it. An explicit content encoding skips
        // this: the bytes are already encoded and must reach the API unchanged.
        var prepared = explicitContentEncoding is not null
            ? new PreparedBody(bodyBytes ?? (body is not null ? Encoding.UTF8.GetBytes(body) : null), contentType, explicitContentEncoding)
            : await PrepareBodyAsync(body, bodyBytes, contentType, policy, cancellationToken).ConfigureAwait(false);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            bool retryable;
            try
            {
                _stats.AddRequest();
                var response = await SendOnceAsync(
                    method, url, prepared, extraHeaders, policy,
                    AttemptTimeout(baseTimeout, overallCap, attempt), cancellationToken).ConfigureAwait(false);

                var status = (int)response.StatusCode;
                if (status < MaxSuccessStatus)
                {
                    // The sent request is disposed; keep a body-less copy so response readers can report the method and path.
                    response.RequestMessage = new HttpRequestMessage(method, url);
                    return response;
                }

                // The statistics track the API's rate limits. Only API calls throw on an error status, so the 429s of other calls stay out.
                if (status == RateLimitExceeded && policy.ThrowOnErrorStatus)
                {
                    _stats.AddRateLimitError(attempt);
                }

                retryable = IsStatusRetryable(status, policy);
                if (!policy.ThrowOnErrorStatus && (!retryable || attempt == maxAttempts))
                {
                    response.RequestMessage = new HttpRequestMessage(method, url);
                    return response;
                }

                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                response.Dispose();
                lastError = BuildApiError(status, errorBody, attempt, method.Method, path);
            }
            catch (ApifyTransportException ex)
            {
                lastError = ex;
                // Network and timeout failures are retried, unless the caller opted out of retrying timeouts or transport errors.
                retryable = policy.RetryTransportErrors && !(doNotRetryTimeouts && ex.IsTimeout);
            }

            if (!retryable || attempt == maxAttempts)
            {
                throw lastError;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(RandomizedDelayMillis(delayMillis)), cancellationToken)
                .ConfigureAwait(false);
            delayMillis = Math.Min(delayMillis * BackoffFactor, _retry.MaxDelayMillis);
        }

        // Unreachable: there is at least one attempt.
        throw lastError ?? new ApifyTransportException("request failed with no attempts");
    }

    /// <summary>Opens a live streaming response with a single attempt and no retry, for log streaming.</summary>
    public Task<HttpResponseMessage> StreamAsync(string url, CancellationToken cancellationToken)
    {
        _stats.AddCall();
        _stats.AddRequest();
        var request = BuildRequest(HttpMethod.Get, url, default, null, RequestPolicy.Default);
        return _transport.SendAsync(request, TimeSpan.FromSeconds(Math.Min(_retry.LongTimeoutSecs, _retry.TimeoutSecs)), streaming: true, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method,
        string url,
        PreparedBody prepared,
        IReadOnlyDictionary<string, string>? extraHeaders,
        RequestPolicy policy,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(method, url, prepared, extraHeaders, policy);
        return await _transport.SendAsync(request, timeout, policy.StreamResponse, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rejects a default header that is a content header (<c>Content-Type</c>, <c>Content-Length</c>, and so on), because
    /// the client sets those per request, or not a valid header name.
    /// </summary>
    /// <exception cref="ArgumentException">A header name is a content header or not a valid header name.</exception>
    private static void ValidateDefaultHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        using var probe = new HttpRequestMessage();
        using var contentProbe = new ByteArrayContent(Array.Empty<byte>());
        foreach (var name in headers.Keys)
        {
            if (probe.Headers.TryAddWithoutValidation(name, string.Empty))
            {
                continue;
            }

            var reason = contentProbe.Headers.TryAddWithoutValidation(name, string.Empty)
                ? "it is a content header, which the client sets per request"
                : "it is not a valid HTTP header name";
            throw new ArgumentException($"ApifyClientOptions.Headers: '{name}' cannot be a default header: {reason}");
        }
    }

    /// <summary>
    /// Moves the request's <c>Authorization</c> header (the token or a default header of that name) to
    /// <paramref name="headerName"/>; <c>null</c> changes nothing. A header of that name set for the single call is
    /// applied afterwards and wins.
    /// </summary>
    private static void MoveAuthorization(HttpRequestMessage request, string? headerName)
    {
        if (headerName is null || !request.Headers.TryGetValues("Authorization", out var values))
        {
            return;
        }

        var authorization = string.Join(", ", values);
        request.Headers.Remove("Authorization");
        request.Headers.Remove(headerName);
        request.Headers.TryAddWithoutValidation(headerName, authorization);
    }

    /// <summary>Sets each header on the request, replacing a value already set under the same name.</summary>
    private static void SetHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var header in headers)
        {
            // Content headers go on the body; the request's header collection rejects them.
            if (IsContentHeader(header.Key))
            {
                continue;
            }

            request.Headers.Remove(header.Key);
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private static bool IsContentHeader(string name) => name.StartsWith(ContentHeaderPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds a request with auth, <c>User-Agent</c>, content type and extra headers.</summary>
    private HttpRequestMessage BuildRequest(
        HttpMethod method,
        string url,
        PreparedBody prepared,
        IReadOnlyDictionary<string, string>? extraHeaders,
        RequestPolicy policy)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        if (!string.IsNullOrEmpty(_token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        // Client-wide default headers win over the built-in ones, and a header of this single call wins over both.
        SetHeaders(request, _defaultHeaders);
        MoveAuthorization(request, policy.AuthorizationHeader);
        SetHeaders(request, extraHeaders);
        if (!policy.FollowRedirects)
        {
            request.Options.Set(ApifyRequestOptions.FollowRedirects, false);
        }

        if (prepared.Bytes is not null)
        {
            var content = new ByteArrayContent(prepared.Bytes);
            // Set the content type as given, without appending a charset.
            content.Headers.ContentType = string.IsNullOrEmpty(prepared.ContentType)
                ? null
                : MediaTypeHeaderValue.Parse(prepared.ContentType);
            if (prepared.ContentEncoding is not null)
            {
                content.Headers.ContentEncoding.Add(prepared.ContentEncoding);
            }

            AddContentHeaders(content, extraHeaders);
            request.Content = content;
        }

        return request;
    }

    /// <summary>
    /// Sets the caller's content headers on the body, except the ones derived from the body. A request without a body has
    /// nothing to attach them to, so they are not sent.
    /// </summary>
    private static void AddContentHeaders(HttpContent content, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var header in headers)
        {
            if (!IsContentHeader(header.Key) || BodyDerivedContentHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            content.Headers.Remove(header.Key);
            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    /// <summary>
    /// Converts a request body to bytes and compresses it once it reaches the size threshold. Raw bytes take precedence
    /// over a string, which is UTF-8 encoded.
    /// </summary>
    /// <remarks>
    /// Bodies of at least <see cref="MinCompressBytes"/> with a compressible content type are compressed with the
    /// configured <see cref="HttpCompressor"/>, brotli by default.
    /// </remarks>
    private async Task<PreparedBody> PrepareBodyAsync(string? body, byte[]? bodyBytes, string contentType, RequestPolicy policy, CancellationToken cancellationToken)
    {
        var raw = bodyBytes ?? (body is not null ? Encoding.UTF8.GetBytes(body) : null);
        if (raw is null)
        {
            return default;
        }

        if (!policy.CompressBody || raw.Length < MinCompressBytes || !IsCompressibleContentType(contentType))
        {
            return new PreparedBody(raw, contentType, null);
        }

        var compressor = policy.Compressor ?? _compressor;
        var compressed = await compressor.CompressAsync(raw, cancellationToken).ConfigureAwait(false);
        return new PreparedBody(compressed, contentType, compressor.ContentEncoding);
    }

    /// <summary>
    /// Decides whether a body with this content type is worth compressing, as the reference client's
    /// <c>isCompressibleContentType()</c> does.
    /// </summary>
    /// <remarks>
    /// Images, audio, video, and archives already carry their own compression, so compressing them wastes CPU and memory
    /// for no gain. Raw formats under such a type (<c>image/bmp</c>, <c>audio/wav</c>), and <c>+json</c> or <c>+xml</c>
    /// types (<c>image/svg+xml</c>) are still compressed. <c>application/octet-stream</c> counts as compressible, since it
    /// holds unknown binary data and is the fallback of <c>SetRecordAsync</c>. A missing content type is not compressible.
    /// </remarks>
    internal static bool IsCompressibleContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return true;
        }

        // The content type may carry parameters, such as "; charset=utf-8".
        var semicolon = contentType.IndexOf(';', StringComparison.Ordinal);
        var mediaType = (semicolon >= 0 ? contentType.Substring(0, semicolon) : contentType)
            .Trim()
            .ToLowerInvariant();

        if (CompressibleMediaTypes.Contains(mediaType))
        {
            return true;
        }

        foreach (var suffix in CompressibleMediaTypeSuffixes)
        {
            if (mediaType.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (AlreadyCompressedMediaTypes.Contains(mediaType))
        {
            return false;
        }

        foreach (var prefix in AlreadyCompressedMediaTypePrefixes)
        {
            if (mediaType.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A request body as bytes, with the content type and optional <c>Content-Encoding</c> to send.</summary>
    private readonly record struct PreparedBody(byte[]? Bytes, string ContentType, string? ContentEncoding);

    /// <summary>
    /// Returns <c>min(overall, base * 2^(attempt-1))</c>: each retry doubles the timeout, and every attempt, the first
    /// included, is capped at the overall budget (<c>ApifyClientOptions.TimeoutSecs</c>), as in the reference client's
    /// <c>timeoutMaxSecs</c>.
    /// </summary>
    private static TimeSpan AttemptTimeout(TimeSpan baseTimeout, TimeSpan overall, int attempt)
    {
        var scaled = baseTimeout;
        for (var i = 1; i < attempt; i++)
        {
            scaled *= 2;
            if (scaled >= overall)
            {
                return overall;
            }
        }

        return scaled < overall ? scaled : overall;
    }

    private static bool IsStatusRetryable(int status, RequestPolicy policy)
        => policy.RetryStatusCodes is { } codes
            ? codes.Contains(status)
            : status == RateLimitExceeded || status >= MinServerError;

    /// <summary>Returns a random delay from <c>[delay, 2*delay)</c>: exponential backoff with jitter.</summary>
    private static double RandomizedDelayMillis(double delayMillis)
    {
        if (delayMillis <= 0)
        {
            return delayMillis;
        }

        return delayMillis + (Random.Shared.NextDouble() * delayMillis);
    }

    /// <summary>Builds an <see cref="ApifyApiException"/> from an error response body.</summary>
    public static ApifyApiException BuildApiError(int status, string body, int attempt, string method, string path)
    {
        string? type = null;
        string? message = null;
        System.Text.Json.Nodes.JsonObject? data = null;

        if (Json.TryDecode(body) is System.Text.Json.Nodes.JsonObject decoded
            && decoded.TryGetPropertyValue("error", out var errorNode)
            && errorNode is System.Text.Json.Nodes.JsonObject error)
        {
            type = AsString(error, "type");
            message = AsString(error, "message");
            if (error.TryGetPropertyValue("data", out var dataNode)
                && dataNode is System.Text.Json.Nodes.JsonObject dataObj)
            {
                data = (System.Text.Json.Nodes.JsonObject)dataObj.DeepClone();
            }
        }

        message ??= body.Length == 0
            ? "unexpected error with status " + status.ToString(CultureInfo.InvariantCulture)
            : "unexpected error: " + body;

        return ApifyApiException.Create(status, type, message, attempt, method, path, data);
    }

    private static string? AsString(System.Text.Json.Nodes.JsonObject obj, string key)
    {
        if (obj.TryGetPropertyValue(key, out var node)
            && node is System.Text.Json.Nodes.JsonValue value
            && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return null;
    }

    /// <summary>Returns the path and query of a URL, for error messages.</summary>
    public static string ExtractPath(string url)
    {
        var rest = url;
        var scheme = rest.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            rest = rest.Substring(scheme + 3);
        }

        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 ? rest.Substring(slash) : string.Empty;
    }

    /// <summary>Whether an exception is a "not found" API error.</summary>
    public static bool IsNotFound(Exception ex)
    {
        if (ex is not ApifyApiException apiError || apiError.StatusCode != NotFound)
        {
            return false;
        }

        return apiError.Type is "record-not-found" or "record-or-token-not-found"
            || string.Equals(apiError.HttpMethod, "HEAD", StringComparison.Ordinal);
    }
}
