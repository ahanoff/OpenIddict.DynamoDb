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

## Releasing

1. Create a GitHub Release with a tag matching the desired version (e.g. `0.1.0`)
2. The release workflow packs the NuGet package and publishes it to nuget.org
3. Requires `NUGET_API_KEY` secret in repository settings
