using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Apify.Client.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class ConfigTests
{
    [Fact]
    public void UserAgentFormat()
    {
        var client = new ApifyClient(new ApifyClientOptions
        {
            Token = "test-token",
            HttpTransport = new MockTransport(),
            IsAtHome = () => false,
        });

        var ua = client.UserAgent;
        Assert.StartsWith("ApifyClient/" + ApifyClientVersion.ClientVersion, ua, System.StringComparison.Ordinal);
        Assert.Contains("; .NET/", ua, System.StringComparison.Ordinal);
        Assert.EndsWith("isAtHome/false", ua, System.StringComparison.Ordinal);
    }

    [Fact]
    public void UserAgentIsAtHomeTrueAndSuffix()
    {
        var client = new ApifyClient(new ApifyClientOptions
        {
            Token = "t",
            UserAgentSuffix = "my-suffix",
            HttpTransport = new MockTransport(),
            IsAtHome = () => true,
        });

        Assert.Contains("isAtHome/true", client.UserAgent, System.StringComparison.Ordinal);
        Assert.EndsWith("; my-suffix", client.UserAgent, System.StringComparison.Ordinal);
    }

    // The exact set of Node `os.platform()` tokens the reference JS client can emit. Every Apify client
    // must report one of these (plus "unknown" for platforms Node cannot run on at all), so the OS token
    // is identical across clients.
    private static readonly string[] ReferenceOsTokens =
    {
        "win32", "darwin", "linux", "android", "freebsd", "openbsd", "netbsd", "sunos", "aix",
    };

    [Fact]
    public void UserAgentOsTokenUsesShortLowercasePlatformIdentifier()
    {
        // The OS token must be a short, lowercase platform identifier aligned with the other Apify
        // clients' Node `os.platform()` values, not a uname-style name (e.g. "win32", never "windows").
        var client = new ApifyClient(new ApifyClientOptions
        {
            Token = "t",
            HttpTransport = new MockTransport(),
        });

        var osToken = Regex.Match(client.UserAgent, @"\(([^;]+);").Groups[1].Value;
        // The emitted token must be a reference os.platform() token (or "unknown" on non-Node platforms).
        Assert.Contains(osToken, ReferenceOsTokens.Append("unknown"));
        Assert.DoesNotContain("windows", client.UserAgent, System.StringComparison.Ordinal);
    }

    [Theory]
    // .NET's OperatingSystem helpers map exactly to the reference Node os.platform() tokens. macOS must be
    // "darwin" (never "osx"/"macos") and Windows must be "win32" (never "windows").
    [InlineData(true, false, false, false, false, "win32")]
    [InlineData(false, true, false, false, false, "darwin")]
    // Android is Linux-based, so it must win over the Linux check and report "android".
    [InlineData(false, false, true, true, false, "android")]
    [InlineData(false, false, false, true, false, "linux")]
    [InlineData(false, false, false, false, true, "freebsd")]
    public void ResolveOsTokenMapsHelperPlatformsToReferenceTokens(
        bool isWindows, bool isMacOs, bool isAndroid, bool isLinux, bool isFreeBsd, string expected)
    {
        // No extended platform matches for these cases; the helper booleans decide the token.
        var token = ApifyClient.ResolveOsToken(isWindows, isMacOs, isAndroid, isLinux, isFreeBsd, _ => false);

        Assert.Equal(expected, token);
    }

    [Theory]
    // Unix platforms .NET has no dedicated helper for, matched via RuntimeInformation.IsOSPlatform. Both
    // Solaris and illumos report as "sunos", matching Node.
    [InlineData("OPENBSD", "openbsd")]
    [InlineData("NETBSD", "netbsd")]
    [InlineData("SOLARIS", "sunos")]
    [InlineData("ILLUMOS", "sunos")]
    [InlineData("AIX", "aix")]
    public void ResolveOsTokenMapsExtendedPlatformsToReferenceTokens(string osPlatform, string expected)
    {
        var target = OSPlatform.Create(osPlatform);
        Func<OSPlatform, bool> isOsPlatform = platform => platform == target;

        // No helper platform matches, so resolution falls through to the extended-platform lookup.
        var token = ApifyClient.ResolveOsToken(false, false, false, false, false, isOsPlatform);

        Assert.Equal(expected, token);
    }

    [Fact]
    public void ResolveOsTokenFallsBackToUnknownForNonNodePlatforms()
    {
        // A platform with no OperatingSystem helper match and no extended-platform match (e.g. iOS or the
        // browser, where the reference's Node runtime cannot run) has no reference token and reports "unknown".
        Assert.Equal("unknown", ApifyClient.ResolveOsToken(false, false, false, false, false, _ => false));
    }

    [Fact]
    public void ApiBaseUrlAppendsV2()
    {
        var client = new ApifyClient(new ApifyClientOptions { Token = "t", BaseUrl = "https://api.example.com/", HttpTransport = new MockTransport() });
        Assert.Equal("https://api.example.com/v2", client.ApiBaseUrl);
    }

    [Fact]
    public void VersionConstants()
    {
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+$"), ApifyClientVersion.ClientVersion);
        Assert.StartsWith("v2-", ApifyClientVersion.ApiSpecVersion, System.StringComparison.Ordinal);
    }
}
