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

    // The Node `os.platform()` tokens the reference client can emit, plus "unknown" for platforms Node does not run on.
    private static readonly string[] ReferenceOsTokens =
    {
        "win32", "darwin", "linux", "android", "freebsd", "openbsd", "netbsd", "sunos", "aix",
    };

    [Fact]
    public void UserAgentOsTokenUsesShortLowercasePlatformIdentifier()
    {
        // The OS token is a short lowercase Node `os.platform()` value, never a uname-style name ("win32", not "windows").
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
    // OperatingSystem helpers map to the Node tokens: "darwin" (not "osx" or "macos") and "win32" (not "windows").
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
    // Unix platforms without an OperatingSystem helper are matched with RuntimeInformation.IsOSPlatform. Solaris and
    // illumos report "sunos", as in Node.
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
        // A platform that no helper or extended lookup matches (iOS, the browser) has no Node token and reports "unknown".
        Assert.Equal("unknown", ApifyClient.ResolveOsToken(false, false, false, false, false, _ => false));
    }

    [Fact]
    public void ApiBaseUrlAppendsV2()
    {
        var client = new ApifyClient(new ApifyClientOptions { Token = "t", BaseUrl = "https://api.example.com/", HttpTransport = new MockTransport() });
        Assert.Equal("https://api.example.com/v2", client.ApiBaseUrl);
    }

    [Fact]
    public void ApiBaseUrlAcceptsAlreadyPresentV2WithoutDoublingIt()
    {
        var client = new ApifyClient(new ApifyClientOptions { Token = "t", BaseUrl = "https://api.example.com/v2", HttpTransport = new MockTransport() });
        Assert.Equal("https://api.example.com/v2", client.ApiBaseUrl);
    }

    [Fact]
    public void ApiBaseUrlAcceptsAlreadyPresentV2WithTrailingSlashWithoutDoublingIt()
    {
        var client = new ApifyClient(new ApifyClientOptions { Token = "t", BaseUrl = "https://api.example.com/v2/", HttpTransport = new MockTransport() });
        Assert.Equal("https://api.example.com/v2", client.ApiBaseUrl);
    }

    [Fact]
    public void ApiBaseUrlTreatsOtherVersionSegmentAsHostPathAndStillAppendsV2()
    {
        var client = new ApifyClient(new ApifyClientOptions { Token = "t", BaseUrl = "https://api.example.com/v3", HttpTransport = new MockTransport() });
        Assert.Equal("https://api.example.com/v3/v2", client.ApiBaseUrl);
    }

    [Fact]
    public void VersionConstants()
    {
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+$"), ApifyClientVersion.ClientVersion);
        Assert.StartsWith("v2-", ApifyClientVersion.ApiSpecVersion, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Every id-addressed accessor rejects an empty id, as the reference client does: it would otherwise address a
    /// collection URL such as <c>.../actor-runs/</c>.
    /// </summary>
    public static TheoryData<string, Action<ApifyClient>> EmptyIdAccessors()
    {
        var data = new TheoryData<string, Action<ApifyClient>>
        {
            { "Actor", c => c.Actor("") },
            { "Build", c => c.Build("") },
            { "Run", c => c.Run("") },
            { "Dataset", c => c.Dataset("") },
            { "KeyValueStore", c => c.KeyValueStore("") },
            { "RequestQueue", c => c.RequestQueue("") },
            { "Task", c => c.Task("") },
            { "Schedule", c => c.Schedule("") },
            { "Webhook", c => c.Webhook("") },
            { "WebhookDispatch", c => c.WebhookDispatch("") },
            { "Log", c => c.Log("") },
            { "User", c => c.User("") },
        };
        return data;
    }

    [Theory]
    [MemberData(nameof(EmptyIdAccessors))]
    public void AccessorRejectsEmptyId(string accessorName, Action<ApifyClient> callAccessor)
    {
        var client = new ApifyClient(new ApifyClientOptions { Token = "t", HttpTransport = new MockTransport() });

        var ex = Record.Exception(() => callAccessor(client));

        Assert.True(ex is ArgumentException, $"{accessorName}(\"\") should throw ArgumentException, threw: {ex?.GetType().Name ?? "nothing"}");
    }
}
