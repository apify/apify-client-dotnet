using System;
using System.Text.Json.Nodes;
using Apify.Client.Models;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Offline tests for the strongly-typed API enums: their wire mapping, parsing of raw values on models
/// (including graceful handling of unknown/absent values), and the terminal-status helper.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ApiEnumTests
{
    [Theory]
    [InlineData(ActorJobStatus.Ready, "READY")]
    [InlineData(ActorJobStatus.Running, "RUNNING")]
    [InlineData(ActorJobStatus.Succeeded, "SUCCEEDED")]
    [InlineData(ActorJobStatus.Failed, "FAILED")]
    [InlineData(ActorJobStatus.TimingOut, "TIMING-OUT")]
    [InlineData(ActorJobStatus.TimedOut, "TIMED-OUT")]
    [InlineData(ActorJobStatus.Aborting, "ABORTING")]
    [InlineData(ActorJobStatus.Aborted, "ABORTED")]
    public void ActorJobStatusRoundTripsThroughWireValue(ActorJobStatus status, string wire)
    {
        Assert.Equal(wire, status.ToWireValue());
    }

    [Theory]
    [InlineData(ActorJobStatus.Succeeded, true)]
    [InlineData(ActorJobStatus.Failed, true)]
    [InlineData(ActorJobStatus.Aborted, true)]
    [InlineData(ActorJobStatus.TimedOut, true)]
    [InlineData(ActorJobStatus.Ready, false)]
    [InlineData(ActorJobStatus.Running, false)]
    [InlineData(ActorJobStatus.TimingOut, false)]
    [InlineData(ActorJobStatus.Aborting, false)]
    public void ActorJobStatusIsTerminalMatchesFinishedStates(ActorJobStatus status, bool terminal)
    {
        Assert.Equal(terminal, status.IsTerminal());
    }

    [Fact]
    public void ActorRunStatusParsesWireValueAndUnknownBecomesNull()
    {
        Assert.Equal(ActorJobStatus.Succeeded, new ActorRun(new JsonObject { ["status"] = "SUCCEEDED" }).Status);
        Assert.Equal(ActorJobStatus.TimedOut, new ActorRun(new JsonObject { ["status"] = "TIMED-OUT" }).Status);

        // Unknown or absent status must not throw; it degrades to null while the raw value stays readable.
        var unknown = new ActorRun(new JsonObject { ["status"] = "SOMETHING-NEW" });
        Assert.Null(unknown.Status);
        Assert.Equal("SOMETHING-NEW", unknown.Get("status")!.GetValue<string>());
        Assert.Null(new ActorRun(new JsonObject()).Status);
    }

    [Fact]
    public void ActorRunIsTerminalReflectsStatus()
    {
        Assert.True(new ActorRun(new JsonObject { ["status"] = "SUCCEEDED" }).IsTerminal);
        Assert.False(new ActorRun(new JsonObject { ["status"] = "RUNNING" }).IsTerminal);
        Assert.False(new ActorRun(new JsonObject()).IsTerminal);
    }

    [Theory]
    [InlineData(RunOrigin.Development, "DEVELOPMENT")]
    [InlineData(RunOrigin.Web, "WEB")]
    [InlineData(RunOrigin.Api, "API")]
    [InlineData(RunOrigin.Scheduler, "SCHEDULER")]
    [InlineData(RunOrigin.Standby, "STANDBY")]
    [InlineData(RunOrigin.Mcp, "MCP")]
    public void RunOriginMapsToWireValue(RunOrigin origin, string wire)
    {
        Assert.Equal(wire, origin.ToWireValue());
    }

    [Theory]
    [InlineData(WebhookEventType.ActorRunSucceeded, "ACTOR.RUN.SUCCEEDED")]
    [InlineData(WebhookEventType.ActorRunTimedOut, "ACTOR.RUN.TIMED_OUT")]
    [InlineData(WebhookEventType.ActorBuildAborted, "ACTOR.BUILD.ABORTED")]
    [InlineData(WebhookEventType.Test, "TEST")]
    public void WebhookEventTypeMapsToWireValue(WebhookEventType eventType, string wire)
    {
        Assert.Equal(wire, eventType.ToWireValue());
    }

    [Fact]
    public void WebhookEventTypesParseAndSkipUnknown()
    {
        var webhook = new Webhook(new JsonObject
        {
            ["eventTypes"] = new JsonArray("ACTOR.RUN.SUCCEEDED", "SOMETHING.NEW", "ACTOR.BUILD.FAILED"),
        });

        var types = webhook.EventTypes;
        Assert.NotNull(types);
        Assert.Equal(new[] { WebhookEventType.ActorRunSucceeded, WebhookEventType.ActorBuildFailed }, types!);

        // Absent field yields null rather than an empty list.
        Assert.Null(new Webhook(new JsonObject()).EventTypes);
    }

    [Fact]
    public void PermissionLevelMapsToWireValue()
    {
        Assert.Equal("LIMITED_PERMISSIONS", PermissionLevel.LimitedPermissions.ToWireValue());
        Assert.Equal("FULL_PERMISSIONS", PermissionLevel.FullPermissions.ToWireValue());
    }
}
