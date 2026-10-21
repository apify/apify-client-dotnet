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
    /// Sets the <c>limit</c> query parameter of the next page: the smaller of the chunk size and the number
    /// of items still wanted under the caller's total limit. A total limit of <c>0</c> means unset (the API
    /// itself reads <c>limit=0</c> as unbounded), so it loses to the chunk size, and when neither is set the
    /// query is left as the caller built it.
    /// </summary>
    /// <param name="query">The page query, which already carries the caller's own <c>limit</c> if any.</param>
    /// <param name="chunkSize">The validated chunk size, or <c>null</c> when unset.</param>
    /// <param name="totalLimit">The caller's total limit across all pages, or <c>null</c> when unbounded.</param>
    /// <param name="yielded">The number of items already yielded.</param>
    public static void SetPageLimit(QueryParams query, int? chunkSize, int? totalLimit, int yielded)
    {
        var remaining = totalLimit is null or 0 ? null : (int?)Math.Max(totalLimit.Value - yielded, 0);
        var pageLimit = chunkSize is null ? remaining : (remaining is null ? chunkSize : Math.Min(chunkSize.Value, remaining.Value));
        if (pageLimit is not null)
        {
            query.Set("limit", pageLimit.Value);
        }
    }
}
