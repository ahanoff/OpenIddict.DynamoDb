using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Helpers;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb;

public class OpenIddictDynamoDbAuthorizationStore<TAuthorization> : IOpenIddictAuthorizationStore<TAuthorization>
    where TAuthorization : OpenIddictDynamoDbAuthorization, new()
{
    private const string SubjectIndexName = "SubjectIndex";
    private const string ApplicationIndexName = "ApplicationIndex";
    private const string AuthorizationIndexName = "AuthorizationIndex";

    private readonly IAmazonDynamoDB _client;
    private readonly OpenIddictDynamoDbOptions _options;

    public OpenIddictDynamoDbAuthorizationStore(IAmazonDynamoDB client, OpenIddictDynamoDbOptions options)
    {
        _client = client;
        _options = options;
    }

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        long count = 0;
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var response = await _client.ScanAsync(new ScanRequest
            {
                TableName = _options.AuthorizationsTableName,
                Select = Select.COUNT,
                FilterExpression = "sk = :meta",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":meta"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationSk)
                },
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            count += response.Count ?? 0;
            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);

        return count;
    }

    public ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<TAuthorization>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("DynamoDB does not support IQueryable-based queries.");

    public async ValueTask CreateAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        var attributes = ToItem(authorization);

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _options.AuthorizationsTableName,
            Item = attributes,
            ConditionExpression = "attribute_not_exists(pk)"
        }, cancellationToken);
    }

    public async ValueTask DeleteAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        try
        {
            await _client.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _options.AuthorizationsTableName,
                Key = CreateKey(authorization.Id),
                ConditionExpression = "#token = :expected",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#token"] = "concurrency_token"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":expected"] = DynamoDbHelper.ToAttr(authorization.ConcurrencyToken)
                }
            }, cancellationToken);
        }
        catch (ConditionalCheckFailedException exception)
        {
            throw CreateConcurrencyException(exception);
        }
    }

    public IAsyncEnumerable<TAuthorization> FindAsync(
        string? subject, string? client,
        CancellationToken cancellationToken)
        => FindAsync(subject, client, status: null, type: null, scopes: default, cancellationToken);

    public IAsyncEnumerable<TAuthorization> FindAsync(
        string? subject, string? client,
        string? status, CancellationToken cancellationToken)
        => FindAsync(subject, client, status, type: null, scopes: default, cancellationToken);

    public IAsyncEnumerable<TAuthorization> FindAsync(
        string? subject, string? client,
        string? status, string? type,
        CancellationToken cancellationToken)
        => FindAsync(subject, client, status, type, scopes: default, cancellationToken);

    public async IAsyncEnumerable<TAuthorization> FindAsync(
        string? subject, string? client,
        string? status, string? type,
        ImmutableArray<string>? scopes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var results = subject is not null
            ? QueryBySubjectAsync(subject, client, cancellationToken)
            : client is not null
                ? QueryByApplicationAsync(client, cancellationToken)
                : ScanAuthorizationsAsync(cancellationToken);

        await foreach (var authorization in results.WithCancellation(cancellationToken))
        {
            if (Matches(authorization, status, type, scopes))
            {
                yield return authorization;
            }
        }
    }

    public async IAsyncEnumerable<TAuthorization> FindByApplicationIdAsync(
        string identifier,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var authorization in QueryByApplicationAsync(identifier, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return authorization;
        }
    }

    public async ValueTask<TAuthorization?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.AuthorizationsTableName,
            Key = CreateKey(identifier),
            ConsistentRead = true
        }, cancellationToken);

        return response.Item is null || response.Item.Count == 0 ? null : DynamoDbHelper.ToAuthorization<TAuthorization>(response.Item);
    }

    public async IAsyncEnumerable<TAuthorization> FindBySubjectAsync(
        string subject,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var authorization in QueryBySubjectAsync(subject, applicationId: null, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return authorization;
        }
    }

    public ValueTask<string?> GetApplicationIdAsync(TAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.ApplicationId);

    public ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken cancellationToken)
        => throw new NotSupportedException("DynamoDB does not support IQueryable-based queries.");

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(TAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.CreationDate);

    public ValueTask<string?> GetIdAsync(TAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.Id);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TAuthorization authorization, CancellationToken cancellationToken)
        => new(JsonSerializationHelper.DeserializeProperties(authorization.Properties));

    public ValueTask<ImmutableArray<string>> GetScopesAsync(TAuthorization authorization, CancellationToken cancellationToken)
        => new(JsonSerializationHelper.DeserializeArray(authorization.Scopes));

    public ValueTask<string?> GetStatusAsync(TAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.Status);

    public ValueTask<string?> GetSubjectAsync(TAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.Subject);

    public ValueTask<string?> GetTypeAsync(TAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.Type);

    public ValueTask<TAuthorization> InstantiateAsync(CancellationToken cancellationToken)
        => new(new TAuthorization { ConcurrencyToken = Guid.NewGuid().ToString() });

    public async IAsyncEnumerable<TAuthorization> ListAsync(
        int? count,
        int? offset,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var skipped = 0;
        var yielded = 0;
        var skip = Math.Max(offset ?? 0, 0);
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var request = new ScanRequest
            {
                TableName = _options.AuthorizationsTableName,
                FilterExpression = "sk = :meta",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":meta"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationSk)
                },
                ExclusiveStartKey = lastKey
            };

            if (count.HasValue)
            {
                request.Limit = Math.Max(count.Value + skip - skipped - yielded, 1);
            }

            var response = await _client.ScanAsync(request, cancellationToken);

            foreach (var item in response.Items ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (skipped < skip)
                {
                    skipped++;
                    continue;
                }

                if (count.HasValue && yielded >= count.Value)
                {
                    yield break;
                }

                yielded++;
                yield return DynamoDbHelper.ToAuthorization<TAuthorization>(item);
            }

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null && (!count.HasValue || yielded < count.Value));
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken cancellationToken)
        => throw new NotSupportedException("DynamoDB does not support IQueryable-based queries.");

    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        long deleted = 0;
        var batch = new List<WriteRequest>(25);

        await foreach (var authorization in ScanAuthorizationsAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            if (deleted + batch.Count >= _options.PruneBatchSize)
            {
                break;
            }

            if (authorization.CreationDate >= threshold)
            {
                continue;
            }

            if (string.Equals(authorization.Status, "valid", StringComparison.Ordinal) &&
                !string.Equals(authorization.Type, "ad-hoc", StringComparison.Ordinal))
            {
                continue;
            }

            if (await HasTokensAsync(authorization.Id, cancellationToken))
            {
                continue;
            }

            batch.Add(new WriteRequest
            {
                DeleteRequest = new DeleteRequest
                {
                    Key = CreateKey(authorization.Id)
                }
            });

            if (batch.Count == 25)
            {
                deleted += await FlushDeleteBatchAsync(batch, cancellationToken);
            }
        }

        if (batch.Count > 0)
        {
            deleted += await FlushDeleteBatchAsync(batch, cancellationToken);
        }

        return deleted;
    }

    public async ValueTask<long> RevokeAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken)
    {
        long count = 0;

        await foreach (var authorization in FindAsync(subject, client, status, type, scopes: default, cancellationToken).WithCancellation(cancellationToken))
        {
            if (await RevokeAuthorizationAsync(authorization.Id, cancellationToken))
            {
                count++;
            }
        }

        return count;
    }

    public async ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken)
    {
        long count = 0;

        await foreach (var authorization in QueryByApplicationAsync(identifier, cancellationToken).WithCancellation(cancellationToken))
        {
            if (await RevokeAuthorizationAsync(authorization.Id, cancellationToken))
            {
                count++;
            }
        }

        return count;
    }

    public async ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken)
    {
        long count = 0;

        await foreach (var authorization in QueryBySubjectAsync(subject, applicationId: null, cancellationToken).WithCancellation(cancellationToken))
        {
            if (await RevokeAuthorizationAsync(authorization.Id, cancellationToken))
            {
                count++;
            }
        }

        return count;
    }

    public ValueTask SetApplicationIdAsync(TAuthorization authorization, string? identifier, CancellationToken cancellationToken)
    {
        authorization.ApplicationId = identifier;
        return default;
    }

    public ValueTask SetCreationDateAsync(TAuthorization authorization, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        authorization.CreationDate = date;
        return default;
    }

    public ValueTask SetPropertiesAsync(
        TAuthorization authorization,
        ImmutableDictionary<string, JsonElement> properties,
        CancellationToken cancellationToken)
    {
        authorization.Properties = JsonSerializationHelper.SerializeProperties(properties);
        return default;
    }

    public ValueTask SetScopesAsync(
        TAuthorization authorization,
        ImmutableArray<string> scopes,
        CancellationToken cancellationToken)
    {
        authorization.Scopes = JsonSerializationHelper.SerializeArray(scopes);
        return default;
    }

    public ValueTask SetStatusAsync(TAuthorization authorization, string? status, CancellationToken cancellationToken)
    {
        authorization.Status = status;
        return default;
    }

    public ValueTask SetSubjectAsync(TAuthorization authorization, string? subject, CancellationToken cancellationToken)
    {
        authorization.Subject = subject;
        return default;
    }

    public ValueTask SetTypeAsync(TAuthorization authorization, string? type, CancellationToken cancellationToken)
    {
        authorization.Type = type;
        return default;
    }

    public async ValueTask UpdateAsync(TAuthorization authorization, CancellationToken cancellationToken)
    {
        var expected = authorization.ConcurrencyToken;
        authorization.ConcurrencyToken = Guid.NewGuid().ToString();
        var attributes = ToItem(authorization);

        try
        {
            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = _options.AuthorizationsTableName,
                Item = attributes,
                ConditionExpression = "#token = :expected",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#token"] = "concurrency_token"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":expected"] = DynamoDbHelper.ToAttr(expected)
                }
            }, cancellationToken);
        }
        catch (ConditionalCheckFailedException exception)
        {
            authorization.ConcurrencyToken = expected;
            throw CreateConcurrencyException(exception);
        }
    }

    private Dictionary<string, AttributeValue> ToItem(TAuthorization authorization)
    {
        var attributes = DynamoDbHelper.ToAttributes(authorization);

        attributes["pk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationPk(authorization.Id));
        attributes["sk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationSk);

        if (authorization.Subject is not null)
        {
            var applicationId = authorization.ApplicationId ?? KeyHelper.NullSentinel;
            attributes["gsi1_pk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationSubjectIndexPk(authorization.Subject));
            attributes["gsi1_sk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationSubjectIndexSk(applicationId, authorization.Id));
        }

        if (authorization.ApplicationId is not null)
        {
            attributes["gsi2_pk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationApplicationIndexPk(authorization.ApplicationId));
            attributes["gsi2_sk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationApplicationIndexSk(authorization.Id));
        }

        return attributes;
    }

    private static Dictionary<string, AttributeValue> CreateKey(string identifier)
        => new()
        {
            ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationPk(identifier)),
            ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationSk)
        };

    private async IAsyncEnumerable<TAuthorization> QueryBySubjectAsync(
        string subject,
        string? applicationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var expressionValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationSubjectIndexPk(subject))
            };

            var request = new QueryRequest
            {
                TableName = _options.AuthorizationsTableName,
                IndexName = SubjectIndexName,
                KeyConditionExpression = "gsi1_pk = :pk",
                ExpressionAttributeValues = expressionValues,
                ExclusiveStartKey = lastKey
            };

            if (applicationId is not null)
            {
                expressionValues[":sk"] = DynamoDbHelper.ToAttr($"APP#{applicationId}#");
                request.KeyConditionExpression += " AND begins_with(gsi1_sk, :sk)";
            }

            var response = await _client.QueryAsync(request, cancellationToken);

            foreach (var item in response.Items ?? [])
            {
                yield return DynamoDbHelper.ToAuthorization<TAuthorization>(item);
            }

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);
    }

    private async IAsyncEnumerable<TAuthorization> QueryByApplicationAsync(
        string applicationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var response = await _client.QueryAsync(new QueryRequest
            {
                TableName = _options.AuthorizationsTableName,
                IndexName = ApplicationIndexName,
                KeyConditionExpression = "gsi2_pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationApplicationIndexPk(applicationId))
                },
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            foreach (var item in response.Items ?? [])
            {
                yield return DynamoDbHelper.ToAuthorization<TAuthorization>(item);
            }

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);
    }

    private async IAsyncEnumerable<TAuthorization> ScanAuthorizationsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var response = await _client.ScanAsync(new ScanRequest
            {
                TableName = _options.AuthorizationsTableName,
                FilterExpression = "sk = :meta",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":meta"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationSk)
                },
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            foreach (var item in response.Items ?? [])
            {
                yield return DynamoDbHelper.ToAuthorization<TAuthorization>(item);
            }

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);
    }

    private static bool Matches(TAuthorization authorization, string? status, string? type, ImmutableArray<string>? scopes)
    {
        if (status is not null && !string.Equals(authorization.Status, status, StringComparison.Ordinal))
        {
            return false;
        }

        if (type is not null && !string.Equals(authorization.Type, type, StringComparison.Ordinal))
        {
            return false;
        }

        if (scopes is { IsDefaultOrEmpty: false })
        {
            var authorizationScopes = JsonSerializationHelper.DeserializeArray(authorization.Scopes).ToHashSet(StringComparer.Ordinal);
            if (!scopes.Value.All(authorizationScopes.Contains))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> HasTokensAsync(string authorizationId, CancellationToken cancellationToken)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = _options.TokensTableName,
            IndexName = AuthorizationIndexName,
            KeyConditionExpression = "gsi4_pk = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenAuthorizationIndexPk(authorizationId))
            },
            Limit = 1
        }, cancellationToken);

        return (response.Count ?? 0) > 0;
    }

    private async Task<int> FlushDeleteBatchAsync(List<WriteRequest> batch, CancellationToken cancellationToken)
    {
        var count = batch.Count;
        var delay = TimeSpan.FromMilliseconds(50);
        var requestItems = new Dictionary<string, List<WriteRequest>>
        {
            [_options.AuthorizationsTableName] = batch
        };

        do
        {
            var response = await _client.BatchWriteItemAsync(new BatchWriteItemRequest
            {
                RequestItems = requestItems
            }, cancellationToken);

            requestItems = response.UnprocessedItems ?? [];

            if (requestItems.Count > 0 && requestItems.Values.Any(items => items.Count > 0))
            {
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 1000));
            }
        }
        while (requestItems.Count > 0 && requestItems.Values.Any(items => items.Count > 0));

        batch.Clear();
        return count;
    }

    private async Task<bool> RevokeAuthorizationAsync(string authorizationId, CancellationToken cancellationToken)
    {
        try
        {
            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _options.AuthorizationsTableName,
                Key = CreateKey(authorizationId),
                UpdateExpression = "SET #status = :revoked, #token = :token",
                ConditionExpression = "attribute_exists(pk) AND (attribute_not_exists(#status) OR #status <> :revoked)",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "status",
                    ["#token"] = "concurrency_token"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":revoked"] = DynamoDbHelper.ToAttr("revoked"),
                    [":token"] = DynamoDbHelper.ToAttr(Guid.NewGuid().ToString())
                }
            }, cancellationToken);

            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    private static OpenIddictExceptions.ConcurrencyException CreateConcurrencyException(Exception exception)
        => new("The authorization was concurrently updated and cannot be persisted in its current state.", exception);
}
