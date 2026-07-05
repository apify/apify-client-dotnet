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
}
