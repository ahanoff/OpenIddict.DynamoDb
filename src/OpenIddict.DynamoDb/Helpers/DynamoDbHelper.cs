using Amazon.DynamoDBv2.Model;
using OpenIddict.DynamoDb.Models;
using System.Reflection;
using System.Text.Json;

namespace OpenIddict.DynamoDb.Helpers;

public static class DynamoDbHelper
{
    // --- To DynamoDB ---
    public static Dictionary<string, AttributeValue> ToAttributes(OpenIddictDynamoDbApplication application)
    {
        var attributes = new Dictionary<string, AttributeValue>();

        Add(attributes, "id", application.Id);
        Add(attributes, "client_id", application.ClientId);
        Add(attributes, "client_secret", application.ClientSecret);
        Add(attributes, "client_type", application.ClientType);
        Add(attributes, "application_type", application.ApplicationType);
        Add(attributes, "consent_type", application.ConsentType);
        Add(attributes, "display_name", application.DisplayName);
        Add(attributes, "display_names", application.DisplayNames);
        Add(attributes, "permissions", application.Permissions);
        Add(attributes, "redirect_uris", application.RedirectUris);
        Add(attributes, "post_logout_redirect_uris", application.PostLogoutRedirectUris);
        Add(attributes, "requirements", application.Requirements);
        Add(attributes, "settings", application.Settings);
        Add(attributes, "json_web_key_set", application.JsonWebKeySet);
        Add(attributes, "properties", application.Properties);
        Add(attributes, "concurrency_token", application.ConcurrencyToken);
        AddCustomProperties(attributes, application, typeof(OpenIddictDynamoDbApplication));

        return attributes;
    }

    public static Dictionary<string, AttributeValue> ToAttributes(OpenIddictDynamoDbAuthorization authorization)
    {
        var attributes = new Dictionary<string, AttributeValue>();

        Add(attributes, "id", authorization.Id);
        Add(attributes, "application_id", authorization.ApplicationId);
        Add(attributes, "subject", authorization.Subject);
        Add(attributes, "status", authorization.Status);
        Add(attributes, "type", authorization.Type);
        Add(attributes, "scopes", authorization.Scopes);
        Add(attributes, "creation_date", authorization.CreationDate);
        Add(attributes, "properties", authorization.Properties);
        Add(attributes, "concurrency_token", authorization.ConcurrencyToken);
        AddCustomProperties(attributes, authorization, typeof(OpenIddictDynamoDbAuthorization));

        return attributes;
    }

    public static Dictionary<string, AttributeValue> ToAttributes(OpenIddictDynamoDbScope scope)
    {
        var attributes = new Dictionary<string, AttributeValue>();

        Add(attributes, "id", scope.Id);
        Add(attributes, "name", scope.Name);
        Add(attributes, "description", scope.Description);
        Add(attributes, "display_name", scope.DisplayName);
        Add(attributes, "descriptions", scope.Descriptions);
        Add(attributes, "display_names", scope.DisplayNames);
        Add(attributes, "resources", scope.Resources);
        Add(attributes, "properties", scope.Properties);
        Add(attributes, "concurrency_token", scope.ConcurrencyToken);
        AddCustomProperties(attributes, scope, typeof(OpenIddictDynamoDbScope));

        return attributes;
    }

    public static Dictionary<string, AttributeValue> ToAttributes(OpenIddictDynamoDbToken token)
    {
        var attributes = new Dictionary<string, AttributeValue>();

        Add(attributes, "id", token.Id);
        Add(attributes, "application_id", token.ApplicationId);
        Add(attributes, "authorization_id", token.AuthorizationId);
        Add(attributes, "subject", token.Subject);
        Add(attributes, "status", token.Status);
        Add(attributes, "type", token.Type);
        Add(attributes, "reference_id", token.ReferenceId);
        Add(attributes, "payload", token.Payload);
        Add(attributes, "creation_date", token.CreationDate);
        Add(attributes, "expiration_date", token.ExpirationDate);
        Add(attributes, "redemption_date", token.RedemptionDate);
        Add(attributes, "properties", token.Properties);
        Add(attributes, "concurrency_token", token.ConcurrencyToken);
        Add(attributes, "ttl", token.TTL);
        AddCustomProperties(attributes, token, typeof(OpenIddictDynamoDbToken));

        return attributes;
    }

    // --- From DynamoDB ---
    public static TApplication ToApplication<TApplication>(Dictionary<string, AttributeValue> attributes)
        where TApplication : OpenIddictDynamoDbApplication, new()
    {
        var application = new TApplication
        {
            Id = GetString(attributes, "id")!,
            ClientId = GetString(attributes, "client_id"),
            ClientSecret = GetString(attributes, "client_secret"),
            ClientType = GetString(attributes, "client_type"),
            ApplicationType = GetString(attributes, "application_type"),
            ConsentType = GetString(attributes, "consent_type"),
            DisplayName = GetString(attributes, "display_name"),
            DisplayNames = GetString(attributes, "display_names"),
            Permissions = GetString(attributes, "permissions"),
            RedirectUris = GetString(attributes, "redirect_uris"),
            PostLogoutRedirectUris = GetString(attributes, "post_logout_redirect_uris"),
            Requirements = GetString(attributes, "requirements"),
            Settings = GetString(attributes, "settings"),
            JsonWebKeySet = GetString(attributes, "json_web_key_set"),
            Properties = GetString(attributes, "properties"),
            ConcurrencyToken = GetString(attributes, "concurrency_token")
        };
        ApplyCustomProperties(application, attributes, typeof(OpenIddictDynamoDbApplication));
        return application;
    }

    public static TAuthorization ToAuthorization<TAuthorization>(Dictionary<string, AttributeValue> attributes)
        where TAuthorization : OpenIddictDynamoDbAuthorization, new()
    {
        var authorization = new TAuthorization
        {
            Id = GetString(attributes, "id")!,
            ApplicationId = GetString(attributes, "application_id"),
            Subject = GetString(attributes, "subject"),
            Status = GetString(attributes, "status"),
            Type = GetString(attributes, "type"),
            Scopes = GetString(attributes, "scopes"),
            CreationDate = GetDateTimeOffset(attributes, "creation_date"),
            Properties = GetString(attributes, "properties"),
            ConcurrencyToken = GetString(attributes, "concurrency_token")
        };
        ApplyCustomProperties(authorization, attributes, typeof(OpenIddictDynamoDbAuthorization));
        return authorization;
    }

    public static TScope ToScope<TScope>(Dictionary<string, AttributeValue> attributes)
        where TScope : OpenIddictDynamoDbScope, new()
    {
        var scope = new TScope
        {
            Id = GetString(attributes, "id")!,
            Name = GetString(attributes, "name"),
            Description = GetString(attributes, "description"),
            DisplayName = GetString(attributes, "display_name"),
            Descriptions = GetString(attributes, "descriptions"),
            DisplayNames = GetString(attributes, "display_names"),
            Resources = GetString(attributes, "resources"),
            Properties = GetString(attributes, "properties"),
            ConcurrencyToken = GetString(attributes, "concurrency_token")
        };
        ApplyCustomProperties(scope, attributes, typeof(OpenIddictDynamoDbScope));
        return scope;
    }

    public static TToken ToToken<TToken>(Dictionary<string, AttributeValue> attributes)
        where TToken : OpenIddictDynamoDbToken, new()
    {
        var token = new TToken
        {
            Id = GetString(attributes, "id")!,
            ApplicationId = GetString(attributes, "application_id"),
            AuthorizationId = GetString(attributes, "authorization_id"),
            Subject = GetString(attributes, "subject"),
            Status = GetString(attributes, "status"),
            Type = GetString(attributes, "type"),
            ReferenceId = GetString(attributes, "reference_id"),
            Payload = GetString(attributes, "payload"),
            CreationDate = GetDateTimeOffset(attributes, "creation_date"),
            ExpirationDate = GetDateTimeOffset(attributes, "expiration_date"),
            RedemptionDate = GetDateTimeOffset(attributes, "redemption_date"),
            Properties = GetString(attributes, "properties"),
            ConcurrencyToken = GetString(attributes, "concurrency_token"),
            TTL = GetLong(attributes, "ttl")
        };
        ApplyCustomProperties(token, attributes, typeof(OpenIddictDynamoDbToken));
        return token;
    }

    // --- Attribute value helpers ---
    public static AttributeValue ToAttr(string? value) => new() { S = value ?? string.Empty };

    public static AttributeValue ToAttr(long? value) => value.HasValue ? new() { N = value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) } : new() { NULL = true };

    public static AttributeValue ToAttr(DateTimeOffset? value) => value.HasValue ? new() { S = value.Value.ToUniversalTime().ToString("o") } : new() { NULL = true };

    public static string? GetString(Dictionary<string, AttributeValue> attrs, string key)
        => attrs.TryGetValue(key, out var av) && av.NULL is not true ? av.S : null;

    public static long? GetLong(Dictionary<string, AttributeValue> attrs, string key)
        => attrs.TryGetValue(key, out var av) && av.NULL is not true &&
           long.TryParse(av.N, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : null;

    public static DateTimeOffset? GetDateTimeOffset(Dictionary<string, AttributeValue> attrs, string key)
        => attrs.TryGetValue(key, out var av) && av.NULL is not true &&
           DateTimeOffset.TryParse(av.S, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var val) ? val : null;

    private static void Add(Dictionary<string, AttributeValue> attributes, string key, string? value)
    {
        if (value is not null)
        {
            attributes[key] = ToAttr(value);
        }
    }

    private static void Add(Dictionary<string, AttributeValue> attributes, string key, long? value)
    {
        if (value.HasValue)
        {
            attributes[key] = ToAttr(value);
        }
    }

    private static void Add(Dictionary<string, AttributeValue> attributes, string key, DateTimeOffset? value)
    {
        if (value.HasValue)
        {
            attributes[key] = ToAttr(value);
        }
    }

    private static void AddCustomProperties(
        Dictionary<string, AttributeValue> attributes,
        IOpenIddictDynamoDbCustomEntity entity,
        Type baseType)
    {
        var values = new Dictionary<string, string>(entity.CustomPropertyData, StringComparer.Ordinal);
        foreach (var property in GetCustomProperties(entity.GetType(), baseType))
        {
            values[property.Name] = JsonSerializer.Serialize(property.GetValue(entity), property.PropertyType);
        }

        if (values.Count > 0)
        {
            attributes["custom_properties"] = new AttributeValue
            {
                M = values.ToDictionary(pair => pair.Key, pair => new AttributeValue { S = pair.Value }, StringComparer.Ordinal)
            };
        }
    }

    private static void ApplyCustomProperties(
        IOpenIddictDynamoDbCustomEntity entity,
        Dictionary<string, AttributeValue> attributes,
        Type baseType)
    {
        if (!attributes.TryGetValue("custom_properties", out var value) || value.M is null)
        {
            return;
        }

        foreach (var pair in value.M.Where(pair => pair.Value.S is not null))
        {
            entity.CustomPropertyData[pair.Key] = pair.Value.S;
        }

        var properties = GetCustomProperties(entity.GetType(), baseType).ToDictionary(property => property.Name, StringComparer.Ordinal);
        foreach (var pair in entity.CustomPropertyData)
        {
            if (properties.TryGetValue(pair.Key, out var property))
            {
                property.SetValue(entity, JsonSerializer.Deserialize(pair.Value, property.PropertyType));
            }
        }
    }

    private static IEnumerable<PropertyInfo> GetCustomProperties(Type entityType, Type baseType)
        => entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            .Where(property => property.DeclaringType is not null && property.DeclaringType != baseType);
}
