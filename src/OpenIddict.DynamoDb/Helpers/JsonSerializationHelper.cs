using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace OpenIddict.DynamoDb.Helpers;

/// <summary>
/// Serializes and deserializes complex OpenIddict properties to/from JSON strings for DynamoDB storage.
/// </summary>
public static class JsonSerializationHelper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // --- String array (ImmutableArray<string>) ---
    public static string? SerializeArray(ImmutableArray<string> values)
    {
        if (values.IsDefaultOrEmpty) return null;
        return JsonSerializer.Serialize(values.ToList(), Options);
    }

    public static ImmutableArray<string> DeserializeArray(string? json)
    {
        if (string.IsNullOrEmpty(json)) return ImmutableArray<string>.Empty;
        var list = JsonSerializer.Deserialize<List<string>>(json, Options);
        return list?.ToImmutableArray() ?? ImmutableArray<string>.Empty;
    }

    // --- String dictionary (ImmutableDictionary<string, string>) ---
    public static string? SerializeDictionary(ImmutableDictionary<string, string>? values)
    {
        if (values is null || values.IsEmpty) return null;
        return JsonSerializer.Serialize(values.ToDictionary(k => k.Key, v => v.Value), Options);
    }

    public static ImmutableDictionary<string, string> DeserializeStringDictionary(string? json)
    {
        if (string.IsNullOrEmpty(json)) return ImmutableDictionary<string, string>.Empty;
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options);
        return dict?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty;
    }

    // --- CultureInfo display names (ImmutableDictionary<CultureInfo, string>) ---
    public static string? SerializeCultureInfoDictionary(ImmutableDictionary<CultureInfo, string>? values)
    {
        if (values is null || values.IsEmpty) return null;
        var dict = values.ToDictionary(k => k.Key.Name, v => v.Value);
        return JsonSerializer.Serialize(dict, Options);
    }

    public static ImmutableDictionary<CultureInfo, string> DeserializeCultureInfoDictionary(string? json)
    {
        if (string.IsNullOrEmpty(json)) return ImmutableDictionary<CultureInfo, string>.Empty;
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options);
        if (dict is null) return ImmutableDictionary<CultureInfo, string>.Empty;
        return dict
            .Where(kvp => !string.IsNullOrEmpty(kvp.Key))
            .ToImmutableDictionary(
                kvp => new CultureInfo(kvp.Key),
                kvp => kvp.Value);
    }

    // --- Properties (ImmutableDictionary<string, JsonElement>) ---
    public static string? SerializeProperties(ImmutableDictionary<string, JsonElement>? values)
    {
        if (values is null || values.IsEmpty) return null;
        return JsonSerializer.Serialize(values.ToDictionary(k => k.Key, v => v.Value), Options);
    }

    public static ImmutableDictionary<string, JsonElement> DeserializeProperties(string? json)
    {
        if (string.IsNullOrEmpty(json)) return ImmutableDictionary<string, JsonElement>.Empty;
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, Options);
        return dict?.ToImmutableDictionary() ?? ImmutableDictionary<string, JsonElement>.Empty;
    }

    // --- JsonWebKeySet ---
    public static string? SerializeJsonWebKeySet(JsonWebKeySet? set)
    {
        if (set is null) return null;
        return JsonSerializer.Serialize(set, Options);
    }

    public static JsonWebKeySet? DeserializeJsonWebKeySet(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<JsonWebKeySet>(json, Options);
    }
}
