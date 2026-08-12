# Production deployment and operations

OpenIddict.DynamoDb stores security state. A green local test suite is not a production qualification; complete this guide in an AWS staging account before serving credentials.

## Design article reconciliation

The original design article predates three production hardening changes. DynamoDB TTL now applies only to reference lookup items, not token metadata, because out-of-band TTL deletion cannot decrement the authorization token counter. Token metadata is removed by scheduled pruning. Store lookup methods intentionally return expired entries to match the OpenIddict 7.5 contract; OpenIddict server/validation handlers enforce the stored UTC expiration date independently of TTL. Finally, user- and client-derived key segments are SHA-256 hashed, so the article's raw key examples must not be used for migrations or diagnostics.

## Required application configuration

- Keep all OpenIddict packages on the same supported 7.5.x version and test every upgrade against issuance, refresh, introspection, revocation, logout, and pruning flows.
- Register one long-lived `IAmazonDynamoDB` client through dependency injection. Configure the AWS SDK retry mode, operation timeout, and HTTP timeout for the service latency budget. Do not create a client per request.
- Keep `TokenApplicationShardCount` identical on every process. The shard algorithm is stable, but changing the count requires a migration that rewrites every token's application-shard key.
- Keep host clocks synchronized. Revocation markers compare UTC creation times against a UTC revocation cutoff.
- For APIs that locally validate self-contained access tokens, enable both `EnableTokenEntryValidation()` and `EnableAuthorizationEntryValidation()` when immediate database revocation is required. OpenIddict intentionally does not perform these reads by default. Remote APIs should use introspection when they cannot access the store.
- Token store lookup methods return expired entries as required by the OpenIddict store contract. Expiration is rejected by OpenIddict's server/validation handlers using the stored UTC expiration date; DynamoDB TTL is never the validation authority.
- Never put `CountAsync`, unfiltered `ListAsync`, pruning, or `IQueryable` manager overloads on OAuth request paths. These APIs scan and paginate the complete table; `IQueryable` expressions run in memory after materialization.

## Schema and deployment rules

All four tables require string `pk` and `sk` keys. The authorization table requires `SubjectIndex` (`gsi1_pk`, `gsi1_sk`) and `ApplicationIndex` (`gsi2_pk`, `gsi2_sk`). The token table requires `SubjectAppIndex`, `SubjectIndex`, `ApplicationShardedIndex`, and `AuthorizationIndex` using `gsi1` through `gsi4`. Every GSI uses `KEYS_ONLY`; the token table enables TTL on `ttl`.

Deploy additive table/index changes first, wait until every table and index is `ACTIVE`, then deploy code that uses them. Roll code back before removing an index. Never change key encoding or shard count in place. Create new tables, dual-write/backfill with an audited migration, verify counts and sampled point lookups, cut traffic over, and retain the old tables until the rollback window closes.

The stable hashed key format, authorization token counters, revocation markers, and `KEYS_ONLY` projections introduced after beta1 are schema changes. Existing beta data must be migrated or recreated before deploying this version. Backfill an exact token counter, including zero, for every legacy authorization before enabling writes: issuance, deletion, and pruning fail closed when the counter is absent. Remove `ttl` from token metadata during migration: metadata is deliberately retained for store-driven pruning so its authorization counter can be decremented; TTL applies only to reference lookup items.

DynamoDB rejects an oversized transaction atomically, but applications and scopes deliberately cap collection cardinality at 48 so a worst-case replacement fits the 100-action transaction limit. Reject unusually large payloads before calling the manager and alert on DynamoDB validation errors or 400 KB item-limit failures.

## Resilience and recovery

The IaC modules enable point-in-time recovery, deletion protection, and encryption by default. Use a customer-managed KMS key when policy requires customer-controlled rotation or grants. Tag every table with owner, environment, data classification, and cost-allocation fields.

Run a restore drill before launch and at least quarterly. PITR restores to a new table; the drill must validate the four restored tables, every index, TTL, application configuration, token shard count, and a controlled cutover/rollback. Define recovery-time and recovery-point objectives. For regional disaster recovery, test an explicit cross-Region restore or Global Tables design and account for its eventual cross-Region replication semantics.

For provisioned billing, configure Application Auto Scaling independently for each table and every GSI. Size transactional write paths for twice the normal write-capacity consumption. On-demand mode still needs account/table quota reviews and load tests.

## Monitoring

Create per-table and per-index CloudWatch dashboards and alarms for:

- `ReadThrottleEvents`, `WriteThrottleEvents`, and `ThrottledRequests` greater than zero;
- `SystemErrors` greater than zero and sustained `SuccessfulRequestLatency` above the service budget;
- `TransactionConflict` and unexpected `ConditionalCheckFailedRequests` increases;
- consumed read/write capacity approaching provisioned capacity or on-demand account limits;
- `TimeToLiveDeletedItemCount` disappearance when expired-token volume is expected.

Also record application metrics for DynamoDB operation name, attempt count, terminal exception category, cancellation, bulk-revocation candidate count, and prune conflicts. Never attach client secrets, token payloads, reference identifiers, raw subject identifiers, or serialized entity properties to logs or traces.

Contributor Insights can locate hot/throttled keys, but it publishes primary-key values into its own CloudWatch encryption context. Keys in this library are hashed where they contain user/client input; still complete a privacy review before enabling it. Use throttled-keys-only mode when it satisfies the diagnostic requirement.

## Runtime IAM policy

Grant the server role data-plane access only to the four configured tables and their indexes. Substitute the real Region, account, and table names:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "dynamodb:BatchGetItem",
        "dynamodb:DeleteItem",
        "dynamodb:DescribeTable",
        "dynamodb:GetItem",
        "dynamodb:PutItem",
        "dynamodb:Query",
        "dynamodb:Scan",
        "dynamodb:TransactWriteItems",
        "dynamodb:UpdateItem"
      ],
      "Resource": [
        "arn:aws:dynamodb:REGION:ACCOUNT:table/OpenIddictApplications",
        "arn:aws:dynamodb:REGION:ACCOUNT:table/OpenIddictAuthorizations",
        "arn:aws:dynamodb:REGION:ACCOUNT:table/OpenIddictAuthorizations/index/*",
        "arn:aws:dynamodb:REGION:ACCOUNT:table/OpenIddictScopes",
        "arn:aws:dynamodb:REGION:ACCOUNT:table/OpenIddictTokens",
        "arn:aws:dynamodb:REGION:ACCOUNT:table/OpenIddictTokens/index/*"
      ]
    }
  ]
}
```

If a customer-managed key is used, add only the required KMS grant. Keep table creation, backup, restore, deletion, Contributor Insights, and alarm-management permissions out of the runtime role.

## Pre-launch verification

1. Run the repository tests against DynamoDB Local and a disposable real AWS table set. Schedule OpenIddict token pruning; TTL removes expired reference lookups but deliberately does not replace metadata pruning.
2. Inject throttling, transaction conflicts, request timeout after send, cancellation, and delayed GSI visibility. Verify issuance fails closed and revocation remains effective.
3. Load test the largest client, subject, authorization, redirect-URI set, scope-resource set, and configured shard count. Record p50/p95/p99 latency and consumed capacity.
4. Through a complete OpenIddict server or validation flow, prove an expired token is rejected before TTL deletion, an immediately revoked token is rejected despite GSI lag, and a refreshed record survives concurrent pruning.
5. Exercise backup restore, credential rotation, index rollout/rollback, application deployment rollback, and regional recovery.

AWS references: [transactions and idempotency](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/transaction-apis.html), [PITR](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Point-in-time-recovery.html), [CloudWatch metrics](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Monitoring-metrics-with-Amazon-CloudWatch.html), [Contributor Insights](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/contributorinsights.html). OpenIddict references: [token storage and validation](https://documentation.openiddict.com/configuration/token-storage.html), [authorization storage and validation](https://documentation.openiddict.com/configuration/authorization-storage).
