using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace OpenIddict.DynamoDb.Tests.Helpers;

internal sealed class RevocationRaceClient(
    IAmazonDynamoDB inner,
    Func<CancellationToken, Task> beforeTransaction) : AmazonDynamoDBClient(
        new AnonymousAWSCredentials(),
        new AmazonDynamoDBConfig { ServiceURL = "http://127.0.0.1:1" })
{
    private int _triggered;

    public override Task<GetItemResponse> GetItemAsync(
        GetItemRequest request,
        CancellationToken cancellationToken = default)
        => inner.GetItemAsync(request, cancellationToken);

    public override Task<BatchGetItemResponse> BatchGetItemAsync(
        BatchGetItemRequest request,
        CancellationToken cancellationToken = default)
        => inner.BatchGetItemAsync(request, cancellationToken);

    public override async Task<TransactWriteItemsResponse> TransactWriteItemsAsync(
        TransactWriteItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _triggered, 1) == 0)
        {
            await beforeTransaction(cancellationToken);
        }

        return await inner.TransactWriteItemsAsync(request, cancellationToken);
    }
}
