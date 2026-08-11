using System.Collections.Generic;
using System.Text.Json.Nodes;
using Apify.Client.Internal;

namespace Apify.Client.Models;

/// <summary>
/// A single, cursor-paginated page of a request queue's requests (as returned by
/// <see cref="Apify.Client.Resources.RequestQueueClient.ListRequestsAsync"/>).
/// </summary>
public sealed class RequestQueueRequestsPage
{
    private RequestQueueRequestsPage(
        IReadOnlyList<RequestQueueRequest> items,
        long limit,
        string? exclusiveStartId,
        string? cursor,
        string? nextCursor)
    {
        Items = items;
        Limit = limit;
        ExclusiveStartId = exclusiveStartId;
        Cursor = cursor;
        NextCursor = nextCursor;
    }

    /// <summary>Builds a page from the decoded response object.</summary>
    /// <param name="data">The decoded response object.</param>
    public static RequestQueueRequestsPage FromData(JsonNode? data)
    {
        var obj = JsonValues.AsObject(data);
        var items = new List<RequestQueueRequest>();
        foreach (var item in JsonValues.ObjectItems(obj))
        {
            items.Add(RequestQueueRequest.FromJsonObject(item));
        }

        return new RequestQueueRequestsPage(
            items,
            JsonValues.IntOr(obj, "limit", items.Count),
            JsonValues.String(obj, "exclusiveStartId"),
            JsonValues.String(obj, "cursor"),
            JsonValues.String(obj, "nextCursor"));
    }

    /// <summary>The requests in this page.</summary>
    public IReadOnlyList<RequestQueueRequest> Items { get; }

    /// <summary>The maximum number of requests requested for this page.</summary>
    public long Limit { get; }

    /// <summary>The ID of the last request of the previous page, if pagination was continued by ID.</summary>
    /// <remarks>Deprecated by the API in favor of <see cref="Cursor"/>/<see cref="NextCursor"/>.</remarks>
    public string? ExclusiveStartId { get; }

    /// <summary>The cursor that produced this page, if pagination was continued by cursor.</summary>
    public string? Cursor { get; }

    /// <summary>The cursor to pass to fetch the next page, or <c>null</c> if this is the last page.</summary>
    public string? NextCursor { get; }
}
