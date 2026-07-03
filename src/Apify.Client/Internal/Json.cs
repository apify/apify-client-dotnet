using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Apify.Client.Internal;

/// <summary>
/// Shared JSON (de)serialization for the client.
/// </summary>
internal static class Json
{
    /// <summary>
    /// Serialization options matching the reference client's output: slashes and non-ASCII characters
    /// are left unescaped (like PHP's <c>JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE</c>).
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Serializes a value to a JSON string.</summary>
    public static string Encode(object? value) => JsonSerializer.Serialize(value, SerializerOptions);

    /// <summary>Serializes a <see cref="JsonNode"/> to a JSON string.</summary>
    public static string Encode(JsonNode? value) =>
        value?.ToJsonString(SerializerOptions) ?? "null";

    /// <summary>Decodes a JSON string into a <see cref="JsonNode"/> (<c>null</c> for an empty body).</summary>
    public static JsonNode? Decode(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        return JsonNode.Parse(body);
    }

    /// <summary>
    /// Decodes a JSON response body wrapped in a <c>{"data": ...}</c> envelope, returning the unwrapped
    /// <c>data</c> value (or <c>null</c> if it is absent/null).
    /// </summary>
    public static JsonNode? DecodeData(string body)
    {
        if (Decode(body) is JsonObject obj && obj.TryGetPropertyValue("data", out var data))
        {
            return data;
        }

        return null;
    }

    /// <summary>Attempts to decode a body, returning <c>null</c> on any parse error.</summary>
    public static JsonNode? TryDecode(string body)
    {
        try
        {
            return Decode(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
