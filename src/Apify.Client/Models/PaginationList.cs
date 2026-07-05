using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Apify.Client.Internal;

namespace Apify.Client.Models;

/// <summary>
/// A single page of an offset/limit-paginated list.
/// </summary>
/// <remarks>
/// The pagination metadata (<see cref="Total"/>, <see cref="Offset"/>, <see cref="Limit"/>,
/// <see cref="Count"/>, <see cref="Desc"/>) accompanies the <see cref="Items"/>. <see cref="Count"/> is
/// always the number of items in <em>this</em> page (so <c>this[i]</c> is valid for
/// <c>0 &lt;= i &lt; Count</c>); the API's total across all pages is exposed separately as
/// <see cref="Total"/>. Note: <see cref="Total"/> reflects the API's reported total, which can briefly lag
/// immediately after a write (the count is computed asynchronously) — re-read after a short delay if you
/// need an exact post-write total.
/// </remarks>
/// <typeparam name="T">The hydrated item type.</typeparam>
public sealed class PaginationList<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _items;

    private PaginationList(IReadOnlyList<T> items, long total, long offset, long limit, bool desc)
    {
        _items = items;
        Total = total;
        Offset = offset;
        Limit = limit;
        Desc = desc;
    }

    /// <summary>Builds a page from a decoded paginated object, hydrating each item.</summary>
    /// <param name="data">The decoded paginated object.</param>
    /// <param name="hydrate">Maps each raw item to a model.</param>
    internal static PaginationList<T> FromData(JsonNode? data, Func<JsonObject, T> hydrate)
    {
        var obj = JsonValues.AsObject(data);
        var rawItems = JsonValues.ObjectItems(obj);
        var items = new List<T>(rawItems.Count);
        foreach (var raw in rawItems)
        {
            items.Add(hydrate(raw));
        }

        return new PaginationList<T>(
            items,
            JsonValues.IntOr(obj, "total", items.Count),
            JsonValues.IntOr(obj, "offset", 0),
            JsonValues.IntOr(obj, "limit", items.Count),
            JsonValues.BoolOr(obj, "desc", false));
    }

    /// <summary>
    /// Builds a page directly from items and metadata (used by the dataset-items endpoint, which returns
    /// a bare array with pagination in response headers).
    /// </summary>
    /// <param name="items">The page items.</param>
    /// <param name="total">Total number of items available across all pages.</param>
    /// <param name="offset">Number of items skipped at the start.</param>
    /// <param name="limit">Maximum number of items the API would return.</param>
    /// <param name="desc">Whether the items are in descending order.</param>
    internal static PaginationList<T> FromItems(IReadOnlyList<T> items, long total, long offset, long limit, bool desc)
        => new(items, total, offset, limit, desc);

    /// <summary>The items of this page (never <c>null</c>).</summary>
    public IReadOnlyList<T> Items => _items;

    /// <summary>Total number of items available across all pages.</summary>
    public long Total { get; }

    /// <summary>Number of items skipped at the start.</summary>
    public long Offset { get; }

    /// <summary>Maximum number of items the API would return for this request.</summary>
    public long Limit { get; }

    /// <summary>
    /// Number of items in this page (always equal to <c>Items.Count</c>). Use <see cref="Total"/> for the
    /// count across all pages.
    /// </summary>
    public long Count => _items.Count;

    /// <summary>Whether the items are in descending order.</summary>
    public bool Desc { get; }

    /// <inheritdoc />
    int IReadOnlyCollection<T>.Count => _items.Count;

    /// <inheritdoc />
    public T this[int index] => _items[index];

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
