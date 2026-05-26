using System.Text.Json;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb.Tests.Helpers;

public static class TestEntities
{
    public static OpenIddictDynamoDbApplication CreateApplication(string? id = null, string? clientId = null)
    {
        var value = id ?? NewId("app");

        return new OpenIddictDynamoDbApplication
        {
            Id = value,
            ClientId = clientId ?? $"client-{value}",
            ClientSecret = "secret",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ApplicationType = "web",
            ConsentType = "explicit",
            DisplayName = $"Application {value}",
            Permissions = JsonArray(
                OpenIddictConstants.Permissions.Prefixes.Endpoint + "authorization",
                OpenIddictConstants.Permissions.Prefixes.Scope + "openid"),
            RedirectUris = JsonArray($"https://example.com/{value}/callback"),
            PostLogoutRedirectUris = JsonArray($"https://example.com/{value}/signout-callback"),
            Requirements = JsonArray("pkce"),
            Settings = "{\"setting\":\"value\"}",
            Properties = "{\"property\":\"value\"}",
            ConcurrencyToken = Guid.NewGuid().ToString()
        };
    }

    public static OpenIddictDynamoDbAuthorization CreateAuthorization(
        string? id = null,
        string? applicationId = null,
        string? subject = null)
    {
        var value = id ?? NewId("authz");

        return new OpenIddictDynamoDbAuthorization
        {
            Id = value,
            ApplicationId = applicationId ?? NewId("app"),
            Subject = subject ?? $"subject-{value}",
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            Scopes = JsonArray("openid", "profile"),
            CreationDate = DateTimeOffset.UtcNow,
            Properties = "{\"property\":\"value\"}",
            ConcurrencyToken = Guid.NewGuid().ToString()
        };
    }

    public static OpenIddictDynamoDbScope CreateScope(string? id = null, string? name = null)
    {
        var value = id ?? NewId("scope");

        return new OpenIddictDynamoDbScope
        {
            Id = value,
            Name = name ?? $"scope-{value}",
            Description = $"Description for {value}",
            DisplayName = $"Scope {value}",
            Resources = JsonArray("resource1", "resource2"),
            Properties = "{\"property\":\"value\"}",
            ConcurrencyToken = Guid.NewGuid().ToString()
        };
    }

    public static OpenIddictDynamoDbToken CreateToken(
        string? id = null,
        string? applicationId = null,
        string? authorizationId = null,
        string? subject = null,
        string? referenceId = null)
    {
        var value = id ?? NewId("token");

        return new OpenIddictDynamoDbToken
        {
            Id = value,
            ApplicationId = applicationId ?? NewId("app"),
            AuthorizationId = authorizationId ?? NewId("authz"),
            Subject = subject ?? $"subject-{value}",
            ReferenceId = referenceId ?? $"reference-{value}",
            Type = OpenIddictConstants.TokenTypeHints.AccessToken,
            Status = OpenIddictConstants.Statuses.Valid,
            CreationDate = DateTimeOffset.UtcNow,
            ExpirationDate = DateTimeOffset.UtcNow.AddHours(1),
            Payload = "payload",
            Properties = "{\"property\":\"value\"}",
            ConcurrencyToken = Guid.NewGuid().ToString()
        };
    }

    public static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    public static string JsonArray(params string[] values) => JsonSerializer.Serialize(values);
}
