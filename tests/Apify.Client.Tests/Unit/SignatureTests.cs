using System;
using System.Text.RegularExpressions;
using Apify.Client.Internal;
using Xunit;

namespace Apify.Client.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class SignatureTests
{
    [Fact]
    public void HmacSignatureIsDeterministicAndBase62()
    {
        var sig = Signatures.CreateHmacSignature("secret-key", "my-message");
        Assert.Equal(sig, Signatures.CreateHmacSignature("secret-key", "my-message"));
        Assert.Matches(new Regex("^[0-9a-zA-Z]+$"), sig);
        Assert.NotEqual(sig, Signatures.CreateHmacSignature("secret-key", "other-message"));
    }

    /// <summary>
    /// Known-answer vectors pinned to values independently computed with a bignum oracle (matching the
    /// upstream @apify/utilities algorithm). Guards the byte-wise base62 long division and the base64url
    /// envelope against regressions.
    /// </summary>
    [Fact]
    public void KnownAnswerVectors()
    {
        Assert.Equal("G5BYW8zvRuVZrdxLfboF", Signatures.CreateHmacSignature("secret-key", "my-message"));
        Assert.Equal("Oj9uljsqvVPaH2iLmW4i", Signatures.CreateHmacSignature("secret", "0.0.resource-id"));
        Assert.Equal("MC4wLk9qOXVsanNxdlZQYUgyaUxtVzRp", Signatures.SignStorageContent("secret", "resource-id", null));
    }

    [Fact]
    public void StorageContentSignatureIsBase64UrlWithoutPadding()
    {
        var sig = Signatures.SignStorageContent("secret", "resource-id", null);
        Assert.DoesNotMatch(new Regex("[+/=]"), sig);

        var decoded = DecodeBase64Url(sig);
        // Envelope form: "{version}.{expiresAtMillis}.{hmac}"; non-expiring uses expiry 0.
        Assert.StartsWith("0.0.", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpiringSignatureEncodesFutureExpiry()
    {
        var sig = Signatures.SignStorageContent("secret", "rid", 3600);
        var decoded = DecodeBase64Url(sig);
        var parts = decoded.Split('.');
        Assert.True(long.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) > 0);
    }

    private static string DecodeBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '=');
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}
