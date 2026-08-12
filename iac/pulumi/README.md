# @ahanoff/openiddict-dynamodb-pulumi

Pulumi component that creates DynamoDB tables for [OpenIddict.DynamoDb](https://github.com/ahanoff/OpenIddict.DynamoDb).

## Install

```bash
npm install @ahanoff/openiddict-dynamodb-pulumi
```

## Usage

### Default settings

All tables use on-demand billing, point-in-time recovery, server-side encryption, and deletion protection:

```ts
import { OpenIddictDynamoDb } from "@ahanoff/openiddict-dynamodb-pulumi";

const openiddict = new OpenIddictDynamoDb("openiddict");
```

### Custom table names

```ts
new OpenIddictDynamoDb("openiddict", {
  applicationsTableArgs: { name: "my-app-Applications" },
  tokensTableArgs: { name: "my-app-Tokens" },
});
```

### Provisioned billing for tokens

```ts
new OpenIddictDynamoDb("openiddict", {
  tokensTableArgs: {
    billingMode: "PROVISIONED",
    readCapacity: 20,
    writeCapacity: 10,
  },
  globalSecondaryIndexReadCapacity: 20,
  globalSecondaryIndexWriteCapacity: 10,
});
```

### Shared args with per-table override

`tableArgs` applies to all tables. Per-table args override:

```ts
new OpenIddictDynamoDb("openiddict", {
  tableArgs: {
    billingMode: "PROVISIONED",
    readCapacity: 5,
    writeCapacity: 5,
  },
  tokensTableArgs: {
    readCapacity: 20,
    writeCapacity: 10,
  },
});
```

### Tags

```ts
new OpenIddictDynamoDb("openiddict", {
  tags: { Environment: "production" },
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
| `tags` | `Record<string, string>` | Tags applied to all tables |
| `tableArgs` | `TableArgs` (partial) | Applied to all tables |
| `applicationsTableArgs` | `TableArgs` (partial) | Override for applications table |
| `authorizationsTableArgs` | `TableArgs` (partial) | Override for authorizations table |
| `scopesTableArgs` | `TableArgs` (partial) | Override for scopes table |
| `tokensTableArgs` | `TableArgs` (partial) | Override for tokens table |
| `globalSecondaryIndexReadCapacity` | `Input<number>` | Read capacity for each GSI whose table uses provisioned billing; ignored for on-demand tables |
| `globalSecondaryIndexWriteCapacity` | `Input<number>` | Write capacity for each GSI whose table uses provisioned billing; ignored for on-demand tables |

GSIs use `KEYS_ONLY` projection; stores re-read base items consistently. Review the repository [production guide](../../docs/production.md) before deployment.
