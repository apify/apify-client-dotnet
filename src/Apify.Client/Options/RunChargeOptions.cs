namespace Apify.Client.Options;

/// <summary>Configures charging for a pay-per-event Actor run.</summary>
public sealed class RunChargeOptions
{
    /// <summary>Creates charge options.</summary>
    /// <param name="eventName">The name of the event to charge for. Required.</param>
    /// <param name="count">The number of times to charge the event (defaults to 1).</param>
    /// <param name="idempotencyKey">
    /// A key that deduplicates the charge across retries. If unset, one is auto-generated as
    /// <c>"{runId}-{eventName}-{timestampMillis}-{random}"</c>, matching the reference client.
    /// </param>
    public RunChargeOptions(string eventName, int? count = null, string? idempotencyKey = null)
    {
        EventName = eventName;
        Count = count;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>The name of the event to charge for.</summary>
    public string EventName { get; }

    /// <summary>The number of times to charge the event (defaults to 1).</summary>
    public int? Count { get; }

    /// <summary>A key that deduplicates the charge across retries.</summary>
    public string? IdempotencyKey { get; }

    /// <summary>The count to send, defaulting to 1.</summary>
    internal int CountValue() => Count ?? 1;
}
