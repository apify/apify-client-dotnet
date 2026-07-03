namespace Apify.Client.Options;

/// <summary>
/// Per-client options for a <see cref="Apify.Client.Resources.RequestQueueClient"/>, mirroring the
/// reference client's <c>requestQueue(id, { clientKey, timeoutSecs })</c>.
/// </summary>
public sealed class RequestQueueClientOptions
{
    /// <summary>
    /// A stable client key identifying this client to the queue. Required to operate on locks the client
    /// itself created, and lets the API detect whether multiple clients access the queue.
    /// </summary>
    public string? ClientKey { get; init; }

    /// <summary>
    /// Per-request timeout (seconds) for this queue client's calls. It shortens the wait for each call and
    /// is capped at the client-wide overall timeout, so a value larger than that timeout has no effect. When
    /// <c>null</c> the shared client-wide timeout is used.
    /// </summary>
    public double? TimeoutSecs { get; init; }
}
