using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Apify.Client.Internal;

/// <summary>One fetched page: its items and, when the API reports it, the number of rows it scanned.</summary>
/// <param name="Items">The items on the page.</param>
/// <param name="ScannedRows">The rows the API scanned to produce the page (<c>null</c> when not reported).</param>
/// <typeparam name="T">The item type.</typeparam>
internal readonly record struct FetchedPage<T>(IReadOnlyList<T> Items, long? ScannedRows);

/// <summary>
/// The offset/limit paging loop shared by every list iterator, ported from the reference client's
/// <c>listPaginatedFromCallback</c>.
/// </summary>
internal static class PagedIteration
{
    /// <summary>
    /// Lazily yields every item from <paramref name="startOffset"/> on, fetching pages on demand.
    /// </summary>
    /// <remarks>
    /// <paramref name="totalLimit"/> caps the number of scanned rows (<c>null</c> or <c>0</c> for no cap).
    /// The next offset and the stop condition follow the rows a page scanned, not the items it returned:
    /// advancing by the item count would re-scan rows after a filter dropped some and skip rows after
    /// <c>unwind</c> multiplied them, and stopping at an empty page would end the iteration in front of rows
    /// a filter hid. The scanned count is derived from a dataset's item count, which lags a fresh push, so
    /// the larger of it and the item count is used, capped at the rows the request asked for so an unwound
    /// page does not advance past rows the next request would then never read. The total a page reports is
    /// never consulted, because it changes when the listed resource grows during the iteration; iteration
    /// stops once a page scans no rows, which costs one extra request at the end.
    /// </remarks>
    /// <param name="startOffset">The offset of the first row.</param>
    /// <param name="totalLimit">The most rows to scan across all pages, or <c>null</c>/<c>0</c> for no cap.</param>
    /// <param name="chunkSize">The most rows to request per call, or <c>null</c> for the API default.</param>
    /// <param name="fetchPage">Fetches one page given its offset and limit (<c>null</c> limit: leave it out).</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    /// <typeparam name="T">The item type.</typeparam>
    public static async IAsyncEnumerable<T> IterateAsync<T>(
        int startOffset,
        int? totalLimit,
        int? chunkSize,
        Func<int, int?, CancellationToken, Task<FetchedPage<T>>> fetchPage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        PageSizing.ValidateChunkSize(chunkSize, "ChunkSize");
        var offset = Math.Max(startOffset, 0);
        int? remaining = totalLimit is > 0 ? totalLimit : null;

        long scanned;
        do
        {
            var requestedLimit = PageSizing.MinForLimit(remaining, chunkSize);
            var page = await fetchPage(offset, requestedLimit, cancellationToken).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                yield return item;
            }

            scanned = ScannedRows(page, requestedLimit);
            offset += (int)scanned;
            remaining -= (int?)scanned;
        }
        while (scanned > 0 && (remaining is null || remaining > 0));
    }

    private static long ScannedRows<T>(FetchedPage<T> page, int? requestedLimit)
    {
        var scanned = Math.Max(page.ScannedRows ?? 0, page.Items.Count);
        return requestedLimit is > 0 ? Math.Min(scanned, requestedLimit.Value) : scanned;
    }
}
