namespace Apify.Client.Models;

/// <summary>
/// A single record retrieved from a key-value store.
/// </summary>
/// <remarks>
/// <see cref="Value"/> holds the record's <b>raw bytes</b> exactly as stored, so binary records (images,
/// gzip, protobuf, XLSX, …) survive a round-trip intact. Decode it according to <see cref="ContentType"/>:
/// for text use <c>System.Text.Encoding.UTF8.GetString(record.Value)</c>, and for JSON
/// (e.g. records written with <see cref="Apify.Client.Resources.KeyValueStoreClient.SetRecordJsonAsync"/>)
/// deserialize the bytes with <c>System.Text.Json.JsonSerializer.Deserialize&lt;T&gt;(record.Value)</c>.
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

    /// <summary>
    /// The raw record bytes, exactly as stored. Decode according to <see cref="ContentType"/> (see the
    /// class remarks for text/JSON decoding).
    /// </summary>
    public byte[] Value { get; }

    /// <summary>The record's MIME type, as reported by the API.</summary>
    public string? ContentType { get; }
}
