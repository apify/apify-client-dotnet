using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>An environment variable attached to an Actor version.</summary>
public sealed class ActorEnvVar : ApifyResource
{
    private ActorEnvVar(JsonObject data)
        : base(data)
    {
    }

    /// <summary>Creates an environment variable.</summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="value">The environment variable value.</param>
    /// <param name="isSecret">Whether the value is stored as a secret.</param>
    public ActorEnvVar(string? name = null, string? value = null, bool? isSecret = null)
        : this(new JsonObject())
    {
        if (name is not null)
        {
            Name = name;
        }

        if (value is not null)
        {
            Value = value;
        }

        if (isSecret is not null)
        {
            IsSecret = isSecret;
        }
    }

    /// <summary>Wraps a raw env-var object (used when hydrating from the API).</summary>
    /// <param name="data">The raw decoded env-var object.</param>
    public static ActorEnvVar FromJsonObject(JsonObject data) => new(data);

    /// <summary>The environment variable name.</summary>
    public string? Name
    {
        get => GetString("name");
        set => SetOrRemove("name", value);
    }

    /// <summary>The environment variable value.</summary>
    public string? Value
    {
        get => GetString("value");
        set => SetOrRemove("value", value);
    }

    /// <summary>Whether the value is stored as a secret.</summary>
    public bool? IsSecret
    {
        get => GetBool("isSecret");
        set => SetOrRemove("isSecret", value);
    }

    // Honor the documented "null fields are omitted" contract: a null assignment removes the key rather
    // than writing a JSON null node.
    private void SetOrRemove(string key, string? value)
    {
        if (value is null)
        {
            ToJsonObject().Remove(key);
        }
        else
        {
            ToJsonObject()[key] = value;
        }
    }

    private void SetOrRemove(string key, bool? value)
    {
        if (value is null)
        {
            ToJsonObject().Remove(key);
        }
        else
        {
            ToJsonObject()[key] = value.Value;
        }
    }
}
