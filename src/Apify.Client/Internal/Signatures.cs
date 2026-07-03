using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Apify.Client.Internal;

/// <summary>
/// Apify storage-content URL signing, byte-for-byte compatible with the platform's
/// <c>@apify/utilities</c> implementation that the reference clients rely on.
/// </summary>
internal static class Signatures
{
    /// <summary>Version tag embedded in storage-content signatures (upstream default).</summary>
    private const string StorageContentSignatureVersion = "0";

    /// <summary>Number of leading hex characters of the HMAC digest used.</summary>
    private const int HmacSignatureHexLen = 30;

    /// <summary>Base62 alphabet (digits, then lowercase, then uppercase), matching upstream.</summary>
    private const string Base62Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private const int Base = 62;
    private const int ByteBase = 256;

    /// <summary>
    /// Computes an Apify URL-signing signature, byte-for-byte compatible with upstream
    /// <c>createHmacSignature</c>: HMAC-SHA256(secret, message) as lowercase hex, take the first 30 hex
    /// characters, interpret them as a big integer, then base62-encode (alphabet <c>0-9a-zA-Z</c>).
    /// </summary>
    public static string CreateHmacSignature(string secretKey, string message)
    {
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(message));
        var hex = Convert.ToHexString(digest).ToLowerInvariant();
        var truncated = hex.Substring(0, HmacSignatureHexLen);
        return HexToBase62(truncated);
    }

    /// <summary>
    /// Builds a storage-content signature for a resource's public URL, byte-for-byte compatible with
    /// upstream <c>createStorageContentSignature</c>.
    /// </summary>
    /// <remarks>
    /// It signs the message <c>"{version}.{expiresAtMillis}.{resourceId}"</c> (<c>expiresAtMillis</c> is
    /// the absolute expiry in ms, or <c>0</c> for a non-expiring URL) with <see cref="CreateHmacSignature"/>,
    /// then returns the base64url (no padding) encoding of <c>"{version}.{expiresAtMillis}.{hmac}"</c>.
    /// </remarks>
    /// <param name="secretKey">The store/dataset URL-signing secret key.</param>
    /// <param name="resourceId">The resource id being signed.</param>
    /// <param name="expiresInSecs">Optional expiry in seconds (<c>null</c> for a non-expiring URL).</param>
    public static string SignStorageContent(string secretKey, string resourceId, int? expiresInSecs)
    {
        var expiresAtMillis = expiresInSecs is not null
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresInSecs.Value * 1000L)
            : 0L;
        var version = StorageContentSignatureVersion;
        var expiryText = expiresAtMillis.ToString(CultureInfo.InvariantCulture);
        var message = version + "." + expiryText + "." + resourceId;
        var hmac = CreateHmacSignature(secretKey, message);
        var envelope = version + "." + expiryText + "." + hmac;
        return Base64UrlNoPadding(Encoding.UTF8.GetBytes(envelope));
    }

    /// <summary>
    /// Interprets a hex string as a big-endian non-negative integer and encodes it in base62.
    /// Implemented with byte-wise long division (base 256 → base 62) so it needs no bignum dependency.
    /// </summary>
    private static string HexToBase62(string hex)
    {
        var digits = new List<int>(Convert.FromHexString(hex).Length);
        foreach (var b in Convert.FromHexString(hex))
        {
            digits.Add(b);
        }

        if (digits.Count == 0)
        {
            return "0";
        }

        var result = new StringBuilder();
        while (digits.Count > 0)
        {
            var remainder = 0;
            var quotient = new List<int>(digits.Count);
            foreach (var value in digits)
            {
                var accumulator = (remainder * ByteBase) + value;
                var q = accumulator / Base;
                remainder = accumulator % Base;
                if (quotient.Count > 0 || q != 0)
                {
                    quotient.Add(q);
                }
            }

            result.Insert(0, Base62Alphabet[remainder]);
            digits = quotient;
        }

        return result.Length == 0 ? "0" : result.ToString();
    }

    /// <summary>Encodes bytes as base64url without padding (<c>+/</c> → <c>-_</c>, trailing <c>=</c> removed).</summary>
    private static string Base64UrlNoPadding(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
