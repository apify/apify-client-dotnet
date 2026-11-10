using System;

namespace Apify.Client.Exceptions;

/// <summary>A network or timeout failure, which the client retries.</summary>
/// <remarks>
/// A custom <see cref="Apify.Client.Http.IHttpTransport"/> throws this for connection, DNS and timeout failures, and
/// returns a non-2xx status as a response.
/// </remarks>
public sealed class ApifyTransportException : ApifyClientException
{
    /// <summary>Creates a transport exception.</summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    /// <param name="isTimeout">Whether the failure was caused by a request timeout.</param>
    public ApifyTransportException(string message, Exception? innerException = null, bool isTimeout = false)
        : base(message, innerException)
    {
        IsTimeout = isTimeout;
    }

    /// <summary>Whether the failure was caused by a request timeout.</summary>
    public bool IsTimeout { get; }
}
