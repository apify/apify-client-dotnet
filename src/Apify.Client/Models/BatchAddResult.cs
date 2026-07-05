using System.Collections.Generic;

namespace Apify.Client.Models;

/// <summary>
/// The result of a batch request-add: the accepted (processed) and the unprocessed requests.
/// </summary>
public sealed class BatchAddResult
{
    private List<RequestQueueOperationInfo> _processed;
    private List<RequestQueueRequest> _unprocessed;

    /// <summary>Creates a result with the given processed and unprocessed requests.</summary>
    /// <param name="processedRequests">The requests the API successfully added.</param>
    /// <param name="unprocessedRequests">The requests the API did not process.</param>
    public BatchAddResult(
        IEnumerable<RequestQueueOperationInfo>? processedRequests = null,
        IEnumerable<RequestQueueRequest>? unprocessedRequests = null)
    {
        _processed = processedRequests is null ? new List<RequestQueueOperationInfo>() : new List<RequestQueueOperationInfo>(processedRequests);
        _unprocessed = unprocessedRequests is null ? new List<RequestQueueRequest>() : new List<RequestQueueRequest>(unprocessedRequests);
    }

    /// <summary>The requests the API successfully added.</summary>
    public IReadOnlyList<RequestQueueOperationInfo> ProcessedRequests => _processed;

    /// <summary>The requests the API did not process.</summary>
    public IReadOnlyList<RequestQueueRequest> UnprocessedRequests => _unprocessed;

    /// <summary>Replaces the processed requests.</summary>
    internal void SetProcessedRequests(List<RequestQueueOperationInfo> processedRequests) => _processed = processedRequests;

    /// <summary>Replaces the unprocessed requests.</summary>
    internal void SetUnprocessedRequests(List<RequestQueueRequest> unprocessedRequests) => _unprocessed = unprocessedRequests;

    /// <summary>Appends another result's requests into this one (used to merge per-chunk batch results).</summary>
    internal void Merge(BatchAddResult other)
    {
        _processed.AddRange(other._processed);
        _unprocessed.AddRange(other._unprocessed);
    }
}
