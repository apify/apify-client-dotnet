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
    /// <param name="innerException">The JSON parse error, if any.</param>
    public InvalidResponseBodyException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
