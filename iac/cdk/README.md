# @ahanoff/openiddict-dynamodb-cdk

AWS CDK construct that creates DynamoDB tables for [OpenIddict.DynamoDb](https://github.com/ahanoff/OpenIddict.DynamoDb).

## Install

```bash
npm install @ahanoff/openiddict-dynamodb-cdk
```

## Usage

### Default settings

All tables use on-demand billing, point-in-time recovery, AWS-managed encryption, and deletion protection:

```ts
import { OpenIddictDynamoDb } from "@ahanoff/openiddict-dynamodb-cdk";

const openiddict = new OpenIddictDynamoDb(this, "OpenIddict");
```

### Custom table names

```ts
new OpenIddictDynamoDb(this, "OpenIddict", {
  applicationsTableProps: { tableName: "my-app-Applications" },
  tokensTableProps: { tableName: "my-app-Tokens" },
});
```

### Provisioned billing for tokens

```ts
new OpenIddictDynamoDb(this, "OpenIddict", {
  tokensTableProps: {
    billingMode: BillingMode.PROVISIONED,
    readCapacity: 20,
    writeCapacity: 10,
  },
  globalSecondaryIndexReadCapacity: 20,
  globalSecondaryIndexWriteCapacity: 10,
});
```

### Shared props with per-table override

`tableProps` applies to all tables. Per-table props override:

```ts
new OpenIddictDynamoDb(this, "OpenIddict", {
  tableProps: {
    billingMode: BillingMode.PROVISIONED,
    readCapacity: 5,
    writeCapacity: 5,
  },
  tokensTableProps: {
    readCapacity: 20,
    writeCapacity: 10,
  },
});
```

## Tables created

| Table | GSIs | TTL |
|-------|------|-----|
| `OpenIddictApplications` | — | no |
| `OpenIddictAuthorizations` | SubjectIndex, ApplicationIndex | no |
| `OpenIddictScopes` | — | no |
| `OpenIddictTokens` | SubjectAppIndex, SubjectIndex, ApplicationShardedIndex, AuthorizationIndex | yes (`ttl`) |

## Properties

| Prop | Type | Description |
|------|------|-------------|
| `tableProps` | `TableProps` (partial) | Applied to all tables |
| `applicationsTableProps` | `TableProps` (partial) | Override for applications table |
| `authorizationsTableProps` | `TableProps` (partial) | Override for authorizations table |
| `scopesTableProps` | `TableProps` (partial) | Override for scopes table |
| `tokensTableProps` | `TableProps` (partial) | Override for tokens table |
| `globalSecondaryIndexReadCapacity` | `number` | Read capacity for each GSI whose table uses provisioned billing; ignored for on-demand tables |
| `globalSecondaryIndexWriteCapacity` | `number` | Write capacity for each GSI whose table uses provisioned billing; ignored for on-demand tables |

GSIs use `KEYS_ONLY` projection; stores re-read base items consistently. Review the repository [production guide](../../docs/production.md) before deployment.
