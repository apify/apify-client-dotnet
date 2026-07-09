using System;

namespace Apify.Client.Models;

/// <summary>
/// How an Actor run was started (the <c>RunOrigin</c> schema in the Apify API).
/// </summary>
public enum RunOrigin
{
    /// <summary>Started from the Actor's development console.</summary>
    Development,

    /// <summary>Started from the Apify Console web UI.</summary>
    Web,

    /// <summary>Started programmatically through the API.</summary>
    Api,

    /// <summary>Started by a schedule.</summary>
    Scheduler,

    /// <summary>Started as part of a test.</summary>
    Test,

    /// <summary>Started by a webhook.</summary>
    Webhook,

    /// <summary>Started by another Actor.</summary>
    Actor,

    /// <summary>Started from the Apify CLI.</summary>
    Cli,

    /// <summary>Started by a CI/CD pipeline.</summary>
    Ci,

    /// <summary>Started by the Actor Standby feature.</summary>
    Standby,

    /// <summary>Started via the Model Context Protocol (MCP).</summary>
    Mcp,
}

/// <summary>
/// Maps <see cref="RunOrigin"/> to its API wire representation. Internal: an origin is only ever passed
/// to the client through <c>LastRunOptions.Origin</c>, so callers never stringify it themselves.
/// </summary>
internal static class RunOriginExtensions
{
    /// <summary>The wire value the API uses for this origin (e.g. <c>SCHEDULER</c>).</summary>
    public static string ToWireValue(this RunOrigin origin) => origin switch
    {
        RunOrigin.Development => "DEVELOPMENT",
        RunOrigin.Web => "WEB",
        RunOrigin.Api => "API",
        RunOrigin.Scheduler => "SCHEDULER",
        RunOrigin.Test => "TEST",
        RunOrigin.Webhook => "WEBHOOK",
        RunOrigin.Actor => "ACTOR",
        RunOrigin.Cli => "CLI",
        RunOrigin.Ci => "CI",
        RunOrigin.Standby => "STANDBY",
        RunOrigin.Mcp => "MCP",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "unknown run origin"),
    };
}
