using System.Collections.Generic;
using System.Text.Json.Nodes;
using Apify.Client.Internal;

namespace Apify.Client.Models;

/// <summary>
/// The result of a batch request-delete: the requests that were successfully removed and the ones that
/// could not be (and can be retried).
/// </summary>
public sealed class BatchDeleteResult
{
    private BatchDeleteResult(IReadOnlyList<RequestQueueRequest> processed, IReadOnlyList<RequestQueueRequest> unprocessed)
    {
        ProcessedRequests = processed;
        UnprocessedRequests = unprocessed;
    }

    /// <summary>Builds a result from the decoded response object.</summary>
    /// <param name="data">The decoded response object.</param>
    public static BatchDeleteResult FromData(JsonNode? data)
    {
        var obj = JsonValues.AsObject(data);
        return new BatchDeleteResult(Hydrate(obj, "processedRequests"), Hydrate(obj, "unprocessedRequests"));
    }

    private static List<RequestQueueRequest> Hydrate(JsonObject obj, string key)
    {
        var items = new List<RequestQueueRequest>();
        if (obj.TryGetPropertyValue(key, out var node) && node is JsonArray array)
        {
            foreach (var item in array)
            {
                items.Add(RequestQueueRequest.FromJsonObject(item as JsonObject ?? new JsonObject()));
            }
        }

        return items;
    }

    /// <summary>The requests that were successfully deleted from the queue.</summary>
    public IReadOnlyList<RequestQueueRequest> ProcessedRequests { get; }

    /// <summary>The requests that failed to be deleted and can be retried.</summary>
    public IReadOnlyList<RequestQueueRequest> UnprocessedRequests { get; }
}
