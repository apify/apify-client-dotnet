namespace Apify.Client.Exceptions;

/// <summary>
/// Thrown by <see cref="Resources.ActorServerClient"/> when the Actor cannot be used as a Server Actor: it has no web
/// server, or the build that should serve its server interface does not exist. A missing Actor throws an
/// <see cref="ApifyApiException"/> instead.
/// </summary>
public sealed class ActorServerException : ApifyClientException
{
    /// <summary>Creates a Server Actor exception with the given message.</summary>
    /// <param name="message">A description of the failure.</param>
    public ActorServerException(string message)
        : base(message)
    {
    }
}
