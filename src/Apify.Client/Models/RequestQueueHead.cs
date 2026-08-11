using System.Collections.Generic;
using System.Text.Json.Nodes;
using Apify.Client.Internal;

namespace Apify.Client.Models;

/// <summary>The head (front) of a request queue.</summary>
public sealed class RequestQueueHead
{
    private RequestQueueHead(IReadOnlyList<RequestQueueRequest> items, long limit, string? queueModifiedAt, bool hadMultipleClients)
    {
        Items = items;
        Limit = limit;
        QueueModifiedAt = queueModifiedAt;
        HadMultipleClients = hadMultipleClients;
    }

    /// <summary>Builds a head from the decoded queue-head object.</summary>
    /// <param name="data">The decoded queue-head object.</param>
    public static RequestQueueHead FromData(JsonNode? data)
    {
        var obj = JsonValues.AsObject(data);
        var items = new List<RequestQueueRequest>();
        foreach (var item in JsonValues.ObjectItems(obj))
        {
            items.Add(RequestQueueRequest.FromJsonObject(item));
        }

        return new RequestQueueHead(
            items,
            JsonValues.IntOr(obj, "limit", items.Count),
            JsonValues.String(obj, "queueModifiedAt"),
            JsonValues.BoolOr(obj, "hadMultipleClients", false));
    }

    /// <summary>The requests at the head of the queue.</summary>
    public IReadOnlyList<RequestQueueRequest> Items { get; }

    /// <summary>The maximum number of requests requested.</summary>
    public long Limit { get; }

    /// <summary>ISO 8601 timestamp of the last modification to the queue.</summary>
    public string? QueueModifiedAt { get; }

    /// <summary>Whether multiple clients have accessed the queue.</summary>
    public bool HadMultipleClients { get; }
}
