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
}
