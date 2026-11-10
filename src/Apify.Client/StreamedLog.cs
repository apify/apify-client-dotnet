using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Options;
using Apify.Client.Resources;

namespace Apify.Client;

/// <summary>
/// Redirects a run's or build's live log to a sink, one complete message at a time, as the reference client's
/// <c>StreamedLog</c> does: it reads the raw log stream, splits it at the ISO 8601 timestamps that start each message
/// and passes each message to the <c>toLog</c> callback.
/// </summary>
/// <remarks>
/// Each callback argument is one complete, trimmed message. Redirection runs on a background task started by
/// <see cref="Start"/> and drained by <see cref="StopAsync"/> or disposal.
/// </remarks>
public sealed class StreamedLog : IAsyncDisposable
{
    /// <summary>
    /// Matches the ISO 8601 UTC timestamp (<c>2024-01-02T03:04:05.678Z</c>) that starts each message of the Apify log;
    /// a message can span several lines.
    /// </summary>
    private static readonly Regex MessageMarker = new(
        @"(?:\n|^)(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z)",
        RegexOptions.Compiled);

    /// <summary>Size of the read buffer.</summary>
    private const int ReadChunkChars = 4096;

    /// <summary>
    /// Pause before the first reopen of a log stream that ended before the run logged anything. Each further reopen
    /// doubles it up to <see cref="EmptyStreamMaxRetryMillis"/>.
    /// </summary>
    private const int EmptyStreamInitialRetryMillis = 500;

    /// <summary>Longest pause between reopens of an empty log stream.</summary>
    private const int EmptyStreamMaxRetryMillis = 5_000;

    private readonly LogClient _logClient;
    private readonly Action<string> _toLog;
    private readonly DateTimeOffset? _relevancyTimeLimit;
    private readonly object _lock = new();

    private CancellationTokenSource? _cts;
    private Task? _streamingTask;

    internal StreamedLog(LogClient logClient, Action<string> toLog, bool fromStart)
    {
        _logClient = logClient;
        _toLog = toLog;
        // With fromStart false, ignore messages from before this helper was created.
        _relevancyTimeLimit = fromStart ? null : DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Pass this as the <c>log</c> argument of <c>ActorClient.CallAsync</c> or <c>TaskClient.CallAsync</c> to write the run's
    /// log to the standard output with the prefix <c>&lt;Actor name&gt; runId:&lt;id&gt; -&gt; </c>, as the reference client
    /// does by default. Only <c>CallAsync</c> recognizes it; <see cref="RunClient.GetStreamedLog"/> uses a sink as it is, so
    /// use <see cref="CreatePrefixedSink"/> there.
    /// </summary>
    public static readonly Action<string> DefaultConsole = static message => Console.Out.WriteLine(message);

    /// <summary>
    /// Creates a sink that writes each message on one line with the prefix <c>&lt;Actor name&gt; runId:&lt;id&gt; -&gt; </c>,
    /// for a run that is already known (<see cref="RunClient.GetStreamedLog"/>).
    /// </summary>
    /// <param name="runId">The run ID shown in the prefix.</param>
    /// <param name="actorName">The Actor name shown in the prefix; omitted when <c>null</c> or empty.</param>
    /// <param name="writer">Where the lines are written (default: the standard output).</param>
    public static Action<string> CreatePrefixedSink(string runId, string? actorName = null, TextWriter? writer = null)
    {
        var name = string.IsNullOrEmpty(actorName) ? "runId:" + runId : actorName + " runId:" + runId;
        var prefix = name + " -> ";
        return message => (writer ?? Console.Out).WriteLine(prefix + message);
    }

    /// <summary>Starts redirecting the log on a background task. Throws if already started.</summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_streamingTask is not null)
            {
                throw new InvalidOperationException("Log streaming is already active.");
            }

            _cts = new CancellationTokenSource();
            _streamingTask = StreamLogAsync(_cts.Token);
        }
    }

    /// <summary>Stops redirecting and waits for the background task to drain. A no-op if not started.</summary>
    public async Task StopAsync()
    {
        Task? task;
        CancellationTokenSource? cts;
        lock (_lock)
        {
            task = _streamingTask;
            cts = _cts;
            _streamingTask = null;
            _cts = null;
        }

        if (task is null || cts is null)
        {
            return;
        }

        cts.Cancel();
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected: cancellation stops the redirection.
        }
        finally
        {
            cts.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task StreamLogAsync(CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();
        var sawAnyBytes = false;
        var retryMillis = EmptyStreamInitialRetryMillis;

        try
        {
            while (!sawAnyBytes)
            {
                // _logClient is the run's or build's nested client, which throws on a 404 instead of returning null.
                using var stream = (await _logClient.StreamAsync(new LogOptions { Raw = true }, cancellationToken).ConfigureAwait(false))!;
                using var reader = new StreamReader(stream, Encoding.UTF8);

                var chunk = new char[ReadChunkChars];
                int read;
                while ((read = await reader.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    sawAnyBytes = true;
                    buffer.Append(chunk, 0, read);
                    FlushMessages(buffer, flushRemainder: false);
                }

                if (sawAnyBytes)
                {
                    break;
                }

                // A run that has not logged anything yet is served as an empty stream that closes at once, so reopen it with growing
                // backoff until the first bytes arrive. Otherwise the log of a run still queued when streaming started would never be redirected.
                await Task.Delay(TimeSpan.FromMilliseconds(retryMillis), cancellationToken).ConfigureAwait(false);
                retryMillis = Math.Min(retryMillis * 2, EmptyStreamMaxRetryMillis);
            }
        }
        catch (OperationCanceledException) when (!sawAnyBytes)
        {
            // Stopped before the run logged anything (for example it was queued): fetch whatever log exists in one plain request.
            // Best effort, so a failure must not turn a clean stop into an exception.
            try
            {
                var content = await _logClient.GetAsync(new LogOptions { Raw = true }, CancellationToken.None).ConfigureAwait(false);
                buffer.Append(content);
            }
            catch (Exception)
            {
                // Leave the buffer as it is; the log is not redirected for this stop.
            }
        }
        finally
        {
            // Emit what is left: a message without a trailing newline, or the fallback fetched above.
            FlushMessages(buffer, flushRemainder: true);
        }
    }

    /// <summary>
    /// Emits every complete message in <paramref name="buffer"/>. A message runs from one timestamp to the next; the last
    /// one is complete only with <paramref name="flushRemainder"/> (the stream ended), otherwise it stays in the buffer.
    /// </summary>
    private void FlushMessages(StringBuilder buffer, bool flushRemainder)
    {
        var text = buffer.ToString();
        var matches = MessageMarker.Matches(text);
        if (matches.Count == 0)
        {
            if (flushRemainder)
            {
                EmitMessage(text, timestamp: null);
                buffer.Clear();
            }

            return;
        }

        var completeCount = flushRemainder ? matches.Count : matches.Count - 1;
        for (var i = 0; i < completeCount; i++)
        {
            var start = matches[i].Groups[1].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Groups[1].Index : text.Length;
            EmitMessage(text.Substring(start, end - start), matches[i].Groups[1].Value);
        }

        if (flushRemainder)
        {
            buffer.Clear();
        }
        else
        {
            var lastStart = matches[matches.Count - 1].Groups[1].Index;
            buffer.Clear();
            buffer.Append(text, lastStart, text.Length - lastStart);
        }
    }

    /// <summary>
    /// Writes one message to the sink. Blank messages are dropped, and so are messages older than this helper when
    /// <c>fromStart</c> is off.
    /// </summary>
    private void EmitMessage(string message, string? timestamp)
    {
        if (_relevancyTimeLimit is not null
            && timestamp is not null
            && DateTimeOffset.TryParse(
                timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var logTime)
            && logTime < _relevancyTimeLimit.Value)
        {
            return;
        }

        var trimmed = message.Trim();
        if (trimmed.Length > 0)
        {
            _toLog(trimmed);
        }
    }
}
