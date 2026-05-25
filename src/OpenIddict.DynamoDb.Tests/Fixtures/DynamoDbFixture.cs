using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Testcontainers.DynamoDb;

namespace OpenIddict.DynamoDb.Tests.Fixtures;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DynamoDbCollection : ICollectionFixture<DynamoDbFixture>
{
    public const string Name = "DynamoDB Local";
}

public sealed class DynamoDbFixture : IAsyncLifetime
{
    private readonly DynamoDbContainer? _container;

    public DynamoDbFixture()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL")))
        {
            _container = new DynamoDbBuilder()
                .WithImage("amazon/dynamodb-local:latest")
                .Build();
        }

        var suffix = Guid.NewGuid().ToString("N");
        Options = new OpenIddictDynamoDbOptions
        {
            ApplicationsTableName = $"OpenIddictApplications-{suffix}",
            AuthorizationsTableName = $"OpenIddictAuthorizations-{suffix}",
            ScopesTableName = $"OpenIddictScopes-{suffix}",
            TokensTableName = $"OpenIddictTokens-{suffix}"
        };
    }

    public IAmazonDynamoDB Client { get; private set; } = null!;

    public OpenIddictDynamoDbOptions Options { get; }

    public async Task InitializeAsync()
    {
        var endpointUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");

        if (_container is not null)
        {
            await _container.StartAsync();
            endpointUrl = _container.GetConnectionString();
        }

        var config = new AmazonDynamoDBConfig
        {
            AuthenticationRegion = "us-east-1",
            ServiceURL = endpointUrl
        };

        Client = new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), config);

        // Wait for DynamoDB Local to be ready (CI service containers may not be immediately available)
        await WaitForDynamoDbReadyAsync();

        await OpenIddictDynamoDbTableCreator.CreateTablesAsync(Client, Options, CancellationToken.None);
    }

    private async Task WaitForDynamoDbReadyAsync(int maxRetries = 10, int delayMs = 1000)
    {
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                await Client.ListTablesAsync(CancellationToken.None);
                return;
            }
            catch
            {
                if (i == maxRetries - 1) throw;
                await Task.Delay(delayMs);
            }
        }
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
