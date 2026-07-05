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
    /// The current run status. One of the eight <c>ActorJobStatus</c> values: <c>READY</c>, <c>RUNNING</c>,
    /// <c>SUCCEEDED</c>, <c>FAILED</c>, <c>TIMING-OUT</c>, <c>TIMED-OUT</c>, <c>ABORTING</c>, <c>ABORTED</c>.
    /// </summary>
    public string? Status => GetString("status");

    /// <summary>An optional human-readable status message.</summary>
    public string? StatusMessage => GetString("statusMessage");

    /// <summary>When the run started (ISO-8601 string).</summary>
    public string? StartedAt => GetString("startedAt");

    /// <summary>When the run finished (absent while still running).</summary>
    public string? FinishedAt => GetString("finishedAt");

    /// <summary>The ID of the build used for the run.</summary>
    public string? BuildId => GetString("buildId");

    /// <summary>The ID of the run's default dataset.</summary>
    public string? DefaultDatasetId => GetString("defaultDatasetId");

    /// <summary>The ID of the run's default key-value store.</summary>
    public string? DefaultKeyValueStoreId => GetString("defaultKeyValueStoreId");

    /// <summary>The ID of the run's default request queue.</summary>
    public string? DefaultRequestQueueId => GetString("defaultRequestQueueId");

    /// <summary>The URL of the run's container (for live access).</summary>
    public string? ContainerUrl => GetString("containerUrl");

    /// <summary>Whether the run has reached a terminal (finished) status.</summary>
    public bool IsTerminal => Statuses.IsTerminal(Status);
}
