using System;

namespace Apify.Client.Options;

/// <summary>
/// Configures retrying a run start while the account has no free memory or concurrent-run slot for it (the
/// API's <c>actor-memory-limit-exceeded</c>/<c>concurrent-runs-limit-exceeded</c> errors, which clear as
/// other runs or builds finish). Passed to <see cref="ActorStartOptions.WaitForResources"/> and
/// <see cref="TaskStartOptions.WaitForResources"/>; matches the reference client's <c>waitForResources</c>
/// start option (there a <c>boolean | number</c> union, modelled here as this small value type so the
/// "retry forever" and "retry for N seconds" cases stay distinct and self-documenting).
/// </summary>
public readonly struct WaitForResources : IEquatable<WaitForResources>
{
    private WaitForResources(double? maxWaitSecs)
    {
        MaxWaitSecs = maxWaitSecs;
    }

    /// <summary>
    /// The maximum time to keep retrying, in seconds, or <c>null</c> when retrying until the run starts
    /// (see <see cref="UntilAvailable"/>).
    /// </summary>
    public double? MaxWaitSecs { get; }

    /// <summary>Retries indefinitely until the run starts.</summary>
    public static WaitForResources UntilAvailable { get; } = new(null);

    /// <summary>
    /// Retries for up to <paramref name="maxWaitSecs"/> seconds, after which the last
    /// <c>actor-memory-limit-exceeded</c>/<c>concurrent-runs-limit-exceeded</c> error is thrown.
    /// </summary>
    /// <param name="maxWaitSecs">The retry budget in seconds; must not be negative.</param>
    public static WaitForResources For(double maxWaitSecs)
    {
        if (maxWaitSecs < 0 || double.IsNaN(maxWaitSecs))
        {
            throw new ArgumentOutOfRangeException(nameof(maxWaitSecs), maxWaitSecs, "must not be negative");
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
