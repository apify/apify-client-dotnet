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
        Assert.Contains(osToken, new[] { "win32", "darwin", "linux", "android", "freebsd", "unknown" });
        Assert.DoesNotContain("windows", client.UserAgent, System.StringComparison.Ordinal);
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
