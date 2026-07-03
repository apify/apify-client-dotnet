using System.Text.Json.Nodes;
using Apify.Client.Internal;

namespace Apify.Client.Models;

/// <summary>A single build of an Actor.</summary>
public sealed class Build : ApifyResource
{
    /// <summary>Wraps a raw build object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public Build(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique build ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The ID of the Actor this build belongs to.</summary>
    public string? ActId => GetString("actId");

    /// <summary>
    /// The current build status. One of the eight <c>ActorJobStatus</c> values: <c>READY</c>, <c>RUNNING</c>,
    /// <c>SUCCEEDED</c>, <c>FAILED</c>, <c>TIMING-OUT</c>, <c>TIMED-OUT</c>, <c>ABORTING</c>, <c>ABORTED</c>.
    /// </summary>
    public string? Status => GetString("status");

    /// <summary>When the build started (ISO-8601 string).</summary>
    public string? StartedAt => GetString("startedAt");

    /// <summary>When the build finished (absent while still building).</summary>
    public string? FinishedAt => GetString("finishedAt");

    /// <summary>The human-readable build number (e.g. <c>0.1.2</c>).</summary>
    public string? BuildNumber => GetString("buildNumber");

    /// <summary>Whether the build has reached a terminal (finished) status.</summary>
    public bool IsTerminal => Statuses.IsTerminal(Status);
}
