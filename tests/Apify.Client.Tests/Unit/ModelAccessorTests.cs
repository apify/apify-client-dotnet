using System.Text.Json.Nodes;
using Apify.Client.Models;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// The typed accessors for fields the OpenAPI specification added to the build, run, storage, webhook, schedule
/// and request models (spec v2-2026-10-29T100357Z).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ModelAccessorTests
{
    [Fact]
    public void BuildExposesItsSourceAndExitFields()
    {
        var build = new Build(JsonNode.Parse(
            "{\"changelog\":\"Fixed\",\"exitCode\":0,\"gitBranchName\":\"main\",\"gitCommitId\":\"abc123\"}")!.AsObject());

        Assert.Equal("Fixed", build.Changelog);
        Assert.Equal(0, build.ExitCode);
        Assert.Equal("main", build.GitBranchName);
        Assert.Equal("abc123", build.GitCommitId);
    }

    [Fact]
    public void RunExposesItsConsoleUrlNormalizedAndItsStandbyConfiguration()
    {
        var run = new ActorRun(JsonNode.Parse(
            "{\"consoleUrl\":\"HTTPS://Console.Apify.com:443/view/runs/r1\",\"standby\":{\"deployment\":\"MULTI_TENANT_PAYING_USERS\"}}")!.AsObject());

        Assert.Equal("https://console.apify.com/view/runs/r1", run.ConsoleUrl);
        Assert.Equal("MULTI_TENANT_PAYING_USERS", run.Standby!["deployment"]!.GetValue<string>());
    }

    [Fact]
    public void RunWithoutStandbyHasNone()
    {
        var run = new ActorRun(new JsonObject());

        Assert.Null(run.Standby);
        Assert.Null(run.ConsoleUrl);
    }

    [Fact]
    public void StoragesExposeTheirTitle()
    {
        var data = JsonNode.Parse("{\"title\":\"My storage\"}")!.AsObject();

        Assert.Equal("My storage", new Dataset(data).Title);
        Assert.Equal("My storage", new KeyValueStore(data.DeepClone().AsObject()).Title);
        Assert.Equal("My storage", new RequestQueue(data.DeepClone().AsObject()).Title);
    }

    [Fact]
    public void WebhookExposesItsTitleAndFlags()
    {
        var webhook = new Webhook(JsonNode.Parse("{\"title\":\"Notify\",\"isEnabled\":true,\"isApifyIntegration\":false}")!.AsObject());

        Assert.Equal("Notify", webhook.Title);
        Assert.True(webhook.IsEnabled);
        Assert.False(webhook.IsApifyIntegration);
    }

    [Fact]
    public void ScheduleExposesItsTitleDescriptionAndEditorState()
    {
        var schedule = new Schedule(JsonNode.Parse("{\"title\":\"Nightly\",\"description\":\"d\",\"uiState\":{\"a\":1}}")!.AsObject());

        Assert.Equal("Nightly", schedule.Title);
        Assert.Equal("d", schedule.Description);
        Assert.Equal(1, schedule.UiState!["a"]!.GetValue<int>());
    }

    [Fact]
    public void ScheduleWithNullEditorStateHasNone()
    {
        var schedule = new Schedule(JsonNode.Parse("{\"uiState\":null}")!.AsObject());

        Assert.Null(schedule.UiState);
    }

    [Fact]
    public void RequestExposesItsLockHolderAndHandledAt()
    {
        var request = RequestQueueRequest.FromJsonObject(JsonNode.Parse(
            "{\"uniqueKey\":\"k\",\"url\":\"https://a.com\",\"lockByClient\":\"client-1\",\"handledAt\":\"2026-01-01T00:00:00.000Z\"}")!.AsObject());

        Assert.Equal("client-1", request.LockByClient);
        Assert.Equal("2026-01-01T00:00:00.000Z", request.HandledAt);
    }

    [Fact]
    public void PendingRequestWithNullHandledAtHasNoHandledAt()
    {
        var request = RequestQueueRequest.FromJsonObject(JsonNode.Parse(
            "{\"id\":\"r1\",\"uniqueKey\":\"k\",\"url\":\"https://a.com\",\"handledAt\":null}")!.AsObject());

        Assert.Null(request.HandledAt);
    }
}
