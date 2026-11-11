using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Apify.Client.Internal;

/// <summary>Signing of storage-content URLs, byte-for-byte compatible with <c>@apify/utilities</c>.</summary>
internal static class Signatures
{
    /// <summary>Version tag in the signatures.</summary>
    private const string StorageContentSignatureVersion = "0";

    /// <summary>Number of leading hex characters of the HMAC digest that are used.</summary>
    private const int HmacSignatureHexLen = 30;

    /// <summary>Base62 alphabet: digits, lowercase, uppercase.</summary>
    private const string Base62Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private const int Base = 62;
    private const int ByteBase = 256;

    /// <summary>
    /// Computes the signature as <c>createHmacSignature</c> upstream does: HMAC-SHA256 as lowercase hex, the first 30 hex
    /// characters read as a big integer and base62-encoded.
    /// </summary>
    public static string CreateHmacSignature(string secretKey, string message)
    {
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(message));
        var hex = Convert.ToHexString(digest).ToLowerInvariant();
        var truncated = hex.Substring(0, HmacSignatureHexLen);
        return HexToBase62(truncated);
    }

    /// <summary>
    /// Rejects a zero or negative expiry, which would give a URL that has already expired. Omit the expiry for a permanent
    /// URL. The reference client also requires a positive <c>expiresInSecs</c>.
    /// </summary>
    /// <param name="expiresInSecs">The expiry from the caller's arguments, or <c>null</c> when unset.</param>
    /// <param name="paramName">The caller's parameter name, used in the error message.</param>
    public static void ValidateExpiry(int? expiresInSecs, string paramName)
    {
        if (expiresInSecs is <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, expiresInSecs, paramName + " must be positive; omit it for a URL that never expires.");
        }
    }

    /// <summary>Builds the signature of a resource's public URL, as upstream <c>createStorageContentSignature</c>.</summary>
    /// <remarks>
    /// It signs <c>"{version}.{expiresAtMillis}.{resourceId}"</c> (<c>expiresAtMillis</c> is the absolute expiry, or
    /// <c>0</c> for none) with <see cref="CreateHmacSignature"/> and returns the base64url encoding, without padding, of
    /// <c>"{version}.{expiresAtMillis}.{hmac}"</c>.
    /// </remarks>
    /// <param name="secretKey">The URL-signing secret key of the store or dataset.</param>
    /// <param name="resourceId">The resource id being signed.</param>
    /// <param name="expiresInSecs">Optional expiry in seconds (<c>null</c> for none).</param>
    public static string SignStorageContent(string secretKey, string resourceId, int? expiresInSecs)
    {
        ValidateExpiry(expiresInSecs, nameof(expiresInSecs));
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

    /// <summary>Encodes a hex string, read as a big-endian integer, in base62 by long division, without a bignum dependency.</summary>
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

    /// <summary>Encodes bytes as base64url without padding.</summary>
    private static string Base64UrlNoPadding(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
