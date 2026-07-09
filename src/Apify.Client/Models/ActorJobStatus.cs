using System;

namespace Apify.Client.Models;

/// <summary>
/// The lifecycle status of an Actor run or build (the <c>ActorJobStatus</c> schema in the Apify API).
/// </summary>
public enum ActorJobStatus
{
    /// <summary>The job is queued and about to start.</summary>
    Ready,

    /// <summary>The job is currently running.</summary>
    Running,

    /// <summary>The job finished successfully (terminal).</summary>
    Succeeded,

    /// <summary>The job failed (terminal).</summary>
    Failed,

    /// <summary>The job exceeded its timeout and is being terminated.</summary>
    TimingOut,

    /// <summary>The job was terminated because it exceeded its timeout (terminal).</summary>
    TimedOut,

    /// <summary>The job is being aborted.</summary>
    Aborting,

    /// <summary>The job was aborted (terminal).</summary>
    Aborted,
}

/// <summary>Maps <see cref="ActorJobStatus"/> to and from its API wire representation.</summary>
public static class ActorJobStatusExtensions
{
    /// <summary>The wire value the API uses for this status (e.g. <c>TIMING-OUT</c>).</summary>
    public static string ToWireValue(this ActorJobStatus status) => status switch
    {
        ActorJobStatus.Ready => "READY",
        ActorJobStatus.Running => "RUNNING",
        ActorJobStatus.Succeeded => "SUCCEEDED",
        ActorJobStatus.Failed => "FAILED",
        ActorJobStatus.TimingOut => "TIMING-OUT",
        ActorJobStatus.TimedOut => "TIMED-OUT",
        ActorJobStatus.Aborting => "ABORTING",
        ActorJobStatus.Aborted => "ABORTED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "unknown job status"),
    };

    /// <summary>
    /// Whether this status is terminal: a run/build in a terminal status is finished and will not change.
    /// </summary>
    public static bool IsTerminal(this ActorJobStatus status) => status is
        ActorJobStatus.Succeeded or ActorJobStatus.Failed or ActorJobStatus.Aborted or ActorJobStatus.TimedOut;

    /// <summary>
    /// Parses an API wire value into an <see cref="ActorJobStatus"/>, or returns <c>null</c> if the value is
    /// absent or not a recognized status. The raw string remains available via the model's
    /// <see cref="ApifyResource.Get"/>.
    /// </summary>
    internal static ActorJobStatus? FromWireValue(string? value) => value switch
    {
        "READY" => ActorJobStatus.Ready,
        "RUNNING" => ActorJobStatus.Running,
        "SUCCEEDED" => ActorJobStatus.Succeeded,
        "FAILED" => ActorJobStatus.Failed,
        "TIMING-OUT" => ActorJobStatus.TimingOut,
        "TIMED-OUT" => ActorJobStatus.TimedOut,
        "ABORTING" => ActorJobStatus.Aborting,
        "ABORTED" => ActorJobStatus.Aborted,
        _ => null,
    };
}
