using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>Returned when adding or updating a request in a queue.</summary>
public sealed class RequestQueueOperationInfo : ApifyResource
{
    /// <summary>Wraps a raw operation-info object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public RequestQueueOperationInfo(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The ID of the affected request.</summary>
    public string? RequestId => GetString("requestId");

    /// <summary>
    /// The unique key of the affected request. Populated for batch-add results; may be <c>null</c> for
    /// single add/update operations.
    /// </summary>
    public string? UniqueKey => GetString("uniqueKey");

    /// <summary>Whether the request was already in the queue.</summary>
    public bool? WasAlreadyPresent => GetBool("wasAlreadyPresent");

    /// <summary>Whether the request had already been handled.</summary>
    public bool? WasAlreadyHandled => GetBool("wasAlreadyHandled");
}
