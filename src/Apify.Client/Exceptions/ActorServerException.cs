namespace Apify.Client.Exceptions;

/// <summary>
/// Thrown by <see cref="Resources.ActorServerClient"/> when the Actor cannot be used as a Server Actor: it does
/// not serve a web server, or the build that should serve its server interface does not exist. A missing Actor
/// is reported as the usual <see cref="ApifyApiException"/> instead. Matches the reference client's
/// <c>ApifyClientError</c> for these cases.
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
