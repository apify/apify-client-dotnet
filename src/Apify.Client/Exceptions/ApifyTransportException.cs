using System;

namespace Apify.Client.Exceptions;

/// <summary>
/// Marks a transport-level (network/timeout) failure, which is retryable by the client.
/// </summary>
/// <remarks>
/// A custom <see cref="Apify.Client.Http.IHttpTransport"/> should throw this for connection, DNS and
/// timeout failures; a non-2xx HTTP status must be returned as a normal response instead.
/// </remarks>
public sealed class ApifyTransportException : Exception
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
