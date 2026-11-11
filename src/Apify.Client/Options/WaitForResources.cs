using System;

namespace Apify.Client.Options;

/// <summary>
/// Configures retrying a run start while the account has no free memory or concurrent-run slot
/// (<c>actor-memory-limit-exceeded</c>, <c>concurrent-runs-limit-exceeded</c>), which clears as other runs finish. Passed
/// to <see cref="ActorStartOptions.WaitForResources"/> and <see cref="TaskStartOptions.WaitForResources"/>. The reference
/// client's <c>waitForResources</c> is a <c>boolean | number</c>; this value type keeps "retry forever" and "retry for N
/// seconds" distinct.
/// </summary>
public readonly struct WaitForResources : IEquatable<WaitForResources>
{
    private WaitForResources(double? maxWaitSecs)
    {
        MaxWaitSecs = maxWaitSecs;
    }

    /// <summary>The longest time to keep retrying in seconds, or <c>null</c> to retry until the run starts (see <see cref="UntilAvailable"/>).</summary>
    public double? MaxWaitSecs { get; }

    /// <summary>Retries indefinitely until the run starts.</summary>
    public static WaitForResources UntilAvailable { get; } = new(null);

    /// <summary>Retries for up to <paramref name="maxWaitSecs"/> seconds, then throws the last resource-limit error.</summary>
    /// <param name="maxWaitSecs">
    /// The retry budget in seconds; finite and not negative. Use <see cref="UntilAvailable"/>, not
    /// <see cref="double.PositiveInfinity"/>, to retry forever.
    /// </param>
    public static WaitForResources For(double maxWaitSecs)
    {
        if (maxWaitSecs < 0 || !double.IsFinite(maxWaitSecs))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxWaitSecs), maxWaitSecs, "must be a non-negative, finite number of seconds; use UntilAvailable to retry forever");
        }

        return new WaitForResources(maxWaitSecs);
    }

    /// <inheritdoc />
    public bool Equals(WaitForResources other) => Nullable.Equals(MaxWaitSecs, other.MaxWaitSecs);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is WaitForResources other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => MaxWaitSecs.GetHashCode();

    /// <summary>Equality by <see cref="MaxWaitSecs"/>.</summary>
    public static bool operator ==(WaitForResources left, WaitForResources right) => left.Equals(right);

    /// <summary>Inequality by <see cref="MaxWaitSecs"/>.</summary>
    public static bool operator !=(WaitForResources left, WaitForResources right) => !left.Equals(right);
}
