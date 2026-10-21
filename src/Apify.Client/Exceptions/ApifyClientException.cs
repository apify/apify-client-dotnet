using System;

namespace Apify.Client.Exceptions;

/// <summary>
/// Base class of every error this client throws for a failed API call: <see cref="ApifyApiException"/>
/// (and its per-status subclasses), <see cref="ApifyTransportException"/>,
/// <see cref="InvalidResponseBodyException"/> and <see cref="ResponseValidationException"/>. A single
/// <c>catch (ApifyClientException)</c> catches all of them, matching the reference client's
/// <c>ApifyClientError</c>.
/// </summary>
/// <remarks>
/// An invalid argument throws a standard <see cref="ArgumentException"/>/<see cref="ArgumentOutOfRangeException"/>
/// before any request is sent; neither of those extends this class.
/// </remarks>
public abstract class ApifyClientException : Exception
{
    /// <summary>Creates a client exception with the given message.</summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    protected ApifyClientException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
