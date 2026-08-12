# OpenIddict.DynamoDb

DynamoDB stores for [OpenIddict](https://github.com/openiddict/openiddict-core), designed around DynamoDB access patterns rather than generic CRUD.

## Install

```bash
dotnet add package ahanoff.OpenIddict.DynamoDb
```

Requires .NET 10 and OpenIddict 7.5.x.

## Register stores

```csharp
services.AddOpenIddict()
    .AddCore(options =>
    {
        options.AddDynamoDb(dynamo =>
        {
            dynamo.ApplicationsTableName = "OpenIddictApplications";
            dynamo.AuthorizationsTableName = "OpenIddictAuthorizations";
            dynamo.ScopesTableName = "OpenIddictScopes";
            dynamo.TokensTableName = "OpenIddictTokens";
        });
    });
```

The `AddDynamoDb` call registers all four stores (application, authorization, scope, token) with their OpenIddict interfaces. You must also register `IAmazonDynamoDB` in your DI container — the stores receive it through constructor injection.

```csharp
// Example: register the DynamoDB client
services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient());
```

## Configuration options

| Option | Default | Description |
| --- | --- | --- |
| `ApplicationsTableName` | `OpenIddictApplications` | Table name for application entities |
| `AuthorizationsTableName` | `OpenIddictAuthorizations` | Table name for authorization entities |
| `ScopesTableName` | `OpenIddictScopes` | Table name for scope entities |
| `TokensTableName` | `OpenIddictTokens` | Table name for token entities |
| `TokenApplicationShardCount` | `10` | Number of shards for token-by-application GSI queries |
| `MaxRedirectUrisPerApplication` | `48` | Max redirect + post-logout URIs per application; guarantees worst-case replacement fits one transaction |
| `MaxResourcesPerScope` | `48` | Max resources per scope; guarantees worst-case replacement fits one transaction |
| `PruneParallelSegments` | `10` | Parallel scan segments for `PruneAsync` |
| `PruneBatchSize` | `1000` | Max items pruned per `PruneAsync` call |

## Table creation

### Development and testing

The package includes a `OpenIddictDynamoDbTableCreator` that creates all four tables with the required key schemas and GSIs:

```csharp
await OpenIddictDynamoDbTableCreator.CreateTablesAsync(client, options, cancellationToken);
```

This creates tables with on-demand billing (`PAY_PER_REQUEST`), including GSIs for the authorization and token tables and TTL on the tokens table. It is idempotent — calling it on existing tables is safe.

The table creator is intended for local development. Production deployments should use one of the IaC modules and complete the [production checklist](docs/production.md).

## Custom entities

If you need custom properties on the models, subclass the default entities and register them:

```csharp
public class MyApplication : OpenIddictDynamoDbApplication
{
    public string? TenantId { get; set; }
}

services.AddOpenIddict()
    .AddCore(options =>
    {
        options.AddDynamoDb(dynamo =>
        {
            // configure options
        })
        .ReplaceDefaultApplicationEntity<MyApplication>();
    });
```

The same pattern applies for `ReplaceDefaultAuthorizationEntity<T>`, `ReplaceDefaultScopeEntity<T>`, and `ReplaceDefaultTokenEntity<T>`.

Public read/write properties declared on derived entities are stored in a `custom_properties` DynamoDB map as JSON. Unknown fields are retained when an entity is read and written by an older application version.

## Query and consistency behavior

Protocol point lookups use strongly consistent base-table reads. GSI-backed enumeration first discovers keys through the index and then strongly reads the base records. Bulk revocation writes an authoritative base-table revocation marker before its best-effort GSI fanout, so a newly written record hidden by GSI lag is still rejected by point lookup.

The `IQueryable` manager overloads are supported by scanning and materializing the table, then applying the query in memory. They are administrative APIs and must not be placed on request hot paths.

See [Production deployment and operations](docs/production.md) for revocation validation settings, IAM, alarms, backup/restore, migration, and capacity guidance.

## License

[Apache-2.0](LICENSE)
