using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Apify.Client.Internal;

namespace Apify.Client.Models;

/// <summary>A page of an offset/limit-paginated list.</summary>
/// <remarks>
/// The page metadata (<see cref="Total"/>, <see cref="Offset"/>, <see cref="Limit"/>, <see cref="Count"/>,
/// <see cref="Desc"/>) accompanies the <see cref="Items"/>. <see cref="Count"/> is the number of entries the API went
/// through for this page, which can differ from the number of items, so use <c>Items.Count</c> for the items. The
/// list's own <c>Count</c> (<see cref="IReadOnlyCollection{T}"/>, LINQ's <c>Count()</c>), enumeration, and indexer
/// cover the items only. <see cref="Total"/> can lag briefly after a write, since the API computes it asynchronously.
/// </remarks>
/// <typeparam name="T">The hydrated item type.</typeparam>
public sealed class PaginationList<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _items;

    private PaginationList(IReadOnlyList<T> items, long count, long total, long offset, long limit, bool desc)
    {
        _items = items;
        Count = count;
        Total = total;
        Offset = offset;
        Limit = limit;
        Desc = desc;
    }

    /// <summary>Builds a page from a decoded paginated object, hydrating each item.</summary>
    /// <param name="data">The decoded paginated object; its <c>count</c> becomes <see cref="Count"/>.</param>
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
            JsonValues.IntOr(obj, "count", items.Count),
            JsonValues.IntOr(obj, "total", items.Count),
            JsonValues.IntOr(obj, "offset", 0),
            JsonValues.IntOr(obj, "limit", items.Count),
            JsonValues.BoolOr(obj, "desc", false));
    }

    /// <summary>Builds a page from items and metadata, for the dataset-items endpoint, which returns a bare array.</summary>
    /// <param name="items">The page items.</param>
    /// <param name="count">The rows the API scanned for the page, or the item count when that is larger.</param>
    /// <param name="total">Total number of items available across all pages.</param>
    /// <param name="offset">Number of items skipped at the start.</param>
    /// <param name="limit">Maximum number of items the API would return.</param>
    /// <param name="desc">Whether the items are in descending order.</param>
    internal static PaginationList<T> FromItems(IReadOnlyList<T> items, long count, long total, long offset, long limit, bool desc)
        => new(items, count, total, offset, limit, desc);

    /// <summary>The items of this page.</summary>
    public IReadOnlyList<T> Items => _items;

    /// <summary>Total number of items available across all pages.</summary>
    public long Total { get; }

    /// <summary>Number of items skipped at the start.</summary>
    public long Offset { get; }

    /// <summary>Maximum number of items the API would return for this request.</summary>
    public long Limit { get; }

    /// <summary>
    /// The number of entries on this page. For <see cref="Resources.DatasetClient.ListItemsAsync"/> it is the number of
    /// dataset rows the API scanned, or the number of items if that is larger, so filters such as
    /// <see cref="Options.DatasetListItemsOptions.SkipEmpty"/> or <see cref="Options.DatasetListItemsOptions.Clean"/> can
    /// leave it above <c>Items.Count</c>. The next page starts at <c>Offset + Math.Min(Count, Limit)</c>; the cap matters
    /// with <see cref="Options.DatasetListItemsOptions.Unwind"/>, which can return more items than rows. For other pages it
    /// is the <c>count</c> the API reports, which equals the items returned unless the API drops entries after counting
    /// them, as the Store listing does. <see cref="Total"/> is the count across all pages.
    /// </summary>
    public long Count { get; }

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
