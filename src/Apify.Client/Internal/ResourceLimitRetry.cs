using System;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Options;

namespace Apify.Client.Internal;

/// <summary>
/// Implements <see cref="ActorStartOptions.WaitForResources"/> and <see cref="TaskStartOptions.WaitForResources"/>:
/// retries a run start while the API rejects it for lack of resources, as the reference client's
/// <c>startWaitingForResources()</c>.
/// </summary>
internal static class ResourceLimitRetry
{
    /// <summary>Error types of a start rejected because no memory or concurrent-run slot is free; both clear as other runs finish.</summary>
    private const string MemoryLimitErrorType = "actor-memory-limit-exceeded";

    private const string ConcurrentRunsLimitErrorType = "concurrent-runs-limit-exceeded";

    /// <summary>Cooldown between attempts.</summary>
    private const int CooldownMillis = 10_000;

    /// <summary>
    /// Calls <paramref name="start"/> and retries it every <paramref name="cooldownMillis"/> while it fails with a
    /// resource-limit error. A <c>null</c> <paramref name="waitForResources"/> throws the first error;
    /// <see cref="WaitForResources.MaxWaitSecs"/> <c>null</c> retries until success, and a value bounds the retrying before
    /// the last error is thrown. Other errors are thrown at once.
    /// </summary>
    /// <param name="start">Makes one start attempt.</param>
    /// <param name="waitForResources">The retry configuration, or <c>null</c> to disable retrying.</param>
    /// <param name="cancellationToken">A token to cancel the wait between attempts.</param>
    /// <param name="cooldownMillis">The cooldown between attempts; a parameter so unit tests need not wait 10 seconds.</param>
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
