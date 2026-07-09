using System;

namespace Apify.Client.Models;

/// <summary>
/// An event that can trigger a webhook (the <c>WebhookEventType</c> schema in the Apify API).
/// </summary>
public enum WebhookEventType
{
    /// <summary>An Actor run was created.</summary>
    ActorRunCreated,

    /// <summary>An Actor run finished successfully.</summary>
    ActorRunSucceeded,

    /// <summary>An Actor run failed.</summary>
    ActorRunFailed,

    /// <summary>An Actor run timed out.</summary>
    ActorRunTimedOut,

    /// <summary>An Actor run was aborted.</summary>
    ActorRunAborted,

    /// <summary>An Actor run was resurrected.</summary>
    ActorRunResurrected,

    /// <summary>An Actor build was created.</summary>
    ActorBuildCreated,

    /// <summary>An Actor build finished successfully.</summary>
    ActorBuildSucceeded,

    /// <summary>An Actor build failed.</summary>
    ActorBuildFailed,

    /// <summary>An Actor build timed out.</summary>
    ActorBuildTimedOut,

    /// <summary>An Actor build was aborted.</summary>
    ActorBuildAborted,

    /// <summary>A test event used to verify a webhook is configured correctly.</summary>
    Test,
}

/// <summary>
/// Maps <see cref="WebhookEventType"/> to and from its API wire representation. <see cref="ToWireValue"/>
/// is public because webhook definitions are created from free-form objects, so callers building an
/// <c>eventTypes</c> array need to turn the enum into its API string themselves.
/// </summary>
public static class WebhookEventTypeExtensions
{
    /// <summary>The wire value the API uses for this event type (e.g. <c>ACTOR.RUN.SUCCEEDED</c>).</summary>
    public static string ToWireValue(this WebhookEventType eventType) => eventType switch
    {
        WebhookEventType.ActorRunCreated => "ACTOR.RUN.CREATED",
        WebhookEventType.ActorRunSucceeded => "ACTOR.RUN.SUCCEEDED",
        WebhookEventType.ActorRunFailed => "ACTOR.RUN.FAILED",
        WebhookEventType.ActorRunTimedOut => "ACTOR.RUN.TIMED_OUT",
        WebhookEventType.ActorRunAborted => "ACTOR.RUN.ABORTED",
        WebhookEventType.ActorRunResurrected => "ACTOR.RUN.RESURRECTED",
        WebhookEventType.ActorBuildCreated => "ACTOR.BUILD.CREATED",
        WebhookEventType.ActorBuildSucceeded => "ACTOR.BUILD.SUCCEEDED",
        WebhookEventType.ActorBuildFailed => "ACTOR.BUILD.FAILED",
        WebhookEventType.ActorBuildTimedOut => "ACTOR.BUILD.TIMED_OUT",
        WebhookEventType.ActorBuildAborted => "ACTOR.BUILD.ABORTED",
        WebhookEventType.Test => "TEST",
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "unknown webhook event type"),
    };

    /// <summary>
    /// Parses an API wire value into a <see cref="WebhookEventType"/>, or returns <c>null</c> if the value is
    /// not a recognized event type.
    /// </summary>
    internal static WebhookEventType? FromWireValue(string? value) => value switch
    {
        "ACTOR.RUN.CREATED" => WebhookEventType.ActorRunCreated,
        "ACTOR.RUN.SUCCEEDED" => WebhookEventType.ActorRunSucceeded,
        "ACTOR.RUN.FAILED" => WebhookEventType.ActorRunFailed,
        "ACTOR.RUN.TIMED_OUT" => WebhookEventType.ActorRunTimedOut,
        "ACTOR.RUN.ABORTED" => WebhookEventType.ActorRunAborted,
        "ACTOR.RUN.RESURRECTED" => WebhookEventType.ActorRunResurrected,
        "ACTOR.BUILD.CREATED" => WebhookEventType.ActorBuildCreated,
        "ACTOR.BUILD.SUCCEEDED" => WebhookEventType.ActorBuildSucceeded,
        "ACTOR.BUILD.FAILED" => WebhookEventType.ActorBuildFailed,
        "ACTOR.BUILD.TIMED_OUT" => WebhookEventType.ActorBuildTimedOut,
        "ACTOR.BUILD.ABORTED" => WebhookEventType.ActorBuildAborted,
        "TEST" => WebhookEventType.Test,
        _ => null,
    };
}
