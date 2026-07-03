using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Configures resurrecting a finished run.</summary>
public sealed class RunResurrectOptions
{
    /// <summary>The tag or number of the build to resurrect with.</summary>
    public string? Build { get; init; }

    /// <summary>Memory in megabytes to allocate.</summary>
    public int? MemoryMbytes { get; init; }

    /// <summary>The run timeout in seconds.</summary>
    public int? TimeoutSecs { get; init; }

    /// <summary>Maximum number of dataset items to charge (pay-per-result Actors).</summary>
    public int? MaxItems { get; init; }

    /// <summary>Maximum total charge in USD (pay-per-event Actors).</summary>
    public double? MaxTotalChargeUsd { get; init; }

    /// <summary>If <c>true</c>, restart the run if it fails.</summary>
    public bool? RestartOnError { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddString("build", Build)
            .AddInt("memory", MemoryMbytes)
            .AddInt("timeout", TimeoutSecs)
            .AddInt("maxItems", MaxItems)
            .AddDouble("maxTotalChargeUsd", MaxTotalChargeUsd)
            .AddBool("restartOnError", RestartOnError);
    }
}
