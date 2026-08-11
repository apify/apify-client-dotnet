using System.Collections.Generic;
using System.Text.Json.Nodes;
using Apify.Client.Internal;

namespace Apify.Client.Models;

/// <summary>A batch of requests from the head of a request queue, locked for exclusive processing.</summary>
public sealed class LockedRequestQueueHead
{
    private LockedRequestQueueHead(
        IReadOnlyList<RequestQueueRequest> items,
        long limit,
        bool hadMultipleClients,
        long lockSecs,
        bool? queueHasLockedRequests,
        string? clientKey)
    {
        Items = items;
        Limit = limit;
        HadMultipleClients = hadMultipleClients;
        LockSecs = lockSecs;
        QueueHasLockedRequests = queueHasLockedRequests;
        ClientKey = clientKey;
    }

    /// <summary>Builds a locked head from the decoded response object.</summary>
    /// <param name="data">The decoded response object.</param>
    public static LockedRequestQueueHead FromData(JsonNode? data)
    {
        var obj = JsonValues.AsObject(data);
        var items = new List<RequestQueueRequest>();
        foreach (var item in JsonValues.ObjectItems(obj))
        {
            items.Add(RequestQueueRequest.FromJsonObject(item));
        }

        return new LockedRequestQueueHead(
            items,
            JsonValues.IntOr(obj, "limit", items.Count),
            JsonValues.BoolOr(obj, "hadMultipleClients", false),
            JsonValues.IntOr(obj, "lockSecs", 0),
            obj.ContainsKey("queueHasLockedRequests") ? JsonValues.BoolOr(obj, "queueHasLockedRequests", false) : null,
            JsonValues.String(obj, "clientKey"));
    }

    /// <summary>The locked requests from the head of the queue. Each carries its own <c>LockExpiresAt</c>.</summary>
    public IReadOnlyList<RequestQueueRequest> Items { get; }

    /// <summary>The maximum number of requests requested.</summary>
    public long Limit { get; }

    /// <summary>Whether multiple clients have accessed the queue.</summary>
    public bool HadMultipleClients { get; }

    /// <summary>The lock duration applied to every returned request, in seconds.</summary>
    public long LockSecs { get; }

    /// <summary>Whether the queue has any requests locked by any client (this one or another).</summary>
    public bool? QueueHasLockedRequests { get; }

    /// <summary>The client key used to acquire the locks.</summary>
    public string? ClientKey { get; }
}
