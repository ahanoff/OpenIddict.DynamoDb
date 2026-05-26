# Contributing to OpenIddict.DynamoDb

## Prerequisites

- .NET 10 SDK (version matching `global.json`)
- Podman

## Running tests

Tests are integration tests against DynamoDB Local via [Testcontainers](https://dotnet.testcontainers.org/) and Podman.

```bash
systemctl --user start podman.socket
dotnet test
```

Testcontainers reads Podman's socket from `~/.testcontainers.properties` and starts `amazon/dynamodb-local:latest` automatically.
