using System.Text.Json.Nodes;
using Apify.Client.Internal;

namespace Apify.Client.Models;

/// <summary>The result of prolonging a request lock: the new lock expiry.</summary>
public sealed class RequestLockInfo
{
    private RequestLockInfo(string? lockExpiresAt)
    {
        LockExpiresAt = lockExpiresAt;
    }

    /// <summary>Builds a lock info from the decoded response object.</summary>
    /// <param name="data">The decoded response object.</param>
    public static RequestLockInfo FromData(JsonNode? data)
    {
        var obj = JsonValues.AsObject(data);
        return new RequestLockInfo(JsonValues.String(obj, "lockExpiresAt"));
    }

    /// <summary>When the (possibly just-extended) lock expires (ISO-8601 string).</summary>
    public string? LockExpiresAt { get; }
}
