using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>
/// Base class for API resource models.
/// </summary>
/// <remarks>
/// Each model wraps the raw decoded JSON object and exposes commonly-used fields as typed properties.
/// The full payload — including any field the API adds that is not modelled here — is always available
/// via <see cref="ToJsonObject"/> and <see cref="Get"/>, so additive API changes never lose data.
/// </remarks>
public abstract class ApifyResource
{
    private readonly JsonObject _data;

    /// <summary>Wraps the raw decoded resource object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    protected ApifyResource(JsonObject data)
    {
        _data = data;
    }

    /// <summary>The full raw resource object, including fields not mapped to a typed property.</summary>
    public JsonObject ToJsonObject() => _data;

    /// <summary>A single raw field by key (<c>null</c> if absent).</summary>
    /// <param name="key">The field name.</param>
    public JsonNode? Get(string key) => _data.TryGetPropertyValue(key, out var value) ? value : null;

    /// <summary>Reads a string field, coercing numbers to their text form; <c>null</c> if absent or unsupported.</summary>
    protected string? GetString(string key)
    {
        var node = Get(key);
        return node?.GetValueKind() switch
        {
            JsonValueKind.String => node.GetValue<string>(),
            JsonValueKind.Number => node.ToJsonString(),
            _ => null,
        };
    }

    /// <summary>Reads an integer field, coercing numeric strings and fractional numbers; <c>null</c> if absent.</summary>
    protected long? GetInt(string key)
    {
        var node = Get(key);
        if (node is null)
        {
            return null;
        }

        var text = node.GetValueKind() switch
        {
            JsonValueKind.Number => node.ToJsonString(),
            JsonValueKind.String => node.GetValue<string>(),
            _ => null,
        };
        if (text is null)
        {
            return null;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return longValue;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return (long)doubleValue;
        }

        return null;
    }

    /// <summary>Reads a boolean field; <c>null</c> if absent or not a JSON boolean.</summary>
    protected bool? GetBool(string key)
    {
        return Get(key)?.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    /// <summary>Reads a string-array field (numbers coerced to text); <c>null</c> if absent or not an array.</summary>
    protected IReadOnlyList<string>? GetStringList(string key)
    {
        if (Get(key) is not JsonArray array)
        {
            return null;
        }

        var result = new List<string>(array.Count);
        foreach (var item in array)
        {
            var text = item?.GetValueKind() switch
            {
                JsonValueKind.String => item.GetValue<string>(),
                JsonValueKind.Number => item.ToJsonString(),
                _ => null,
            };
            if (text is not null)
            {
                result.Add(text);
            }
        }

        return result;
    }
}
