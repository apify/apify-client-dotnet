using System.Globalization;
using System.Linq;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Response bodies that satisfy the schemas of the Apify OpenAPI specification (the client rejects a
/// successful response that does not), built with only the properties the specification requires.
/// </summary>
internal static class SpecFixtures
{
    private const string Timestamp = "2026-01-01T00:00:00.000Z";

    /// <summary>A paginated list response (<c>{"data": {"total", "offset", "limit", "desc", "count", "items"}}</c>).</summary>
    /// <param name="total">The reported total number of items.</param>
    /// <param name="items">The raw JSON of each item on this page.</param>
    public static string Page(int total, params string[] items)
        => string.Format(
            CultureInfo.InvariantCulture,
            "{{\"data\":{{\"total\":{0},\"offset\":0,\"limit\":1000,\"desc\":false,\"count\":{1},\"items\":[{2}]}}}}",
            total,
            items.Length,
            string.Join(",", items));

    /// <summary>A paginated list response whose reported <c>count</c> (rows scanned) differs from the items returned.</summary>
    public static string PageWithCount(int total, int count, params string[] items)
        => Page(total, items).Replace("\"count\":" + items.Length.ToString(CultureInfo.InvariantCulture), "\"count\":" + count.ToString(CultureInfo.InvariantCulture), System.StringComparison.Ordinal);

    /// <summary>An item of the Actor collection.</summary>
    public static string ActorItem(string id)
        => "{\"id\":\"" + id + "\",\"createdAt\":\"" + Timestamp + "\",\"modifiedAt\":\"" + Timestamp + "\",\"name\":\"n\",\"username\":\"u\"}";

    /// <summary>An item of the Actor task collection.</summary>
    public static string TaskItem(string id)
        => "{\"id\":\"" + id + "\",\"userId\":\"u\",\"actId\":\"a\",\"name\":\"n\",\"createdAt\":\"" + Timestamp + "\",\"modifiedAt\":\"" + Timestamp + "\"}";

    /// <summary>An item of the schedule collection.</summary>
    public static string ScheduleItem(string id)
        => "{\"id\":\"" + id + "\",\"userId\":\"u\",\"name\":\"n\",\"cronExpression\":\"0 0 * * *\",\"timezone\":\"UTC\",\"isEnabled\":false,\"isExclusive\":false,\"createdAt\":\"" + Timestamp + "\",\"modifiedAt\":\"" + Timestamp + "\",\"actions\":[]}";

    /// <summary>An item of the Store collection.</summary>
    public static string StoreItem(string id)
        => "{\"id\":\"" + id + "\",\"title\":\"t\",\"name\":\"n\",\"username\":\"u\",\"stats\":{}}";

    /// <summary>A page of Actors listing the given IDs.</summary>
    public static string ActorPage(int total, params string[] ids) => Page(total, ids.Select(ActorItem).ToArray());

    /// <summary>An Actor (<c>GET /v2/actors/{actorId}</c> and the Actor update responses).</summary>
    public static string Actor(string id, string name = "n", bool isPublic = false)
        => "{\"data\":{\"id\":\"" + id + "\",\"userId\":\"u\",\"name\":\"" + name + "\",\"username\":\"u\",\"isPublic\":" + (isPublic ? "true" : "false")
            + ",\"createdAt\":\"" + Timestamp + "\",\"modifiedAt\":\"" + Timestamp + "\",\"stats\":{},\"versions\":[],\"defaultRunOptions\":{}}}";

    /// <summary>An Actor run.</summary>
    public static string Run(string id, string status = "SUCCEEDED")
        => "{\"data\":" + RunObject(id, status) + "}";

    /// <summary>The bare run object, for tests that compose a larger body.</summary>
    public static string RunObject(string id, string status = "SUCCEEDED")
        => "{\"id\":\"" + id + "\",\"actId\":\"a\",\"userId\":\"u\",\"startedAt\":\"" + Timestamp + "\",\"status\":\"" + status
            + "\",\"meta\":{\"origin\":\"API\"},\"stats\":{},\"options\":{\"build\":\"latest\",\"timeoutSecs\":0,\"memoryMbytes\":256,\"diskMbytes\":0},\"buildId\":\"b\""
            + ",\"defaultKeyValueStoreId\":\"kvs\",\"defaultDatasetId\":\"ds\",\"defaultRequestQueueId\":\"rq\"}";

    /// <summary>An Actor build.</summary>
    public static string Build(string id, string status = "SUCCEEDED")
        => "{\"data\":{\"id\":\"" + id + "\",\"actId\":\"a\",\"userId\":\"u\",\"startedAt\":\"" + Timestamp + "\",\"status\":\"" + status
            + "\",\"meta\":{\"origin\":\"API\"},\"buildNumber\":\"0.0.1\"}}";

    /// <summary>An Actor task.</summary>
    /// <param name="id">The task ID.</param>
    /// <param name="extraProperties">Raw JSON members appended to the task object (e.g. <c>"isPublic":true</c>).</param>
    public static string Task(string id, string? extraProperties = null)
    {
        var item = TaskItem(id);
        return "{\"data\":" + (extraProperties is null ? item : item[..^1] + "," + extraProperties + "}") + "}";
    }

    /// <summary>A dataset with the given reported item count.</summary>
    public static string Dataset(int itemCount = 0)
        => "{\"data\":{\"id\":\"ds\",\"userId\":\"u\",\"createdAt\":\"" + Timestamp + "\",\"modifiedAt\":\"" + Timestamp + "\",\"accessedAt\":\"" + Timestamp
            + "\",\"itemCount\":" + itemCount.ToString(CultureInfo.InvariantCulture) + ",\"cleanItemCount\":0,\"consoleUrl\":\"https://console.apify.com\"}}";

    /// <summary>A page of key-value store keys.</summary>
    /// <param name="limit">The page limit reported by the API.</param>
    /// <param name="isTruncated">Whether more keys follow.</param>
    /// <param name="nextExclusiveStartKey">The key to continue after, or <c>null</c>.</param>
    /// <param name="keys">The keys on this page.</param>
    public static string KeysPage(int limit, bool isTruncated, string? nextExclusiveStartKey, params string[] keys)
        => "{\"data\":{\"items\":[" + string.Join(",", keys.Select(static k => "{\"key\":\"" + k + "\",\"size\":1,\"recordPublicUrl\":\"https://example.com/" + k + "\"}")) + "],\"count\":"
            + keys.Length.ToString(CultureInfo.InvariantCulture) + ",\"limit\":" + limit.ToString(CultureInfo.InvariantCulture) + ",\"isTruncated\":" + (isTruncated ? "true" : "false")
            + (nextExclusiveStartKey is null ? string.Empty : ",\"nextExclusiveStartKey\":\"" + nextExclusiveStartKey + "\"") + "}}";

    /// <summary>A request queue head with the given raw request items.</summary>
    public static string QueueHead(params string[] items)
        => "{\"data\":{\"limit\":10,\"queueModifiedAt\":\"" + Timestamp + "\",\"hadMultipleClients\":false,\"items\":[" + string.Join(",", items) + "]}}";

    /// <summary>A page of request queue requests (<c>GET /v2/request-queues/{queueId}/requests</c>) with the given raw request items.</summary>
    public static string ListRequestsPage(int limit, params string[] items)
        => "{\"data\":{\"items\":[" + string.Join(",", items) + "],\"limit\":" + limit.ToString(CultureInfo.InvariantCulture) + "}}";
}
