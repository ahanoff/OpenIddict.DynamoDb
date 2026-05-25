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
    /// Default: 80 (leaves room for metadata + clientId sentinel).
    /// </summary>
    public int MaxRedirectUrisPerApplication { get; set; } = 80;

    /// <summary>
    /// Maximum number of resources per scope.
    /// Limited by DynamoDB TransactWriteItems 100-item limit.
    /// Default: 95.
    /// </summary>
    public int MaxResourcesPerScope { get; set; } = 95;

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
}
