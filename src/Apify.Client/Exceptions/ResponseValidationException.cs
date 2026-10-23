using System.Collections.Generic;

namespace Apify.Client.Exceptions;

/// <summary>
/// Thrown when a successful response is valid JSON but does not match the schema the Apify API
/// specification declares for its operation: the <c>data</c> envelope or a required property is missing, a
/// value has the wrong JSON type, is <c>null</c> where that is not allowed, or falls outside the documented
/// format, pattern or range. Unknown properties and unknown enum values are accepted. The response arrived
/// in full, so the request is not retried. Matches the reference client's <c>ResponseValidationError</c>.
/// </summary>
public sealed class ResponseValidationException : ApifyClientException
{
    /// <summary>Creates a response-validation exception.</summary>
    /// <param name="issues">Each problem found, as a location in the body and what is wrong with it.</param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <param name="httpMethod">The HTTP method of the request that produced the response.</param>
    /// <param name="path">The API endpoint path of the request (URL excluding origin).</param>
    /// <param name="body">The raw response body that failed validation.</param>
    public ResponseValidationException(IReadOnlyList<string> issues, int statusCode, string httpMethod, string path, string body)
        : base("response does not match the API specification: " + string.Join("; ", issues))
    {
        Issues = issues;
        StatusCode = statusCode;
        HttpMethod = httpMethod;
        Path = path;
        Body = body;
    }

    /// <summary>Each problem found, as a location in the body (e.g. <c>$.data.id</c>) and what is wrong with it.</summary>
    public IReadOnlyList<string> Issues { get; }

    /// <summary>The HTTP status code of the response.</summary>
    public int StatusCode { get; }

    /// <summary>The HTTP method of the request that produced the response (e.g. <c>GET</c>).</summary>
    public string HttpMethod { get; }

    /// <summary>The API endpoint path of the request (URL excluding origin).</summary>
    public string Path { get; }

    /// <summary>The raw response body that failed validation.</summary>
    public string Body { get; }
}
