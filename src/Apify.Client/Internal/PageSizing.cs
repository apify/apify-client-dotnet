using System;

namespace Apify.Client.Internal;

/// <summary>
/// Shared handling of the client-side <c>ChunkSize</c> iteration option: the maximum number of items
/// requested per API call while iterating across pages. It never reaches the query string itself; it only
/// sizes each page's <c>limit</c>, matching the reference client's <c>chunkSize</c>.
/// </summary>
internal static class PageSizing
{
    /// <summary>Rejects a zero or negative chunk size, which the reference client's options schema also refuses.</summary>
    /// <param name="chunkSize">The chunk size from the caller's options, or <c>null</c> when unset.</param>
    /// <param name="optionName">The option's public name, used in the error message.</param>
    public static void ValidateChunkSize(int? chunkSize, string optionName)
    {
        if (chunkSize is <= 0)
        {
            throw new ArgumentOutOfRangeException(optionName, chunkSize, optionName + " must be positive.");
        }
    }

    /// <summary>
    /// The <c>limit</c> to request for the next page: the smaller of the chunk size and the number of items
    /// still wanted. A value of <c>null</c> or <c>0</c> means unset (the API itself reads <c>limit=0</c> as
    /// unbounded), so it loses to any other value; <c>null</c> is returned when both are unset.
    /// </summary>
    /// <param name="chunkSize">The validated chunk size, or <c>null</c> when unset.</param>
    /// <param name="remaining">The number of items still wanted under the caller's total limit, or <c>null</c> when unbounded.</param>
    public static int? PageLimit(int? chunkSize, int? remaining)
    {
        var chunk = chunkSize is 0 ? null : chunkSize;
        var wanted = remaining is 0 ? null : remaining;
        if (chunk is null)
        {
            return wanted;
        }

        return wanted is null ? chunk : Math.Min(chunk.Value, wanted.Value);
    }
}
