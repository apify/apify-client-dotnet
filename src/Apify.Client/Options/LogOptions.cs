using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Configures log retrieval/streaming.</summary>
public sealed class LogOptions
{
    /// <summary>If <c>true</c>, return the unprocessed log content (no platform post-processing).</summary>
    public bool? Raw { get; init; }

    /// <summary>If <c>true</c>, set Content-Disposition so the log is served as a download.</summary>
    public bool? Download { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddBool("raw", Raw).AddBool("download", Download);
    }
}
