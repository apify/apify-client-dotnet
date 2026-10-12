using System;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Options;

namespace Apify.Client.Internal;

/// <summary>
/// Implements <see cref="ActorStartOptions.WaitForResources"/>/<see cref="TaskStartOptions.WaitForResources"/>:
/// retries a run start while the API rejects it for lack of resources. Matches the reference client's
/// <c>startWaitingForResources()</c>.
/// </summary>
internal static class ResourceLimitRetry
{
    /// <summary>
    /// Error types the API rejects a run start with while the account has no free memory or concurrent-run
    /// slot for it. Both clear as other runs or builds finish.
    /// </summary>
    private const string MemoryLimitErrorType = "actor-memory-limit-exceeded";

    private const string ConcurrentRunsLimitErrorType = "concurrent-runs-limit-exceeded";

    /// <summary>Cooldown between two attempts to start a run that was rejected for lack of resources.</summary>
    private const int CooldownMillis = 10_000;

    /// <summary>
    /// Calls <paramref name="start"/>, retrying it every <paramref name="cooldownMillis"/> while it fails
    /// with a resource-limit error. <paramref name="waitForResources"/> <c>null</c> disables retrying
    /// entirely (the first error is thrown as-is); otherwise <see cref="WaitForResources.MaxWaitSecs"/>
    /// <c>null</c> retries until the call succeeds, and a value bounds the retrying, after which the last
    /// error is thrown. Any other error is thrown right away.
    /// </summary>
    /// <param name="start">Makes one start attempt.</param>
    /// <param name="waitForResources">The retry configuration, or <c>null</c> to disable retrying.</param>
    /// <param name="cancellationToken">A token to cancel the wait between attempts.</param>
    /// <param name="cooldownMillis">
    /// The cooldown between attempts; defaults to <see cref="CooldownMillis"/>. Overridable only so unit
    /// tests can exercise multi-attempt retries without a real 10-second wait per attempt.
    /// </param>
    public static async Task<T> StartAsync<T>(
        Func<Task<T>> start,
        WaitForResources? waitForResources,
        CancellationToken cancellationToken,
        int cooldownMillis = CooldownMillis)
    {
        if (waitForResources is null)
        {
            return await start().ConfigureAwait(false);
        }

        var budgetMillis = waitForResources.Value.MaxWaitSecs is { } secs ? (long)(secs * 1000) : (long?)null;
        var stopwatch = budgetMillis is not null ? System.Diagnostics.Stopwatch.StartNew() : null;

        while (true)
        {
            try
            {
                return await start().ConfigureAwait(false);
            }
            catch (ApifyApiException e) when (IsResourceLimitError(e))
            {
                if (budgetMillis is not null)
                {
                    var remainingMillis = budgetMillis.Value - stopwatch!.ElapsedMilliseconds;
                    if (remainingMillis <= 0)
                    {
                        throw;
                    }

                    await Task.Delay((int)Math.Min(cooldownMillis, remainingMillis), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(cooldownMillis, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static bool IsResourceLimitError(ApifyApiException e) =>
        e.Type is MemoryLimitErrorType or ConcurrentRunsLimitErrorType;
}
