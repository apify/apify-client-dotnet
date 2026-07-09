using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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

    /// <summary>The <c>Content-Encoding</c> token used for brotli-compressed request bodies.</summary>
    private const string BrotliEncoding = "br";

    private readonly IHttpTransport _transport;
    private readonly string? _token;
    private readonly RetryConfig _retry;

    public HttpClientCore(IHttpTransport transport, string? token, string userAgent, RetryConfig retry)
    {
        _transport = transport;
        _token = token;
        UserAgent = userAgent;
        _retry = retry;
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
        CancellationToken cancellationToken = default)
    {
        var delayMillis = _retry.MinDelayMillis;
        var maxAttempts = _retry.MaxRetries + 1;
        var path = ExtractPath(url);
        var baseTimeout = timeout ?? TimeSpan.FromSeconds(_retry.TimeoutSecs);
        // Normalize (and, when large enough, compress) the body once up front so retries reuse the same
        // prepared payload instead of re-encoding and re-compressing on every attempt.
        var prepared = PrepareBody(body, bodyBytes, contentType);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            bool retryable;
            try
            {
                var response = await SendOnceAsync(
                    method, url, prepared, extraHeaders,
                    AttemptTimeout(baseTimeout, attempt), cancellationToken).ConfigureAwait(false);

                var status = (int)response.StatusCode;
                if (status < MaxSuccessStatus)
                {
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

        if (extraHeaders is not null)
        {
            foreach (var header in extraHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

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
    /// Bodies at or above <see cref="MinCompressBytes"/> are brotli-compressed (<c>Content-Encoding: br</c>),
    /// matching the reference client, which prefers brotli and only falls back to gzip on runtimes where
    /// brotli is unavailable. .NET's <see cref="BrotliStream"/> is always available, so brotli is always
    /// used here and a gzip fallback would be unreachable.
    /// </remarks>
    private static PreparedBody PrepareBody(string? body, byte[]? bodyBytes, string contentType)
    {
        var raw = bodyBytes ?? (body is not null ? Encoding.UTF8.GetBytes(body) : null);
        if (raw is null)
        {
            return default;
        }

        return raw.Length >= MinCompressBytes
            ? new PreparedBody(BrotliCompress(raw), contentType, BrotliEncoding)
            : new PreparedBody(raw, contentType, null);
    }

    /// <summary>Brotli-compresses a payload into a self-contained byte array.</summary>
    private static byte[] BrotliCompress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionMode.Compress))
        {
            brotli.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    /// <summary>
    /// A request body normalized to bytes, together with the content type and optional
    /// <c>Content-Encoding</c> to send. A <see langword="default"/> value carries no body.
    /// </summary>
    private readonly record struct PreparedBody(byte[]? Bytes, string ContentType, string? ContentEncoding);

    /// <summary>
    /// Returns <c>min(overall, base * 2^(attempt-1))</c>: the first attempt uses the base timeout; each
    /// retry doubles it (a slow-but-progressing connection gets more time) while never exceeding the
    /// overall budget.
    /// </summary>
    private TimeSpan AttemptTimeout(TimeSpan baseTimeout, int attempt)
    {
        var overall = TimeSpan.FromSeconds(_retry.TimeoutSecs);
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

        return new ApifyApiException(status, type, message, attempt, method, path, data);
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
