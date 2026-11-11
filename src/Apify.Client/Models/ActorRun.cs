using System.Text.Json.Nodes;
using Apify.Client.Internal;

namespace Apify.Client.Models;

/// <summary>A single execution of an Actor.</summary>
public sealed class ActorRun : ApifyResource
{
    /// <summary>Wraps a raw run object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public ActorRun(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique run ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The ID of the Actor that produced this run.</summary>
    public string? ActId => GetString("actId");

    /// <summary>The ID of the task that started this run, if any.</summary>
    public string? ActorTaskId => GetString("actorTaskId");

    /// <summary>The ID of the user who owns the run.</summary>
    public string? UserId => GetString("userId");

    /// <summary>
    /// The run status: <c>READY</c>, <c>RUNNING</c>, <c>SUCCEEDED</c>, <c>FAILED</c>, <c>TIMING-OUT</c>, <c>TIMED-OUT</c>,
    /// <c>ABORTING</c> or <c>ABORTED</c>.
    /// </summary>
    public string? Status => GetString("status");

    /// <summary>An optional human-readable status message.</summary>
    public string? StatusMessage => GetString("statusMessage");

    /// <summary>When the run started (ISO 8601).</summary>
    public string? StartedAt => GetString("startedAt");

    /// <summary>When the run finished; absent while it is running.</summary>
    public string? FinishedAt => GetString("finishedAt");

    /// <summary>The ID of the build used for the run.</summary>
    public string? BuildId => GetString("buildId");

    /// <summary>The ID of the run's default dataset.</summary>
    public string? DefaultDatasetId => GetString("defaultDatasetId");

    /// <summary>The ID of the run's default key-value store.</summary>
    public string? DefaultKeyValueStoreId => GetString("defaultKeyValueStoreId");

    /// <summary>The ID of the run's default request queue.</summary>
    public string? DefaultRequestQueueId => GetString("defaultRequestQueueId");

    /// <summary>The URL of the run's container, as an RFC 3986 absolute URI (see <see cref="ApifyResource.GetUrlString"/>).</summary>
    public string? ContainerUrl => GetUrlString("containerUrl");

    /// <summary>Whether the run has reached a terminal status.</summary>
    public bool IsTerminal => Statuses.IsTerminal(Status);

    /// <summary>The URL of the run in Apify Console, as an RFC 3986 absolute URI (see <see cref="ApifyResource.GetUrlString"/>).</summary>
    public string? ConsoleUrl => GetUrlString("consoleUrl");

    /// <summary>
    /// The Server Actor deployment of the run: an object whose <c>deployment</c> names the pool that serves it
    /// (<c>SINGLE_TENANT</c>, <c>MULTI_TENANT_OWNER_PAID</c>, <c>MULTI_TENANT_FREE_USERS</c> or
    /// <c>MULTI_TENANT_PAYING_USERS</c>). <c>null</c> for a run not started by a Server Actor request.
    /// </summary>
    public JsonObject? Standby => Get("standby") as JsonObject;
}
