using System.Security.Cryptography;
using System.Text;
using System.Buffers.Binary;

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
    public static string ClientIdPk(string clientId) => $"CLIENTID#{Sha256(clientId)}";
    public static string ClientIdSk => "#LOOKUP";
    public static string RedirectPk(string uri) => $"REDIRECT#{Sha256(uri)}";
    public static string RedirectSk(string appId) => $"APP#{appId}";
    public static string PostLogoutRedirectPk(string uri) => $"POSTLOGOUT#{Sha256(uri)}";
    public static string PostLogoutRedirectSk(string appId) => $"APP#{appId}";

    // --- Scopes ---
    public static string ScopePk(string id) => $"SCOPE#{id}";
    public static string ScopeSk => "#META";
    public static string ScopeNamePk(string name) => $"NAME#{Sha256(name)}";
    public static string ScopeNameSk => "#LOOKUP";
    public static string ScopeResourcePk(string resource) => $"RESOURCE#{Sha256(resource)}";
    public static string ScopeResourceSk(string scopeId) => $"SCOPE#{scopeId}";

    // --- Authorizations ---
    public static string AuthorizationPk(string id) => $"AUTHZ#{id}";
    public static string AuthorizationSk => "#META";
    // GSI1: Subject index
    public static string AuthorizationSubjectIndexPk(string subject) => $"SUBJ#{Sha256(subject)}";
    public static string AuthorizationSubjectIndexSk(string appId, string authzId) => $"APP#{Sha256(appId)}#AUTHZ#{authzId}";
    // GSI2: Application index
    public static string AuthorizationApplicationIndexPk(string appId) => $"APP#{Sha256(appId)}";
    public static string AuthorizationApplicationIndexSk(string authzId) => $"AUTHZ#{authzId}";

    // --- Tokens ---
    public static string TokenPk(string id) => $"TOKEN#{id}";
    public static string TokenSk => "#META";
    public static string ReferenceIdPk(string referenceId) => $"REF#{Sha256(referenceId)}";
    public static string ReferenceIdSk => "#LOOKUP";
    // GSI1: Subject+Application index with composite SK
    public static string TokenSubjectAppIndexPk(string subject, string appId) => $"SUBJAPP#{Sha256(subject)}#{Sha256(appId)}";
    public static string TokenSubjectAppIndexSk(string? status, string? type, string tokenId) =>
        $"STATUS#{status ?? NullSentinel}#TYPE#{type ?? NullSentinel}#TOKEN#{tokenId}";
    // GSI2: Subject index
    public static string TokenSubjectIndexPk(string subject) => $"SUBJ#{Sha256(subject)}";
    public static string TokenSubjectIndexSk(string tokenId) => $"TOKEN#{tokenId}";
    // GSI3: Application sharded index
    public static string TokenApplicationShardedIndexPk(string appId, string tokenId, int shardCount = 10)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(shardCount, 1);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(tokenId));
        var shard = BinaryPrimitives.ReadUInt32BigEndian(hash) % (uint)shardCount;
        return TokenApplicationShardIndexPk(appId, (int)shard);
    }
    public static string TokenApplicationShardIndexPk(string appId, int shard)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shard);
        return $"APP#{Sha256(appId)}#S#{shard}";
    }
    public static string TokenApplicationShardedIndexSk(string? status, string? type, string tokenId) =>
        $"STATUS#{status ?? NullSentinel}#TYPE#{type ?? NullSentinel}#TOKEN#{tokenId}";
    // GSI4: Authorization index
    public static string TokenAuthorizationIndexPk(string authorizationId) => $"AUTHZ#{Sha256(authorizationId)}";
    public static string TokenAuthorizationIndexSk(string tokenId) => $"TOKEN#{tokenId}";
    public static string AuthorizationTokenCountPk(string authorizationId) => $"AUTHZTOKENCOUNT#{Sha256(authorizationId)}";
    public static string AuthorizationTokenCountSk => "#COUNT";

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
