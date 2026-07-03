using System;

namespace Apify.Client.Options;

/// <summary>An output format for downloading dataset items.</summary>
public enum DownloadItemsFormat
{
    /// <summary>JSON array.</summary>
    Json,

    /// <summary>Newline-delimited JSON.</summary>
    Jsonl,

    /// <summary>Comma-separated values.</summary>
    Csv,

    /// <summary>Microsoft Excel (XLSX) workbook.</summary>
    Xlsx,

    /// <summary>XML.</summary>
    Xml,

    /// <summary>RSS feed.</summary>
    Rss,

    /// <summary>HTML table.</summary>
    Html,
}

/// <summary>Maps <see cref="DownloadItemsFormat"/> values to their API wire representation.</summary>
internal static class DownloadItemsFormatExtensions
{
    /// <summary>The lowercase wire value the API expects for the <c>format</c> query parameter.</summary>
    public static string ToWireValue(this DownloadItemsFormat format) => format switch
    {
        DownloadItemsFormat.Json => "json",
        DownloadItemsFormat.Jsonl => "jsonl",
        DownloadItemsFormat.Csv => "csv",
        DownloadItemsFormat.Xlsx => "xlsx",
        DownloadItemsFormat.Xml => "xml",
        DownloadItemsFormat.Rss => "rss",
        DownloadItemsFormat.Html => "html",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "unknown download format"),
    };
}
