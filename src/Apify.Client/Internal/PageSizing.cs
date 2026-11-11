using System;

namespace Apify.Client.Internal;

/// <summary>
/// Handling of the client-side <c>ChunkSize</c> iteration option, the most items requested per API call. It only sizes
/// each page's <c>limit</c> and is never sent itself, as in the reference client.
/// </summary>
internal static class PageSizing
{
    /// <summary>Rejects a zero or negative chunk size, which the reference client's options schema also refuses.</summary>
    /// <param name="chunkSize">The chunk size from the caller's options, or <c>null</c> when unset.</param>
    /// <param name="optionName">The option's public name, used in the error message.</param>
    public static void ValidateChunkSize(int? chunkSize, string optionName)
    {
        if (chunkSize is <= 0)
        {
            throw new ArgumentOutOfRangeException(optionName, chunkSize, optionName + " must be positive.");
        }
    }

    /// <summary>
    /// The smaller of two page limits. The API reads <c>0</c> as unset, so <c>0</c> and <c>null</c> mean "no limit" and lose
    /// to any other value; the result is <c>null</c> when neither is set. As the reference client's <c>minForLimitParam</c>.
    /// </summary>
    public static int? MinForLimit(int? a, int? b)
    {
        a = a is 0 ? null : a;
        b = b is 0 ? null : b;
        if (a is null)
        {
            return b;
        }

        return b is null ? a : Math.Min(a.Value, b.Value);
    }
}
