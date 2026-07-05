using System;
using System.Collections.Generic;

namespace Apify.Client.Internal;

/// <summary>
/// Run/build status helpers.
/// </summary>
internal static class Statuses
{
    /// <summary>Terminal run/build statuses: a resource in any of these is finished and will not change.</summary>
    private static readonly HashSet<string> Terminal = new(StringComparer.Ordinal)
    {
        "SUCCEEDED",
        "FAILED",
        "ABORTED",
        "TIMED-OUT",
    };

    /// <summary>Reports whether the status is a terminal (finished) run/build status.</summary>
    public static bool IsTerminal(string? status) => status is not null && Terminal.Contains(status);
}
