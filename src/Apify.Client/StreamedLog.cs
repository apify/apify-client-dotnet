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
/// Redirects a run's (or build's) live log to a destination sink, one complete message at a time. Mirrors
/// the reference client's <c>StreamedLog</c> helper: it opens the raw log stream, splits it on Apify's
/// ISO-8601 timestamp line markers, and forwards each complete message to the <c>toLog</c> callback.
/// </summary>
/// <remarks>
/// The destination is modelled as an <see cref="Action{String}"/> (the idiomatic .NET equivalent of the
/// reference client's logger): each argument is one complete, trimmed log message. Redirection runs on a
/// background task started by <see cref="Start"/> and drained by <see cref="StopAsync"/> (or by disposal).
/// </remarks>
public sealed class StreamedLog : IAsyncDisposable
{
    /// <summary>
    /// Apify log lines are prefixed with an ISO-8601 UTC timestamp (e.g. <c>2024-01-02T03:04:05.678Z</c>).
    /// A timestamp at the start of a line marks the beginning of a new (possibly multi-line) message.
    /// </summary>
    private static readonly Regex MessageMarker = new(
        @"(?:\n|^)(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z)",
        RegexOptions.Compiled);

    /// <summary>Size of the read buffer used while draining the log stream.</summary>
    private const int ReadChunkChars = 4096;

    /// <summary>
    /// Pause before the first reopen of a log stream that ended before the run had logged anything. Each
    /// further reopen doubles the pause up to <see cref="EmptyStreamMaxRetryMillis"/>, bounding the request
    /// rate while a run waits a long time to start (e.g. for free memory).
    /// </summary>
    private const int EmptyStreamInitialRetryMillis = 500;

    /// <summary>Upper bound on the pause between reopens of an empty log stream.</summary>
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
        // When fromStart is false, ignore messages timestamped before this helper was created.
        _relevancyTimeLimit = fromStart ? null : DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Pass this as the <c>log</c> argument of <c>ActorClient.CallAsync</c> or <c>TaskClient.CallAsync</c> to get
    /// the reference client's default redirection: once the run has started, its log is written to the
    /// standard output with the prefix <c>&lt;Actor name&gt; runId:&lt;id&gt; -&gt; </c> (the run and its Actor are
    /// fetched to build it, as the reference client does). Invoked directly, it writes the message as it is.
    /// </summary>
    public static readonly Action<string> DefaultConsole = static message => Console.Out.WriteLine(message);

    /// <summary>
    /// Creates a ready-made sink that writes every message with the reference client's default prefix,
    /// <c>&lt;Actor name&gt; runId:&lt;id&gt; -&gt; </c>, one message per line, for a run that is already known (for
    /// <see cref="RunClient.GetStreamedLog"/>). <c>CallAsync</c> creates the run itself, so pass
    /// <see cref="DefaultConsole"/> there instead.
    /// </summary>
    /// <param name="runId">The run ID shown in the prefix.</param>
    /// <param name="actorName">The Actor name shown in the prefix; omitted from it when <c>null</c> or empty.</param>
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

    /// <summary>
    /// Stops redirecting the log and waits for the background task to drain. A no-op if not started.
    /// </summary>
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
            // Expected: cancellation is how redirection is stopped.
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
                // _logClient is always the run/build-nested (ambiguous) client (see the internal
                // constructor's only caller, RunClient.GetStreamedLog), which never resolves a 404 to
                // null — it throws instead.
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

                // The API serves the log of a run that has not logged anything yet as an empty stream that
                // closes at once, so reopen it with growing backoff until the first bytes arrive. Without
                // this, a run that was still queued (no memory free yet) when streaming started would never
                // have any of its log redirected, even once it did start producing output.
                await Task.Delay(TimeSpan.FromMilliseconds(retryMillis), cancellationToken).ConfigureAwait(false);
                retryMillis = Math.Min(retryMillis * 2, EmptyStreamMaxRetryMillis);
            }
        }
        catch (OperationCanceledException) when (!sawAnyBytes)
        {
            // Stopped before the run had logged anything (e.g. it was still queued): fetch whatever log
            // exists in one plain request instead of silently redirecting nothing. Best-effort — a failure
            // here must not turn a clean stop into a thrown exception.
            try
            {
                var content = await _logClient.GetAsync(new LogOptions { Raw = true }, CancellationToken.None).ConfigureAwait(false);
                buffer.Append(content);
            }
            catch (Exception)
            {
                // Leave the buffer as-is; the run's log (if any) simply isn't redirected for this stop.
            }
        }
        finally
        {
            // Emit whatever is left: a message without a trailing newline, or the fallback fetched above.
            FlushMessages(buffer, flushRemainder: true);
        }
    }

    /// <summary>
    /// Emits every complete message currently in <paramref name="buffer"/>. A message runs from one
    /// timestamp marker to the next; the final one is complete only when <paramref name="flushRemainder"/>
    /// is <c>true</c> (stream ended), otherwise it is kept in the buffer as a possibly-incomplete tail.
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
    /// Writes one message to the sink, skipping it when <c>fromStart</c> is disabled and the message's
    /// timestamp predates this helper's creation. Blank messages are dropped.
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
