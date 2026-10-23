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
    /// The smaller of two page limits, for the <c>limit</c> query parameter. The API reads a <c>limit</c> of
    /// <c>0</c> as unset, so <c>0</c> (like <c>null</c>) means "no limit" here and loses to any other value;
    /// the result is <c>null</c> when neither is set. Matches the reference client's <c>minForLimitParam</c>.
    /// </summary>
    public static int? MinForLimit(int? a, int? b)
    {
        a = a is 0 ? null : a;
        b = b is 0 ? null : b;
        if (a is null)
        {
            return b;
        }

        return b is null ? a : Math.Min(a.Value, b.Value);
    }
}
