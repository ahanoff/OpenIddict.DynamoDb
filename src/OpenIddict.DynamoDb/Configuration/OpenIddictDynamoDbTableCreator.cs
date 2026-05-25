using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace OpenIddict.DynamoDb;

public class OpenIddictDynamoDbTableCreator
{
    public static async Task CreateTablesAsync(
        IAmazonDynamoDB client,
        OpenIddictDynamoDbOptions options,
        CancellationToken cancellationToken = default)
    {
        await CreateApplicationsTableAsync(client, options, cancellationToken);
        await CreateScopesTableAsync(client, options, cancellationToken);
        await CreateAuthorizationsTableAsync(client, options, cancellationToken);
        await CreateTokensTableAsync(client, options, cancellationToken);
    }

    private static async Task CreateApplicationsTableAsync(
        IAmazonDynamoDB client,
        OpenIddictDynamoDbOptions options,
        CancellationToken cancellationToken)
    {
        await CreateTableIfNotExistsAsync(client, new CreateTableRequest
        {
            TableName = options.ApplicationsTableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            KeySchema = CreateKeySchema("pk", "sk"),
            AttributeDefinitions = CreateAttributeDefinitions("pk", "sk")
        }, cancellationToken);
    }

    private static async Task CreateScopesTableAsync(
        IAmazonDynamoDB client,
        OpenIddictDynamoDbOptions options,
        CancellationToken cancellationToken)
    {
        await CreateTableIfNotExistsAsync(client, new CreateTableRequest
        {
            TableName = options.ScopesTableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            KeySchema = CreateKeySchema("pk", "sk"),
            AttributeDefinitions = CreateAttributeDefinitions("pk", "sk")
        }, cancellationToken);
    }

    private static async Task CreateAuthorizationsTableAsync(
        IAmazonDynamoDB client,
        OpenIddictDynamoDbOptions options,
        CancellationToken cancellationToken)
    {
        await CreateTableIfNotExistsAsync(client, new CreateTableRequest
        {
            TableName = options.AuthorizationsTableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            KeySchema = CreateKeySchema("pk", "sk"),
            AttributeDefinitions = CreateAttributeDefinitions("pk", "sk", "gsi1_pk", "gsi1_sk", "gsi2_pk", "gsi2_sk"),
            GlobalSecondaryIndexes =
            [
                CreateGlobalSecondaryIndex("SubjectIndex", "gsi1_pk", "gsi1_sk"),
                CreateGlobalSecondaryIndex("ApplicationIndex", "gsi2_pk", "gsi2_sk")
            ]
        }, cancellationToken);
    }

    private static async Task CreateTokensTableAsync(
        IAmazonDynamoDB client,
        OpenIddictDynamoDbOptions options,
        CancellationToken cancellationToken)
    {
        await CreateTableIfNotExistsAsync(client, new CreateTableRequest
        {
            TableName = options.TokensTableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            KeySchema = CreateKeySchema("pk", "sk"),
            AttributeDefinitions = CreateAttributeDefinitions(
                "pk",
                "sk",
                "gsi1_pk",
                "gsi1_sk",
                "gsi2_pk",
                "gsi2_sk",
                "gsi3_pk",
                "gsi3_sk",
                "gsi4_pk",
                "gsi4_sk"),
            GlobalSecondaryIndexes =
            [
                CreateGlobalSecondaryIndex("SubjectAppIndex", "gsi1_pk", "gsi1_sk"),
                CreateGlobalSecondaryIndex("SubjectIndex", "gsi2_pk", "gsi2_sk"),
                CreateGlobalSecondaryIndex("ApplicationShardedIndex", "gsi3_pk", "gsi3_sk"),
                CreateGlobalSecondaryIndex("AuthorizationIndex", "gsi4_pk", "gsi4_sk")
            ]
        }, cancellationToken);

        await client.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
        {
            TableName = options.TokensTableName,
            TimeToLiveSpecification = new TimeToLiveSpecification
            {
                AttributeName = "ttl",
                Enabled = true
            }
        }, cancellationToken);
    }

    private static async Task CreateTableIfNotExistsAsync(
        IAmazonDynamoDB client,
        CreateTableRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.CreateTableAsync(request, cancellationToken);
        }
        catch (ResourceInUseException)
        {
        }

        await WaitForTableActiveAsync(client, request.TableName, cancellationToken);
    }

    private static List<KeySchemaElement> CreateKeySchema(string hashKey, string rangeKey) =>
    [
        new KeySchemaElement(hashKey, KeyType.HASH),
        new KeySchemaElement(rangeKey, KeyType.RANGE)
    ];

    private static List<AttributeDefinition> CreateAttributeDefinitions(params string[] attributeNames) =>
        attributeNames
            .Select(attributeName => new AttributeDefinition(attributeName, ScalarAttributeType.S))
            .ToList();

    private static GlobalSecondaryIndex CreateGlobalSecondaryIndex(string indexName, string hashKey, string rangeKey) =>
        new()
        {
            IndexName = indexName,
            KeySchema = CreateKeySchema(hashKey, rangeKey),
            Projection = new Projection
            {
                ProjectionType = ProjectionType.ALL
            }
        };

    private static async Task WaitForTableActiveAsync(
        IAmazonDynamoDB client, string tableName, CancellationToken cancellationToken)
    {
        while (true)
        {
            var response = await client.DescribeTableAsync(tableName, cancellationToken);
            if (response.Table.TableStatus == TableStatus.ACTIVE)
                return;

            await Task.Delay(1000, cancellationToken);
        }
    }
}
