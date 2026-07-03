using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Apify.Client.Internal;

/// <summary>
/// An ordered collection of query parameters that omits absent (<c>null</c>) values and encodes
/// booleans as <c>1</c>/<c>0</c>, matching the Apify API conventions.
/// </summary>
internal sealed class QueryParams
{
    private readonly List<KeyValuePair<string, string>> _pairs = new();

    /// <summary>Adds a string parameter if the value is non-null.</summary>
    public QueryParams AddString(string key, string? value)
    {
        if (value is not null)
        {
            _pairs.Add(new KeyValuePair<string, string>(key, value));
        }

        return this;
    }

    /// <summary>Adds an integer parameter if the value is non-null.</summary>
    public QueryParams AddInt(string key, long? value)
    {
        if (value is not null)
        {
            _pairs.Add(new KeyValuePair<string, string>(key, value.Value.ToString(CultureInfo.InvariantCulture)));
        }

        return this;
    }

    /// <summary>Adds a floating-point parameter if the value is non-null.</summary>
    public QueryParams AddDouble(string key, double? value)
    {
        if (value is not null)
        {
            // Locale-independent representation without a trailing ".0" for whole numbers.
            var text = value.Value.ToString("0.##########", CultureInfo.InvariantCulture);
            _pairs.Add(new KeyValuePair<string, string>(key, text));
        }

        return this;
    }

    /// <summary>
    /// Adds a boolean parameter, encoded as <c>1</c>/<c>0</c>, if the value is non-null. This matches the
    /// JS reference client, whose axios <c>paramsSerializer</c> converts booleans via <c>Number(value)</c>.
    /// </summary>
    public QueryParams AddBool(string key, bool? value)
    {
        if (value is not null)
        {
            _pairs.Add(new KeyValuePair<string, string>(key, value.Value ? "1" : "0"));
        }

        return this;
    }

    /// <summary>Adds a comma-joined list parameter if the list is non-null and non-empty.</summary>
    public QueryParams AddCsv(string key, IReadOnlyList<string>? values)
    {
        if (values is { Count: > 0 })
        {
            _pairs.Add(new KeyValuePair<string, string>(key, string.Join(",", values)));
        }

        return this;
    }

    /// <summary>Appends an already-stringified key/value pair unconditionally.</summary>
    public QueryParams AddRaw(string key, string value)
    {
        _pairs.Add(new KeyValuePair<string, string>(key, value));
        return this;
    }

    /// <summary>Whether no parameters have been added.</summary>
    public bool IsEmpty => _pairs.Count == 0;

    /// <summary>Returns a shallow copy of this instance.</summary>
    public QueryParams Copy()
    {
        var copy = new QueryParams();
        copy._pairs.AddRange(_pairs);
        return copy;
    }

    /// <summary>Appends all pairs from <paramref name="other"/> to this instance.</summary>
    public QueryParams Extend(QueryParams? other)
    {
        if (other is not null)
        {
            _pairs.AddRange(other._pairs);
        }

        return this;
    }

    /// <summary>Appends the parameters to <paramref name="rawUrl"/> as a URL-encoded query string.</summary>
    public string ApplyToUrl(string rawUrl)
    {
        if (_pairs.Count == 0)
        {
            return rawUrl;
        }

        var builder = new StringBuilder(rawUrl);
        builder.Append(rawUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?');
        for (var i = 0; i < _pairs.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('&');
            }

            builder.Append(Uri.EscapeDataString(_pairs[i].Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(_pairs[i].Value));
        }

        return builder.ToString();
    }
}
