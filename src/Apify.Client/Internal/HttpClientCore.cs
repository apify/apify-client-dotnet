using System;
using System.Collections.Generic;
using System.Globalization;
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
/// The orchestrating HTTP client shared by every resource client. It owns the transport, the optional
/// API token, the <c>User-Agent</c>, and the retry/timeout policy, and applies them to every request.
/// </summary>
internal sealed class HttpClientCore
{
    /// <summary>Status returned when the per-resource rate limit is hit.</summary>
    private const int RateLimitExceeded = 429;

    /// <summary>Statuses at or above this value are treated as retryable internal server errors.</summary>
    private const int MinServerError = 500;

    /// <summary>Responses with a status below this value are treated as success.</summary>
    public const int MaxSuccessStatus = 300;

    /// <summary>Exponential-backoff multiplier applied to the inter-retry delay after each attempt.</summary>
    private const int BackoffFactor = 2;

    private const int NotFound = 404;

    /// <summary>
    /// Request bodies whose size in bytes is at or above this threshold are compressed before sending,
    /// matching the reference client's minimum-compression size.
    /// </summary>
    private const int MinCompressBytes = 1024;

    /// <summary>Media type prefixes whose payloads carry their own compression (e.g. <c>image/png</c>).</summary>
    private static readonly string[] AlreadyCompressedMediaTypePrefixes = { "audio/", "image/", "video/" };

    /// <summary>Exact media types whose payloads carry their own compression (archives, packages, fonts).</summary>
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

    /// <summary>Uncompressed media types that sit under an already-compressed prefix but still benefit from compression.</summary>
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

    /// <summary>Structured syntax suffixes marking a media type as text even under an already-compressed prefix (e.g. <c>image/svg+xml</c>).</summary>
    private static readonly string[] CompressibleMediaTypeSuffixes = { "+json", "+xml" };

    private readonly IHttpTransport _transport;
    private readonly string? _token;
    private readonly RetryConfig _retry;
    private readonly HttpCompressor _compressor;
    private readonly IReadOnlyDictionary<string, string>? _defaultHeaders;

    public HttpClientCore(
        IHttpTransport transport,
        string? token,
        string userAgent,
        RetryConfig retry,
        HttpCompressor compressor,
        IReadOnlyDictionary<string, string>? defaultHeaders = null)
    {
        _transport = transport;
        _token = token;
        UserAgent = userAgent;
        _retry = retry;
        _compressor = compressor;
        ValidateDefaultHeaders(defaultHeaders);
        _defaultHeaders = defaultHeaders;
    }

    /// <summary>The <c>User-Agent</c> header value this client sends.</summary>
    public string UserAgent { get; }

    /// <summary>The configured overall per-request timeout budget, in seconds.</summary>
    public double RequestTimeoutSecs => _retry.TimeoutSecs;

    /// <summary>
    /// Sends a request with auth, User-Agent and the retry policy applied, returning the successful
    /// response (the caller owns and disposes it).
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
        CancellationToken cancellationToken = default)
    {
        var delayMillis = _retry.MinDelayMillis;
        var maxAttempts = _retry.MaxRetries + 1;
        var path = ExtractPath(url);
        // An explicit timeout (e.g. a per-queue or per-call WithTimeout override) always wins as the base for
        // the per-attempt doubling below; otherwise the tier's configured duration is the base. Either way,
        // the retry-growth cap is always the configured overall budget (ApifyClientOptions.TimeoutSecs) —
        // matching the reference client, an explicit override above that budget is capped back down to it
        // rather than silently exceeding it. Raise TimeoutSecs itself to allow a longer per-call timeout.
        var baseTimeout = timeout ?? TimeSpan.FromSeconds(_retry.TierSecs(tier));
        var overallCap = TimeSpan.FromSeconds(_retry.TimeoutSecs);
        // Normalize (and, when large enough, compress) the body once up front so retries reuse the same
        // prepared payload instead of re-encoding and re-compressing on every attempt. A caller-supplied
        // explicitContentEncoding (e.g. SetRecordAsync's contentEncoding for an already-compressed value)
        // skips this: the bytes are already in their final, encoded form and must reach the API unchanged.
        var prepared = explicitContentEncoding is not null
            ? new PreparedBody(bodyBytes ?? (body is not null ? Encoding.UTF8.GetBytes(body) : null), contentType, explicitContentEncoding)
            : await PrepareBodyAsync(body, bodyBytes, contentType, cancellationToken).ConfigureAwait(false);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            bool retryable;
            try
            {
                var response = await SendOnceAsync(
                    method, url, prepared, extraHeaders,
                    AttemptTimeout(baseTimeout, overallCap, attempt), cancellationToken).ConfigureAwait(false);

                var status = (int)response.StatusCode;
                if (status < MaxSuccessStatus)
                {
                    // The sent request is disposed by now; keep a body-less copy of it so response readers can
                    // report the method and path in the errors they raise.
                    response.RequestMessage = new HttpRequestMessage(method, url);
                    return response;
                }

                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                response.Dispose();
                lastError = BuildApiError(status, errorBody, attempt, method.Method, path);
                retryable = IsStatusRetryable(status);
            }
            catch (ApifyTransportException ex)
            {
                lastError = ex;
                // Network/timeout failures are retryable, unless the caller opted out of retrying timeouts.
                retryable = !(doNotRetryTimeouts && ex.IsTimeout);
            }

            if (!retryable || attempt == maxAttempts)
            {
                throw lastError;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(RandomizedDelayMillis(delayMillis)), cancellationToken)
                .ConfigureAwait(false);
            delayMillis = Math.Min(delayMillis * BackoffFactor, _retry.MaxDelayMillis);
        }

        // Unreachable in practice (maxAttempts >= 1); defensive.
        throw lastError ?? new ApifyTransportException("request failed with no attempts");
    }

    /// <summary>Opens a live streaming response (single attempt, no retry). Used by log streaming.</summary>
    public Task<HttpResponseMessage> StreamAsync(string url, CancellationToken cancellationToken)
    {
        var request = BuildRequest(HttpMethod.Get, url, default, null);
        return _transport.SendAsync(request, TimeSpan.FromSeconds(_retry.TimeoutSecs), streaming: true, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method,
        string url,
        PreparedBody prepared,
        IReadOnlyDictionary<string, string>? extraHeaders,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(method, url, prepared, extraHeaders);
        return await _transport.SendAsync(request, timeout, streaming: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rejects a default header that is not a request header. Content headers (<c>Content-Type</c>,
    /// <c>Content-Encoding</c>, <c>Content-Length</c>, ...) describe the body, which the client sets per
    /// request, so they cannot be defaults.
    /// </summary>
    /// <exception cref="ArgumentException">A header name is not a valid request header name.</exception>
    private static void ValidateDefaultHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        using var probe = new HttpRequestMessage();
        foreach (var name in headers.Keys)
        {
            if (!probe.Headers.TryAddWithoutValidation(name, string.Empty))
            {
                throw new ArgumentException(
                    $"'{name}' cannot be a default header: only request headers are allowed, not content headers such as Content-Type", nameof(headers));
            }
        }
    }

    /// <summary>Sets each header on the request, replacing any value already set under the same name.</summary>
    private static void SetHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var header in headers)
        {
            request.Headers.Remove(header.Key);
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    /// <summary>Builds a fully-prepared request with auth, User-Agent, content type and extra headers.</summary>
    private HttpRequestMessage BuildRequest(
        HttpMethod method,
        string url,
        PreparedBody prepared,
        IReadOnlyDictionary<string, string>? extraHeaders)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        if (!string.IsNullOrEmpty(_token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        // The client-wide default headers win over the built-in ones above, and a header set for this single call
        // wins over both.
        SetHeaders(request, _defaultHeaders);
        SetHeaders(request, extraHeaders);

        if (prepared.Bytes is not null)
        {
            var content = new ByteArrayContent(prepared.Bytes);
            // Set the content type verbatim (no charset appended unless the caller added one).
            content.Headers.ContentType = string.IsNullOrEmpty(prepared.ContentType)
                ? null
                : MediaTypeHeaderValue.Parse(prepared.ContentType);
            if (prepared.ContentEncoding is not null)
            {
                content.Headers.ContentEncoding.Add(prepared.ContentEncoding);
            }

            request.Content = content;
        }

        return request;
    }

    /// <summary>
    /// Normalizes a request body to bytes and, when it is large enough, compresses it. Raw bytes take
    /// precedence so binary records (e.g. images) are used as-is rather than re-encoded through a UTF-8
    /// string; a string body is UTF-8 encoded. Either kind of payload is then compressed once it reaches
    /// the size threshold (see the remarks).
    /// </summary>
    /// <remarks>
    /// Bodies at or above <see cref="MinCompressBytes"/> whose content type is compressible are compressed with
    /// the configured <see cref="HttpCompressor"/>: brotli (<c>Content-Encoding: br</c>) by default, matching
    /// the reference client's preference.
    /// </remarks>
    private async Task<PreparedBody> PrepareBodyAsync(string? body, byte[]? bodyBytes, string contentType, CancellationToken cancellationToken)
    {
        var raw = bodyBytes ?? (body is not null ? Encoding.UTF8.GetBytes(body) : null);
        if (raw is null)
        {
            return default;
        }

        if (raw.Length < MinCompressBytes || !IsCompressibleContentType(contentType))
        {
            return new PreparedBody(raw, contentType, null);
        }

        var compressed = await _compressor.CompressAsync(raw, cancellationToken).ConfigureAwait(false);
        return new PreparedBody(compressed, contentType, _compressor.ContentEncoding);
    }

    /// <summary>
    /// Decides whether a request body with the given content type is worth compressing, matching the
    /// reference client's <c>isCompressibleContentType()</c>.
    /// </summary>
    /// <remarks>
    /// Images, audio, video and archives already carry their own compression: running them through
    /// brotli/gzip burns CPU, holds a second full copy of the body in memory, and usually produces output
    /// slightly larger than the input. Formats that are raw despite such a media type (e.g. <c>image/bmp</c>,
    /// <c>audio/wav</c>) are still compressed, as are <c>+json</c>/<c>+xml</c> structured-syntax suffixes
    /// (e.g. <c>image/svg+xml</c>). <c>application/octet-stream</c> is deliberately left off the
    /// already-compressed list: it is the catch-all for unknown binary data and <c>SetRecordAsync</c>'s
    /// fallback when no content type is given. No content type is assumed compressible.
    /// </remarks>
    internal static bool IsCompressibleContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return true;
        }

        // Content-Type may carry parameters, e.g. "text/plain; charset=utf-8".
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

    /// <summary>
    /// A request body normalized to bytes, together with the content type and optional
    /// <c>Content-Encoding</c> to send. A <see langword="default"/> value carries no body.
    /// </summary>
    private readonly record struct PreparedBody(byte[]? Bytes, string ContentType, string? ContentEncoding);

    /// <summary>
    /// Returns <c>min(overall, base * 2^(attempt-1))</c>: the first attempt uses <paramref name="baseTimeout"/>
    /// as given (it is not pre-capped by the caller — capping happens only here); each retry doubles it (a
    /// slow-but-progressing connection gets more time), and every attempt, including the first, is capped at
    /// <paramref name="overall"/> — the configured overall budget (<c>ApifyClientOptions.TimeoutSecs</c>).
    /// This is what keeps an explicit <c>WithTimeout</c> override above that budget from exceeding it,
    /// matching the reference client's <c>timeoutMaxSecs</c> cap.
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

    private static bool IsStatusRetryable(int status) => status == RateLimitExceeded || status >= MinServerError;

    /// <summary>Returns a delay chosen randomly from <c>[delay, 2*delay)</c> (exponential backoff + jitter).</summary>
    private static double RandomizedDelayMillis(double delayMillis)
    {
        if (delayMillis <= 0)
        {
            return delayMillis;
        }

        return delayMillis + (Random.Shared.NextDouble() * delayMillis);
    }

    /// <summary>Builds an <see cref="ApifyApiException"/> from an API error response body.</summary>
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

    /// <summary>Returns the path+query portion of a URL, for error reporting.</summary>
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

    /// <summary>Reports whether an exception represents a "resource not found" API error.</summary>
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
