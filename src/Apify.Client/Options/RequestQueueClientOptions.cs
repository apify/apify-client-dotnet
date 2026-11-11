namespace Apify.Client.Options;

/// <summary>Options of a <see cref="Apify.Client.Resources.RequestQueueClient"/>, as the reference client's <c>requestQueue(id, { clientKey, timeoutSecs })</c>.</summary>
public sealed class RequestQueueClientOptions
{
    /// <summary>
    /// A stable key that identifies this client to the queue. Needed to operate on locks the client created, and lets the API
    /// detect several clients on one queue.
    /// </summary>
    public string? ClientKey { get; init; }

    /// <summary>
    /// Per-request timeout in seconds for this queue client's calls, capped at the client-wide overall timeout. <c>null</c>
    /// uses the client-wide timeout.
    /// </summary>
    public double? TimeoutSecs { get; init; }
}
