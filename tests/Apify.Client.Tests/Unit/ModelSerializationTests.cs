using System;
using System.Text.Json.Nodes;
using Apify.Client.Models;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Offline tests for the model "null fields are omitted" serialization contract: setting a property to
/// <c>null</c> must remove the key from the underlying JSON object rather than writing a JSON <c>null</c>
/// node (which the API would treat as an explicit null).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ModelSerializationTests
{
    [Fact]
    public void RequestQueueRequestUserDataNullRemovesKey()
    {
        var request = new RequestQueueRequest("https://a.com", "k");
        request.UserData = new JsonObject { ["label"] = "DETAIL" };
        Assert.True(request.ToJsonObject().ContainsKey("userData"));

        request.UserData = null;
        Assert.False(request.ToJsonObject().ContainsKey("userData"));
    }

    [Fact]
    public void RequestQueueRequestUserDataStoresIndependentCopy()
    {
        var data = new JsonObject { ["label"] = "DETAIL" };
        var request = new RequestQueueRequest("https://a.com", "k") { UserData = data };

        // Mutating the caller's object must not change the stored request (deep-cloned on set).
        data["label"] = "MUTATED";
        Assert.Equal("DETAIL", request.UserData!["label"]!.GetValue<string>());
    }

    [Fact]
    public void ActorEnvVarNullSettersRemoveKeys()
    {
        var envVar = new ActorEnvVar("NAME", "value", isSecret: true);
        var json = envVar.ToJsonObject();
        Assert.True(json.ContainsKey("name"));
        Assert.True(json.ContainsKey("value"));
        Assert.True(json.ContainsKey("isSecret"));

        envVar.Name = null;
        envVar.Value = null;
        envVar.IsSecret = null;

        Assert.False(json.ContainsKey("name"));
        Assert.False(json.ContainsKey("value"));
        Assert.False(json.ContainsKey("isSecret"));
    }

    [Fact]
    public void ActorEnvVarConstructorOmitsUnsetFields()
    {
        // Unset (null) constructor args must not appear as null nodes in the payload.
        var envVar = new ActorEnvVar(name: "ONLY_NAME");
        var json = envVar.ToJsonObject();

        Assert.True(json.ContainsKey("name"));
        Assert.False(json.ContainsKey("value"));
        Assert.False(json.ContainsKey("isSecret"));
    }

    [Fact]
    public void ActorEnvVarSettersWriteTypedValues()
    {
        var envVar = new ActorEnvVar();
        envVar.Name = "K";
        envVar.Value = "V";
        envVar.IsSecret = true;

        var json = envVar.ToJsonObject();
        Assert.Equal("K", json["name"]!.GetValue<string>());
        Assert.Equal("V", json["value"]!.GetValue<string>());
        Assert.True(json["isSecret"]!.GetValue<bool>());
    }

    [Fact]
    public void BuildImageDigestReadsFromRawField()
    {
        var build = new Build(new JsonObject
        {
            ["id"] = "build1",
            ["imageDigest"] = "1b2f1e8c0d5a4c7f9e3b6a2d8c4e0f7a5b9d3c1e6f8a2b4d0c7e9f1a3b5d7c9e",
        });

        Assert.Equal("1b2f1e8c0d5a4c7f9e3b6a2d8c4e0f7a5b9d3c1e6f8a2b4d0c7e9f1a3b5d7c9e", build.ImageDigest);
    }

    [Fact]
    public void BuildImageDigestIsNullWhenAbsent()
    {
        var build = new Build(new JsonObject { ["id"] = "build1" });

        Assert.Null(build.ImageDigest);
    }

    [Fact]
    public void ActorRunContainerUrlIsNormalized()
    {
        // Matches the reference client's z.url({ normalize: true }): lowercased/punycoded host, default
        // port dropped, empty path becomes "/".
        var run = new ActorRun(new JsonObject { ["id"] = "r1", ["containerUrl"] = "HTTPS://Example.COM:443" });

        Assert.Equal("https://example.com/", run.ContainerUrl);
    }

    [Fact]
    public void WebhookRequestUrlIsNormalized()
    {
        var webhook = new Webhook(new JsonObject { ["id"] = "w1", ["requestUrl"] = "https://EXAMPLE.com/a b" });

        // The space is percent-encoded, matching RFC 3986 normalization.
        Assert.Equal("https://example.com/a%20b", webhook.RequestUrl);
    }

    [Fact]
    public void ActorRunContainerUrlIsNullWhenAbsent()
    {
        var run = new ActorRun(new JsonObject { ["id"] = "r1" });

        Assert.Null(run.ContainerUrl);
    }

    [Fact]
    public void ActorRunContainerUrlThrowsOnAnInvalidAbsoluteUrl()
    {
        var run = new ActorRun(new JsonObject { ["id"] = "r1", ["containerUrl"] = "not a url" });

        Assert.Throws<FormatException>(() => run.ContainerUrl);
    }

    [Fact]
    public void RequestQueueRequestUrlIsNotNormalized()
    {
        // The reference client deliberately excludes request-queue requests from URL normalization, so the
        // raw string round-trips unchanged (e.g. an uppercase host is preserved).
        var request = new RequestQueueRequest("HTTPS://Example.COM/Path", "k");

        Assert.Equal("HTTPS://Example.COM/Path", request.Url);
    }
}
