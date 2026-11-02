using System.Collections.Generic;
using System.Threading;

namespace Apify.Client;

/// <summary>
/// Counters of what an <see cref="ApifyClient"/> has sent so far, matching the reference client's
/// <c>stats</c>. All members are safe to read while requests are in flight.
/// </summary>
public sealed class Statistics
{
    private readonly object _lock = new();
    private readonly List<long> _rateLimitErrors = new();
    private long _calls;
    private long _requests;

    /// <summary>The number of client calls made (each call may send several requests when it is retried).</summary>
    public long Calls => Interlocked.Read(ref _calls);

    /// <summary>The number of HTTP requests sent, counting every retry attempt.</summary>
    public long Requests => Interlocked.Read(ref _requests);

    /// <summary>
    /// The number of times the API answered <c>429</c> (rate limit exceeded). Errors on the first attempt are
    /// counted at index 0, errors on the first retry at index 1, and so on. A snapshot copy.
    /// </summary>
    public IReadOnlyList<long> RateLimitErrors
    {
        get
        {
            lock (_lock)
            {
                return _rateLimitErrors.ToArray();
            }
        }
    }

    internal void AddCall() => Interlocked.Increment(ref _calls);

    internal void AddRequest() => Interlocked.Increment(ref _requests);

    internal void AddRateLimitError(int attempt)
    {
        var index = attempt - 1;
        lock (_lock)
        {
            while (_rateLimitErrors.Count <= index)
            {
                _rateLimitErrors.Add(0);
            }

            _rateLimitErrors[index]++;
        }
    }
}
