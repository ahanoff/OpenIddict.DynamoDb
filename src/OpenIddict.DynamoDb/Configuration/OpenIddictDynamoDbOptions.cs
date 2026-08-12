namespace OpenIddict.DynamoDb;

public class OpenIddictDynamoDbOptions
{
    /// <summary>
    /// Table name for OAuth applications.
    /// </summary>
    public string ApplicationsTableName { get; set; } = "OpenIddictApplications";

    /// <summary>
    /// Table name for OAuth authorizations.
    /// </summary>
    public string AuthorizationsTableName { get; set; } = "OpenIddictAuthorizations";

    /// <summary>
    /// Table name for OAuth scopes.
    /// </summary>
    public string ScopesTableName { get; set; } = "OpenIddictScopes";

    /// <summary>
    /// Table name for OAuth tokens.
    /// </summary>
    public string TokensTableName { get; set; } = "OpenIddictTokens";

    /// <summary>
    /// Number of shards for token-by-application queries.
    /// Must be a positive integer. Default: 10.
    /// </summary>
    public int TokenApplicationShardCount { get; set; } = 10;

    /// <summary>
    /// Maximum number of redirect + post-logout redirect URIs per application.
    /// Limited by DynamoDB TransactWriteItems 100-item limit.
    /// Default: 48 (supports a complete replacement plus a client-id rename in one transaction).
    /// </summary>
    public int MaxRedirectUrisPerApplication { get; set; } = 48;

    /// <summary>
    /// Maximum number of resources per scope.
    /// Limited by DynamoDB TransactWriteItems 100-item limit.
    /// Default: 48 (supports a complete replacement plus a name change in one transaction).
    /// </summary>
    public int MaxResourcesPerScope { get; set; } = 48;

    /// <summary>
    /// Number of segments for parallel scan during PruneAsync.
    /// Default: 10.
    /// </summary>
    public int PruneParallelSegments { get; set; } = 10;

    /// <summary>
    /// Maximum number of items to prune per PruneAsync call.
    /// Default: 1000 (matches OpenIddict EF Core pattern).
    /// </summary>
    public int PruneBatchSize { get; set; } = 1000;

    internal void Validate()
    {
        ValidateTableName(ApplicationsTableName, nameof(ApplicationsTableName));
        ValidateTableName(AuthorizationsTableName, nameof(AuthorizationsTableName));
        ValidateTableName(ScopesTableName, nameof(ScopesTableName));
        ValidateTableName(TokensTableName, nameof(TokensTableName));

        ArgumentOutOfRangeException.ThrowIfLessThan(TokenApplicationShardCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(TokenApplicationShardCount, 100);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxRedirectUrisPerApplication, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxRedirectUrisPerApplication, 48);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxResourcesPerScope, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxResourcesPerScope, 48);
        ArgumentOutOfRangeException.ThrowIfLessThan(PruneParallelSegments, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(PruneParallelSegments, 100);
        ArgumentOutOfRangeException.ThrowIfLessThan(PruneBatchSize, 1);
    }

    private static void ValidateTableName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("DynamoDB table names cannot be empty.", parameterName);
        }
    }
}
