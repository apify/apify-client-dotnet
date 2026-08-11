using System.Text.Json.Nodes;
using Apify.Client.Internal;

namespace Apify.Client.Models;

/// <summary>The result of releasing all of a client's request locks on a queue.</summary>
public sealed class UnlockRequestsResult
{
    private UnlockRequestsResult(long unlockedCount)
    {
        UnlockedCount = unlockedCount;
    }

    /// <summary>Builds a result from the decoded response object.</summary>
    /// <param name="data">The decoded response object.</param>
    public static UnlockRequestsResult FromData(JsonNode? data)
    {
        var obj = JsonValues.AsObject(data);
        return new UnlockRequestsResult(JsonValues.IntOr(obj, "unlockedCount", 0));
    }

    /// <summary>The number of requests that were unlocked.</summary>
    public long UnlockedCount { get; }
}
