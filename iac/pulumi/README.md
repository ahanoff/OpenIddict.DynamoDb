# @ahanoff/openiddict-dynamodb-pulumi

Pulumi component that creates DynamoDB tables for [OpenIddict.DynamoDb](https://github.com/ahanoff/OpenIddict.DynamoDb).

## Install

```bash
npm install @ahanoff/openiddict-dynamodb-pulumi
```

## Usage

### Default settings

All tables use on-demand billing:

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
