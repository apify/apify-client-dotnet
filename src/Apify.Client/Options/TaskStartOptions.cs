using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>
/// Configures starting a task run.
/// </summary>
/// <remarks>
/// It mirrors <see cref="ActorStartOptions"/> but omits the fields the task run endpoint does not accept
/// (the Actor-only <c>ContentType</c> and <c>ForcePermissionLevel</c>), matching the reference client.
/// </remarks>
public sealed class TaskStartOptions
{
    /// <summary>The tag or number of the build to run (e.g. <c>latest</c>, <c>0.1.2</c>).</summary>
    public string? Build { get; init; }

    /// <summary>Memory in megabytes allocated for the run.</summary>
    public int? MemoryMbytes { get; init; }

    /// <summary>Timeout for the run in seconds (0 means no timeout).</summary>
    public int? TimeoutSecs { get; init; }

    /// <summary>Maximum seconds to wait server-side for the run to finish (max 60).</summary>
    public int? WaitForFinish { get; init; }

    /// <summary>Maximum number of dataset items to charge (pay-per-result Actors).</summary>
    public int? MaxItems { get; init; }

    /// <summary>Maximum total charge in USD (pay-per-event Actors).</summary>
    public double? MaxTotalChargeUsd { get; init; }

    /// <summary>If <c>true</c>, restart the run if it fails.</summary>
    public bool? RestartOnError { get; init; }

    /// <summary>Ad-hoc webhooks to attach (a JSON-serializable list serialized to base64-encoded JSON).</summary>
    public object? Webhooks { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddString("build", Build)
            .AddInt("memory", MemoryMbytes)
            .AddInt("timeout", TimeoutSecs)
            .AddInt("waitForFinish", WaitForFinish)
            .AddInt("maxItems", MaxItems)
            .AddDouble("maxTotalChargeUsd", MaxTotalChargeUsd)
            .AddBool("restartOnError", RestartOnError)
            .AddString("webhooks", ActorStartOptions.EncodeWebhooks(Webhooks));
    }
}
