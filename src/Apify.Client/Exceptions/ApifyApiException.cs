using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Apify.Client.Exceptions;

/// <summary>
/// Thrown for HTTP requests that reach the Apify API but receive a non-success status code.
/// </summary>
/// <remarks>
/// It mirrors the <c>ApifyApiError</c> of the reference JavaScript client and exposes the parsed error
/// <see cref="Type"/>, the human-readable <see cref="ApiMessage"/>, the HTTP <see cref="StatusCode"/>,
/// the number of the final <see cref="Attempt"/>, and the request <see cref="HttpMethod"/>/<see cref="Path"/>.
/// </remarks>
public class ApifyApiException : Exception
{
    /// <summary>Creates an API exception with the parsed error details.</summary>
    /// <param name="statusCode">The HTTP status code of the error response.</param>
    /// <param name="type">The machine-readable error type returned by the API, if any.</param>
    /// <param name="message">The raw error message returned by the API.</param>
    /// <param name="attempt">The 1-based number of the attempt that produced this error.</param>
    /// <param name="httpMethod">The HTTP method of the API call.</param>
    /// <param name="path">The path of the API endpoint (URL excluding origin).</param>
    /// <param name="data">Additional structured error data provided by the API, if any.</param>
    public ApifyApiException(
        int statusCode,
        string? type,
        string message,
        int attempt,
        string httpMethod,
        string path,
        JsonObject? data = null)
        : base(FormatMessage(statusCode, type, message))
    {
        StatusCode = statusCode;
        Type = type;
        ApiMessage = message;
        Attempt = attempt;
        HttpMethod = httpMethod;
        Path = path;
        ErrorData = data;
    }

    /// <summary>The HTTP status code of the error response.</summary>
    public int StatusCode { get; }

    /// <summary>The machine-readable error type returned by the API (e.g. <c>record-not-found</c>).</summary>
    public string? Type { get; }

    /// <summary>The raw error message returned by the API, without the status/type prefix.</summary>
    public string ApiMessage { get; }

    /// <summary>The number of the API call attempt that produced this error (1-based).</summary>
    public int Attempt { get; }

    /// <summary>The HTTP method of the API call (e.g. <c>GET</c>, <c>POST</c>).</summary>
    public string HttpMethod { get; }

    /// <summary>The path of the API endpoint (URL excluding origin).</summary>
    public string Path { get; }

    /// <summary>
    /// Additional structured data provided by the API about the error, if any. Named <c>ErrorData</c>
    /// (not <c>Data</c>) to avoid hiding <see cref="System.Exception.Data"/>.
    /// </summary>
    public JsonObject? ErrorData { get; }

    private static string FormatMessage(int statusCode, string? type, string message)
    {
        var errType = string.IsNullOrEmpty(type) ? "unknown" : type;
        return string.Format(
            CultureInfo.InvariantCulture,
            "apify API error (status {0}, type {1}): {2}",
            statusCode,
            errType,
            message);
    }

    /// <summary>HTTP status marking a client-side bad request.</summary>
    private const int StatusBadRequest = 400;

    /// <summary>HTTP status marking a missing or invalid API token.</summary>
    private const int StatusUnauthorized = 401;

    /// <summary>HTTP status marking a request the token is not permitted to make.</summary>
    private const int StatusForbidden = 403;

    /// <summary>HTTP status marking a resource that does not exist.</summary>
    private const int StatusNotFound = 404;

    /// <summary>HTTP status marking a request that conflicts with the resource's current state.</summary>
    private const int StatusConflict = 409;

    /// <summary>HTTP status marking a rate-limited request.</summary>
    private const int StatusTooManyRequests = 429;

    /// <summary>Smallest HTTP status treated as a server-side error.</summary>
    private const int StatusServerErrorFloor = 500;

    /// <summary>
    /// Creates the <see cref="ApifyApiException"/> subclass matching <paramref name="statusCode"/> (see the
    /// per-status types in this namespace, e.g. <see cref="NotFoundException"/>), so callers can branch with
    /// a type check instead of comparing status codes or error-type strings. Any other status falls back to
    /// the base <see cref="ApifyApiException"/>; every subclass still satisfies an
    /// <c>is ApifyApiException</c>/<c>catch (ApifyApiException)</c> check, matching the reference client's
    /// <c>ApifyApiError</c> subclass hierarchy.
    /// </summary>
    internal static ApifyApiException Create(
        int statusCode,
        string? type,
        string message,
        int attempt,
        string httpMethod,
        string path,
        System.Text.Json.Nodes.JsonObject? data = null)
    {
        return statusCode switch
        {
            StatusBadRequest => new InvalidRequestException(statusCode, type, message, attempt, httpMethod, path, data),
            StatusUnauthorized => new UnauthorizedException(statusCode, type, message, attempt, httpMethod, path, data),
            StatusForbidden => new ForbiddenException(statusCode, type, message, attempt, httpMethod, path, data),
            StatusNotFound => new NotFoundException(statusCode, type, message, attempt, httpMethod, path, data),
            StatusConflict => new ConflictException(statusCode, type, message, attempt, httpMethod, path, data),
            StatusTooManyRequests => new RateLimitException(statusCode, type, message, attempt, httpMethod, path, data),
            >= StatusServerErrorFloor => new ServerException(statusCode, type, message, attempt, httpMethod, path, data),
            _ => new ApifyApiException(statusCode, type, message, attempt, httpMethod, path, data),
        };
    }
}
