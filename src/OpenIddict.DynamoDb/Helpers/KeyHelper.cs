using System.Security.Cryptography;
using System.Text;

namespace OpenIddict.DynamoDb.Helpers;

/// <summary>
/// Constructs DynamoDB primary keys, sort keys, and GSI keys for all OpenIddict entity types.
/// </summary>
public static class KeyHelper
{
    // Null sentinel for composite sort keys (DynamoDB cannot store null in keys)
    public const string NullSentinel = "_NULL_";

    // --- Applications ---
    public static string ApplicationPk(string id) => $"APP#{id}";
    public static string ApplicationSk => "#META";
    public static string ClientIdPk(string clientId) => $"CLIENTID#{clientId}";
    public static string ClientIdSk => "#LOOKUP";
    public static string RedirectPk(string uri) => $"REDIRECT#{Sha256(uri)}";
    public static string RedirectSk(string appId) => $"APP#{appId}";
    public static string PostLogoutRedirectPk(string uri) => $"POSTLOGOUT#{Sha256(uri)}";
    public static string PostLogoutRedirectSk(string appId) => $"APP#{appId}";

    // --- Scopes ---
    public static string ScopePk(string id) => $"SCOPE#{id}";
    public static string ScopeSk => "#META";
    public static string ScopeNamePk(string name) => $"NAME#{name}";
    public static string ScopeNameSk => "#LOOKUP";
    public static string ScopeResourcePk(string resource) => $"RESOURCE#{resource}";
    public static string ScopeResourceSk(string scopeId) => $"SCOPE#{scopeId}";

    // --- Authorizations ---
    public static string AuthorizationPk(string id) => $"AUTHZ#{id}";
    public static string AuthorizationSk => "#META";
    // GSI1: Subject index
    public static string AuthorizationSubjectIndexPk(string subject) => $"SUBJ#{subject}";
    public static string AuthorizationSubjectIndexSk(string appId, string authzId) => $"APP#{appId}#AUTHZ#{authzId}";
    // GSI2: Application index
    public static string AuthorizationApplicationIndexPk(string appId) => $"APP#{appId}";
    public static string AuthorizationApplicationIndexSk(string authzId) => $"AUTHZ#{authzId}";

    // --- Tokens ---
    public static string TokenPk(string id) => $"TOKEN#{id}";
    public static string TokenSk => "#META";
    public static string ReferenceIdPk(string referenceId) => $"REF#{referenceId}";
    public static string ReferenceIdSk => "#LOOKUP";
    // GSI1: Subject+Application index with composite SK
    public static string TokenSubjectAppIndexPk(string subject, string appId) => $"SUBJAPP#{subject}#{appId}";
    public static string TokenSubjectAppIndexSk(string? status, string? type, string tokenId) =>
        $"STATUS#{status ?? NullSentinel}#TYPE#{type ?? NullSentinel}#TOKEN#{tokenId}";
    // GSI2: Subject index
    public static string TokenSubjectIndexPk(string subject) => $"SUBJ#{subject}";
    public static string TokenSubjectIndexSk(string tokenId) => $"TOKEN#{tokenId}";
    // GSI3: Application sharded index
    public static string TokenApplicationShardedIndexPk(string appId, string tokenId, int shardCount = 10)
    {
        var shard = Math.Abs(tokenId.GetHashCode()) % shardCount;
        return $"APP#{appId}#S#{shard}";
    }
    public static string TokenApplicationShardedIndexSk(string? status, string? type, string tokenId) =>
        $"STATUS#{status ?? NullSentinel}#TYPE#{type ?? NullSentinel}#TOKEN#{tokenId}";
    // GSI4: Authorization index
    public static string TokenAuthorizationIndexPk(string authorizationId) => $"AUTHZ#{authorizationId}";
    public static string TokenAuthorizationIndexSk(string tokenId) => $"TOKEN#{tokenId}";

    // --- Utility ---
    public static string Sha256(string input)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Extracts the entity ID from a PK like "APP#123" → "123"
    /// </summary>
    public static string ExtractId(string pk, string prefix)
    {
        if (pk.StartsWith(prefix))
            return pk[prefix.Length..];
        return pk;
    }
}
