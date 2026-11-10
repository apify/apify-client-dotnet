namespace Apify.Client.Models;

/// <summary>A record from a key-value store.</summary>
/// <remarks>
/// <see cref="Value"/> holds the raw bytes exactly as stored, so binary records survive intact. Decode it by
/// <see cref="ContentType"/>: text with <c>System.Text.Encoding.UTF8.GetString(record.Value)</c>, JSON with
/// <c>System.Text.Json.JsonSerializer.Deserialize&lt;T&gt;(record.Value)</c>.
/// </remarks>
public sealed class KeyValueStoreRecord
{
    /// <summary>Creates a record.</summary>
    /// <param name="key">The record key.</param>
    /// <param name="value">The raw record bytes.</param>
    /// <param name="contentType">The record's MIME type, as reported by the API.</param>
    public KeyValueStoreRecord(string key, byte[] value, string? contentType)
    {
        Key = key;
        Value = value;
        ContentType = contentType;
    }

    /// <summary>The record key.</summary>
    public string Key { get; }

    /// <summary>The raw record bytes, exactly as stored.</summary>
    public byte[] Value { get; }

    /// <summary>The record's MIME type, as reported by the API.</summary>
    public string? ContentType { get; }
}
