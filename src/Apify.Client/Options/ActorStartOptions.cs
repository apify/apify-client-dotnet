using System;
using System.Text;
using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Configures starting an Actor run. All fields are optional.</summary>
public sealed class ActorStartOptions
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

    /// <summary>The content type of the input body. Defaults to <c>application/json</c>.</summary>
    public string? ContentType { get; init; }

    /// <summary>If <c>true</c>, restart the run if it fails.</summary>
    public bool? RestartOnError { get; init; }

    /// <summary>
    /// Retries the start while the account lacks the memory or a concurrent-run slot
    /// (<c>actor-memory-limit-exceeded</c>, <c>concurrent-runs-limit-exceeded</c>) instead of throwing. <c>null</c> (the
    /// default) does not retry.
    /// </summary>
    public WaitForResources? WaitForResources { get; init; }

    /// <summary>Overrides the Actor's permission level for this run (<c>LIMITED_PERMISSIONS</c> or <c>FULL_PERMISSIONS</c>).</summary>
    public string? ForcePermissionLevel { get; init; }

    /// <summary>Ad-hoc webhooks for this run: a JSON-serializable list, sent as base64-encoded JSON in the <c>webhooks</c> query parameter.</summary>
    public object? Webhooks { get; init; }

    /// <summary>The configured content type, or the JSON default when unset.</summary>
    internal string ContentTypeOrDefault() =>
        string.IsNullOrEmpty(ContentType) ? ResourceContext.ContentTypeJson : ContentType;

    internal void AppendTo(QueryParams q)
    {
        q.AddString("build", Build)
            .AddInt("memory", MemoryMbytes)
            .AddInt("timeout", TimeoutSecs)
            .AddInt("waitForFinish", WaitForFinish)
            .AddInt("maxItems", MaxItems)
            .AddDouble("maxTotalChargeUsd", MaxTotalChargeUsd)
            .AddBool("restartOnError", RestartOnError)
            .AddString("forcePermissionLevel", ForcePermissionLevel)
            .AddString("webhooks", EncodeWebhooks(Webhooks));
    }

    /// <summary>
    /// Encodes ad-hoc webhooks as base64-encoded JSON for the <c>webhooks</c> query parameter; <c>null</c> for <c>null</c>.
    /// Shared by the Actor and task start options.
    /// </summary>
    internal static string? EncodeWebhooks(object? webhooks)
    {
        return webhooks is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(Json.Encode(webhooks)));
    }
}
