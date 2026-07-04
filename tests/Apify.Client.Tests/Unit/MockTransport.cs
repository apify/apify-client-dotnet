using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Http;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// A snapshot of a request received by <see cref="MockTransport"/>, captured before the underlying
/// <see cref="HttpRequestMessage"/> is disposed by the client.
/// </summary>
public sealed class RecordedRequest
{
    private readonly Dictionary<string, string> _headers;

    internal RecordedRequest(string method, string uri, string body, byte[] bodyBytes, Dictionary<string, string> headers)
    {
        Method = method;
        Uri = uri;
        Body = body;
        BodyBytes = bodyBytes;
        _headers = headers;
    }

    public string Method { get; }

    public string Uri { get; }

    public string Body { get; }

    /// <summary>The raw (un-decoded) request body bytes, so binary write paths can be asserted verbatim.</summary>
    public byte[] BodyBytes { get; }

    public string Header(string name) => _headers.TryGetValue(name, out var value) ? value : string.Empty;
}

/// <summary>
/// A scripted <see cref="IHttpTransport"/> for offline unit tests. Each queued entry is either a response
/// to return or a transport failure to throw, consumed in order. All received requests are recorded for
/// assertions.
/// </summary>
public sealed class MockTransport : IHttpTransport
{
    private sealed record QueueEntry(bool IsError, bool Timeout, int Status, string Body, IReadOnlyDictionary<string, string>? Headers);

    private readonly Queue<QueueEntry> _queue = new();
    private readonly object _lock = new();
    private int _inFlight;

    public List<RecordedRequest> Received { get; } = new();

    public List<double> Timeouts { get; } = new();

    /// <summary>
    /// When set, every request is answered with a 200 whose <c>processedRequests</c> echoes each
    /// <c>uniqueKey</c> in the (JSON array) request body — so batch calls succeed regardless of the order in
    /// which concurrent chunks arrive. No queued responses are needed in this mode.
    /// </summary>
    public bool EchoBatchProcessed { get; set; }

    /// <summary>Artificial per-call delay (ms) used to force overlap when testing parallel dispatch.</summary>
    public int ArtificialDelayMillis { get; set; }

    /// <summary>The highest number of requests observed in flight at the same time.</summary>
    public int MaxObservedConcurrency { get; private set; }

    public MockTransport QueueResponse(int status, string body = "", IReadOnlyDictionary<string, string>? headers = null)
    {
        _queue.Enqueue(new QueueEntry(false, false, status, body, headers));
        return this;
    }

    public MockTransport QueueError(bool timeout = false)
    {
        _queue.Enqueue(new QueueEntry(true, timeout, 0, string.Empty, null));
        return this;
    }

    public RecordedRequest LastRequest =>
        Received.Count == 0 ? throw new InvalidOperationException("no request was received") : Received[^1];

    public int CallCount => Received.Count;

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, bool streaming, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        var body = string.Empty;
        var bodyBytes = Array.Empty<byte>();
        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }

            // Read the raw bytes so binary bodies can be asserted verbatim; keep a UTF-8 decode for the
            // string-body assertions (equivalent to ReadAsStringAsync for text content).
            bodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            body = System.Text.Encoding.UTF8.GetString(bodyBytes);
        }

        lock (_lock)
        {
            Received.Add(new RecordedRequest(request.Method.Method, request.RequestUri?.ToString() ?? string.Empty, body, bodyBytes, headers));
            Timeouts.Add(timeout.TotalSeconds);
            _inFlight++;
            MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, _inFlight);
        }

        try
        {
            if (ArtificialDelayMillis > 0)
            {
                await Task.Delay(ArtificialDelayMillis, cancellationToken).ConfigureAwait(false);
            }

            if (EchoBatchProcessed)
            {
                return BuildEchoResponse(body);
            }

            QueueEntry entry;
            lock (_lock)
            {
                if (_queue.Count == 0)
                {
                    throw new InvalidOperationException("MockTransport queue is empty");
                }

                entry = _queue.Dequeue();
            }

            if (entry.IsError)
            {
                throw new ApifyTransportException("mock transport failure", null, entry.Timeout);
            }

            var response = new HttpResponseMessage((HttpStatusCode)entry.Status)
            {
                Content = new StringContent(entry.Body),
            };
            if (entry.Headers is not null)
            {
                foreach (var header in entry.Headers)
                {
                    if (!response.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    {
                        response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            return response;
        }
        finally
        {
            lock (_lock)
            {
                _inFlight--;
            }
        }
    }

    /// <summary>Builds a 200 batch response echoing each request-body uniqueKey as processed.</summary>
    private static HttpResponseMessage BuildEchoResponse(string requestBody)
    {
        var processed = new System.Text.Json.Nodes.JsonArray();
        if (System.Text.Json.Nodes.JsonNode.Parse(requestBody) is System.Text.Json.Nodes.JsonArray array)
        {
            foreach (var item in array)
            {
                var key = item?["uniqueKey"]?.GetValue<string>();
                if (key is not null)
                {
                    processed.Add(new System.Text.Json.Nodes.JsonObject
                    {
                        ["uniqueKey"] = key,
                        ["requestId"] = "id-" + key,
                        ["wasAlreadyPresent"] = false,
                        ["wasAlreadyHandled"] = false,
                    });
                }
            }
        }

        var payload = new System.Text.Json.Nodes.JsonObject
        {
            ["data"] = new System.Text.Json.Nodes.JsonObject
            {
                ["processedRequests"] = processed,
                ["unprocessedRequests"] = new System.Text.Json.Nodes.JsonArray(),
            },
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload.ToJsonString()) };
    }
}
