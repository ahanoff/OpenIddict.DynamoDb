using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using OpenIddict.DynamoDb.Helpers;

namespace OpenIddict.DynamoDb.Tests.Helpers;

public sealed class DynamoDbBatchHelperTests
{
    [Fact]
    public async Task GetItemsAsync_RetriesUnprocessedKeys()
    {
        const string tableName = "tokens";
        var key = CreateKey("one");
        using var client = new BatchGetClient(
            new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>(),
                UnprocessedKeys = new Dictionary<string, KeysAndAttributes>
                {
                    [tableName] = new() { Keys = [key], ConsistentRead = true }
                }
            },
            new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    [tableName] = [CreateItem("one")]
                },
                UnprocessedKeys = []
            });

        var items = await DynamoDbBatchHelper.GetItemsAsync(client, tableName, [key], CancellationToken.None);

        Assert.Single(items);
        Assert.Equal(2, client.RequestCount);
        Assert.All(client.Requests, request => Assert.True(request.RequestItems[tableName].ConsistentRead));
    }

    [Fact]
    public async Task GetItemsAsync_DeduplicatesAndChunksKeys()
    {
        const string tableName = "tokens";
        var keys = Enumerable.Range(0, 101).Select(index => CreateKey(index.ToString())).ToList();
        keys.Add(CreateKey("0"));
        using var client = new BatchGetClient(
            CreateEchoResponse(tableName, keys.Take(100)),
            CreateEchoResponse(tableName, keys.Skip(100).Take(1)));

        var items = await DynamoDbBatchHelper.GetItemsAsync(client, tableName, keys, CancellationToken.None);

        Assert.Equal(101, items.Count);
        Assert.Equal([100, 1], client.Requests.Select(request => request.RequestItems[tableName].Keys.Count));
    }

    [Fact]
    public async Task GetItemsAsync_PropagatesCancellation()
    {
        using var client = new BatchGetClient();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DynamoDbBatchHelper.GetItemsAsync(client, "tokens", [CreateKey("one")], source.Token));
    }

    private static BatchGetItemResponse CreateEchoResponse(
        string tableName,
        IEnumerable<Dictionary<string, AttributeValue>> keys)
        => new()
        {
            Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
            {
                [tableName] = keys.Select(key => CreateItem(DynamoDbHelper.GetString(key, "pk")!)).ToList()
            },
            UnprocessedKeys = []
        };

    private static Dictionary<string, AttributeValue> CreateKey(string value)
        => new()
        {
            ["pk"] = DynamoDbHelper.ToAttr(value),
            ["sk"] = DynamoDbHelper.ToAttr("#META")
        };

    private static Dictionary<string, AttributeValue> CreateItem(string value)
        => new(CreateKey(value)) { ["id"] = DynamoDbHelper.ToAttr(value) };

    private sealed class BatchGetClient(params BatchGetItemResponse[] responses) : AmazonDynamoDBClient(
        new AnonymousAWSCredentials(),
        new AmazonDynamoDBConfig { ServiceURL = "http://127.0.0.1:1" })
    {
        private readonly Queue<BatchGetItemResponse> _responses = new(responses);

        internal List<BatchGetItemRequest> Requests { get; } = [];
        internal int RequestCount => Requests.Count;

        public override Task<BatchGetItemResponse> BatchGetItemAsync(
            BatchGetItemRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
