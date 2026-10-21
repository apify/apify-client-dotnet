using System;

namespace Apify.Client.Exceptions;

/// <summary>
/// Thrown when a successful response is valid JSON but does not have the shape the Apify API
/// specification promises, for example it lacks the <c>data</c> envelope or carries an array where the
/// endpoint returns an object. The response arrived in full, so the request is not retried. Matches the
/// reference client's <c>ResponseValidationError</c>.
/// </summary>
public sealed class ResponseValidationException : ApifyClientException
{
    /// <summary>Creates a response-validation exception.</summary>
    /// <param name="message">A description of how the response deviates from the specification.</param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <param name="httpMethod">The HTTP method of the request that produced the response.</param>
    /// <param name="path">The API endpoint path of the request (URL excluding origin).</param>
    /// <param name="body">The raw response body that failed validation.</param>
    public ResponseValidationException(string message, int statusCode, string httpMethod, string path, string body)
        : base(message)
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

    /// <summary>The raw response body that failed validation.</summary>
    public string Body { get; }
}
