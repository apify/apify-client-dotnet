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

    /// <summary>Serializes a value to a <see cref="JsonNode"/> (<c>null</c> for a <c>null</c> value).</summary>
    public static JsonNode? ToNode(object? value) => JsonSerializer.SerializeToNode(value, SerializerOptions);

    /// <summary>Serializes a <see cref="JsonNode"/> to a JSON string.</summary>
    public static string Encode(JsonNode? value) =>
        value?.ToJsonString(SerializerOptions) ?? "null";

    /// <summary>
    /// Decodes a JSON string into a <see cref="JsonNode"/> (<c>null</c> for an empty body). Throws
    /// <see cref="JsonException"/> when it is not valid JSON; response bodies go through
    /// <see cref="ResponseReader"/>, which turns that into an <see cref="Apify.Client.Exceptions.InvalidResponseBodyException"/>.
    /// </summary>
    public static JsonNode? Decode(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        return JsonNode.Parse(body);
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
