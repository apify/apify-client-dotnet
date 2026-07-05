using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Apify.Client.Internal;

/// <summary>
/// Small helpers for reading typed values out of a decoded <see cref="JsonObject"/> with fallbacks,
/// used by the page/head models. Absent or mistyped fields fall back rather than throwing.
/// </summary>
internal static class JsonValues
{
    /// <summary>The object as a <see cref="JsonObject"/>, or an empty object if it is not one.</summary>
    public static JsonObject AsObject(JsonNode? node) => node as JsonObject ?? new JsonObject();

    /// <summary>Reads a string field, or <c>null</c> if absent/not a string.</summary>
    public static string? String(JsonObject obj, string key)
    {
        return obj.TryGetPropertyValue(key, out var node) && node?.GetValueKind() == JsonValueKind.String
            ? node.GetValue<string>()
            : null;
    }

    /// <summary>Reads an integer field, or <paramref name="fallback"/> if absent/not numeric.</summary>
    public static long IntOr(JsonObject obj, string key, long fallback)
    {
        if (obj.TryGetPropertyValue(key, out var node) && node?.GetValueKind() == JsonValueKind.Number
            && long.TryParse(node.ToJsonString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return fallback;
    }

    /// <summary>Reads a boolean field, or <paramref name="fallback"/> if absent/not a boolean.</summary>
    public static bool BoolOr(JsonObject obj, string key, bool fallback)
    {
        return obj.TryGetPropertyValue(key, out var node)
            ? node?.GetValueKind() switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => fallback,
            }
            : fallback;
    }

    /// <summary>Returns the <c>items</c> array of a decoded object as a list of <see cref="JsonObject"/>.</summary>
    public static IReadOnlyList<JsonObject> ObjectItems(JsonObject obj)
    {
        var result = new List<JsonObject>();
        if (obj.TryGetPropertyValue("items", out var node) && node is JsonArray array)
        {
            foreach (var item in array)
            {
                result.Add(item as JsonObject ?? new JsonObject());
            }
        }

        return result;
    }
}
