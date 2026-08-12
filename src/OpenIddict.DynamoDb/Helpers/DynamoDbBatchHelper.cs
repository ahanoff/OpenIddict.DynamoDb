using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace OpenIddict.DynamoDb.Helpers;

internal static class DynamoDbBatchHelper
{
    private const int MaximumAttempts = 8;

    internal static async Task<List<Dictionary<string, AttributeValue>>> GetItemsAsync(
        IAmazonDynamoDB client,
        string tableName,
        IEnumerable<Dictionary<string, AttributeValue>> keys,
        CancellationToken cancellationToken)
    {
        var results = new List<Dictionary<string, AttributeValue>>();
        var distinctKeys = keys.DistinctBy(CreateKeyIdentity, StringComparer.Ordinal).ToArray();
        foreach (var batch in distinctKeys.Chunk(100))
        {
            var pending = new Dictionary<string, KeysAndAttributes>
            {
                [tableName] = new() { ConsistentRead = true, Keys = batch.ToList() }
            };

            for (var attempt = 0; HasKeys(pending); attempt++)
            {
                if (attempt >= MaximumAttempts)
                {
                    throw new ProvisionedThroughputExceededException("DynamoDB did not process all batch-get keys within the retry budget.");
                }

                var response = await client.BatchGetItemAsync(new BatchGetItemRequest { RequestItems = pending }, cancellationToken);
                if ((response.Responses ?? []).TryGetValue(tableName, out var items))
                {
                    results.AddRange(items);
                }

                pending = response.UnprocessedKeys ?? [];
                if (HasKeys(pending))
                {
                    var maximumDelay = Math.Min(25 * (1 << attempt), 1000);
                    await Task.Delay(Random.Shared.Next(maximumDelay / 2, maximumDelay + 1), cancellationToken);
                }
            }
        }

        return results;
    }

    private static bool HasKeys(Dictionary<string, KeysAndAttributes> requestItems)
        => requestItems.Count > 0 && requestItems.Values.Any(static value => value.Keys.Count > 0);

    private static string CreateKeyIdentity(Dictionary<string, AttributeValue> key)
        => $"{DynamoDbHelper.GetString(key, "pk")}\0{DynamoDbHelper.GetString(key, "sk")}";
}
