using System;

namespace Apify.Client.Exceptions;

/// <summary>
/// Thrown when the body of a successful response does not parse as JSON, for example a proxy's HTML page
/// served with a success status. The response arrived in full, so the request is not retried. An error
/// status with an unparsable body throws the <see cref="ApifyApiException"/> for its status code instead.
/// Matches the reference client's <c>InvalidResponseBodyError</c>.
/// </summary>
public sealed class InvalidResponseBodyException : ApifyClientException
{
    /// <summary>Creates an invalid-response-body exception.</summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <param name="httpMethod">The HTTP method of the request that produced the response.</param>
    /// <param name="path">The API endpoint path of the request (URL excluding origin).</param>
    /// <param name="body">The raw response body that failed to parse.</param>
    /// <param name="innerException">The JSON parse error, if any.</param>
    public InvalidResponseBodyException(string message, int statusCode, string httpMethod, string path, string body, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        HttpMethod = httpMethod;
        Path = path;
        Body = body;
    }

    /// <summary>The HTTP status code of the response.</summary>
    public int StatusCode { get; }

    /// <summary>The HTTP method of the request that produced the response (e.g. <c>GET</c>).</summary>
    public string HttpMethod { get; }

    /// <summary>The API endpoint path of the request (URL excluding origin).</summary>
    public string Path { get; }

    /// <summary>The raw response body that failed to parse.</summary>
    public string Body { get; }
}
