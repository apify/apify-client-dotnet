using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Http;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// How <see cref="HttpClientTransport"/> honours <see cref="ApifyRequestOptions.FollowRedirects"/>, against a
/// local server that redirects <c>/start</c> to <c>/target</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HttpClientTransportRedirectTests : IAsyncDisposable
{
    private const string TargetBody = "reached the target";

    private readonly HttpListener _listener = new();
    private readonly string _origin;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serving;

    public HttpClientTransportRedirectTests()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        _origin = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add(_origin + "/");
        _listener.Start();
        _serving = Task.Run(ServeAsync);
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Close();
        await _serving;
        _stop.Dispose();
    }

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            if (context.Request.Url!.AbsolutePath == "/start")
            {
                context.Response.StatusCode = (int)HttpStatusCode.Found;
                context.Response.RedirectLocation = "/target";
            }
            else
            {
                await using var writer = new System.IO.StreamWriter(context.Response.OutputStream);
                await writer.WriteAsync(TargetBody);
            }

            context.Response.Close();
        }
    }

    private HttpRequestMessage StartRequest(bool followRedirects)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, _origin + "/start");
        if (!followRedirects)
        {
            request.Options.Set(ApifyRequestOptions.FollowRedirects, false);
        }

        return request;
    }

    [Fact]
    public async Task RequestsFollowRedirectsByDefault()
    {
        using var transport = new HttpClientTransport();
        using var request = StartRequest(followRedirects: true);

        using var response = await transport.SendAsync(request, TimeSpan.FromSeconds(10), streaming: false, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TargetBody, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ARequestThatMustNotFollowRedirectsGetsTheRedirectResponse()
    {
        using var transport = new HttpClientTransport();
        using var request = StartRequest(followRedirects: false);

        using var response = await transport.SendAsync(request, TimeSpan.FromSeconds(10), streaming: false, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/target", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ASuppliedHttpClientCannotSendARequestThatMustNotFollowRedirects()
    {
        using var httpClient = new HttpClient();
        using var transport = new HttpClientTransport(httpClient);
        using var request = StartRequest(followRedirects: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.SendAsync(request, TimeSpan.FromSeconds(10), streaming: false, CancellationToken.None));

        Assert.Contains("noRedirectHttpClient", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASuppliedNoRedirectHttpClientSendsARequestThatMustNotFollowRedirects()
    {
        using var httpClient = new HttpClient();
        using var noRedirectHandler = new HttpClientHandler { AllowAutoRedirect = false };
        using var noRedirectClient = new HttpClient(noRedirectHandler);
        using var transport = new HttpClientTransport(httpClient, noRedirectClient);
        using var request = StartRequest(followRedirects: false);

        using var response = await transport.SendAsync(request, TimeSpan.FromSeconds(10), streaming: false, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }
}
