using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Helpers;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb;

public class OpenIddictDynamoDbTokenStore<TToken> : IOpenIddictTokenStore<TToken>
    where TToken : OpenIddictDynamoDbToken, new()
{
    private const string SubjectAppIndexName = "SubjectAppIndex";
    private const string SubjectIndexName = "SubjectIndex";
    private const string ApplicationShardedIndexName = "ApplicationShardedIndex";
    private const string AuthorizationIndexName = "AuthorizationIndex";

    private readonly IAmazonDynamoDB _client;
    private readonly OpenIddictDynamoDbOptions _options;

    public OpenIddictDynamoDbTokenStore(IAmazonDynamoDB client, OpenIddictDynamoDbOptions options)
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
                TableName = _options.TokensTableName,
                Select = Select.COUNT,
                FilterExpression = "sk = :meta",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":meta"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSk)
                },
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            count += response.Count ?? 0;
            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);

        return count;
    }

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<TToken>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        => throw new NotSupportedException("DynamoDB does not support IQueryable-based queries.");

    public async ValueTask CreateAsync(TToken token, CancellationToken cancellationToken)
    {
        var transactItems = new List<TransactWriteItem>
        {
            new()
            {
                Put = new Put
                {
                    TableName = _options.TokensTableName,
                    Item = ToItem(token),
                    ConditionExpression = "attribute_not_exists(pk)"
                }
            }
        };

        if (token.ReferenceId is not null)
        {
            transactItems.Add(new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = _options.TokensTableName,
                    Item = ToReferenceItem(token.ReferenceId, token.Id, token.ExpirationDate),
                    ConditionExpression = "attribute_not_exists(pk)"
                }
            });
        }

        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                ClientRequestToken = Guid.NewGuid().ToString(),
                TransactItems = transactItems
            }, cancellationToken);
        }
        catch (ConditionalCheckFailedException exception)
        {
            throw CreateConcurrencyException(exception);
        }
        catch (TransactionCanceledException exception) when (HasConditionalCheckFailure(exception))
        {
            throw CreateConcurrencyException(exception);
        }
    }

    public async ValueTask DeleteAsync(TToken token, CancellationToken cancellationToken)
    {
        var transactItems = new List<TransactWriteItem>
        {
            new()
            {
                Delete = new Delete
                {
                    TableName = _options.TokensTableName,
                    Key = CreateKey(token.Id),
                    ConditionExpression = "#token = :expected",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        ["#token"] = "concurrency_token"
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":expected"] = DynamoDbHelper.ToAttr(token.ConcurrencyToken)
                    }
                }
            }
        };

        if (token.ReferenceId is not null)
        {
            transactItems.Add(new TransactWriteItem
            {
                Delete = new Delete
                {
                    TableName = _options.TokensTableName,
                    Key = CreateReferenceKey(token.ReferenceId)
                }
            });
        }

        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                ClientRequestToken = Guid.NewGuid().ToString(),
                TransactItems = transactItems
            }, cancellationToken);
        }
        catch (ConditionalCheckFailedException exception)
        {
            throw CreateConcurrencyException(exception);
        }
        catch (TransactionCanceledException exception) when (HasConditionalCheckFailure(exception))
        {
            throw CreateConcurrencyException(exception);
        }
    }

    public IAsyncEnumerable<TToken> FindAsync(string? subject, string? client, CancellationToken cancellationToken)
        => FindCoreAsync(subject, client, status: null, type: null, cancellationToken);

    public IAsyncEnumerable<TToken> FindAsync(string? subject, string? client, string? status, CancellationToken cancellationToken)
        => FindCoreAsync(subject, client, status, type: null, cancellationToken);

    public IAsyncEnumerable<TToken> FindAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        CancellationToken cancellationToken)
        => FindCoreAsync(subject, client, status, type, cancellationToken);

    private async IAsyncEnumerable<TToken> FindCoreAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        IAsyncEnumerable<TToken> results;

        if (subject is not null && client is not null)
        {
            results = QueryBySubjectAndApplicationAsync(subject, client, status, type, cancellationToken);
        }
        else if (subject is not null)
        {
            results = QueryBySubjectAsync(subject, cancellationToken);
        }
        else if (client is not null)
        {
            results = QueryByApplicationAsync(client, cancellationToken);
        }
        else
        {
            results = ScanTokensAsync(status, type, cancellationToken);
        }

        await foreach (var token in results.WithCancellation(cancellationToken))
        {
            if (Matches(token, status, type) && !IsExpired(token, now))
            {
                yield return token;
            }
        }
    }

    public async IAsyncEnumerable<TToken> FindByApplicationIdAsync(
        string identifier,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await foreach (var token in QueryByApplicationAsync(identifier, cancellationToken).WithCancellation(cancellationToken))
        {
            if (!IsExpired(token, now))
            {
                yield return token;
            }
        }
    }

    public async IAsyncEnumerable<TToken> FindByAuthorizationIdAsync(
        string identifier,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var token in QueryByAuthorizationAsync(identifier, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return token;
        }
    }

    public async ValueTask<TToken?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TokensTableName,
            Key = CreateKey(identifier),
            ConsistentRead = true
        }, cancellationToken);

        if (response.Item is null || response.Item.Count == 0)
        {
            return null;
        }

        var token = DynamoDbHelper.ToToken<TToken>(response.Item);
        return IsExpired(token, DateTimeOffset.UtcNow) ? null : token;
    }

    public async ValueTask<TToken?> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken)
    {
        var referenceResponse = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TokensTableName,
            Key = CreateReferenceKey(identifier),
            ConsistentRead = true
        }, cancellationToken);

        if (referenceResponse.Item is null ||
            referenceResponse.Item.Count == 0 ||
            !referenceResponse.Item.TryGetValue("token_id", out var tokenIdAttribute) ||
            string.IsNullOrEmpty(tokenIdAttribute.S))
        {
            return null;
        }

        var tokenResponse = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TokensTableName,
            Key = CreateKey(tokenIdAttribute.S),
            ConsistentRead = true
        }, cancellationToken);

        if (tokenResponse.Item is null || tokenResponse.Item.Count == 0)
        {
            return null;
        }

        var token = DynamoDbHelper.ToToken<TToken>(tokenResponse.Item);
        return IsExpired(token, DateTimeOffset.UtcNow) ? null : token;
    }

    public async IAsyncEnumerable<TToken> FindBySubjectAsync(
        string subject,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await foreach (var token in QueryBySubjectAsync(subject, cancellationToken).WithCancellation(cancellationToken))
        {
            if (!IsExpired(token, now))
            {
                yield return token;
            }
        }
    }

    public ValueTask<string?> GetApplicationIdAsync(TToken token, CancellationToken cancellationToken)
        => new(token.ApplicationId);

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<TToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
        => throw new NotSupportedException("DynamoDB does not support IQueryable-based queries.");

    public ValueTask<string?> GetAuthorizationIdAsync(TToken token, CancellationToken cancellationToken)
        => new(token.AuthorizationId);

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(TToken token, CancellationToken cancellationToken)
        => new(token.CreationDate);

    public ValueTask<DateTimeOffset?> GetExpirationDateAsync(TToken token, CancellationToken cancellationToken)
        => new(token.ExpirationDate);

    public ValueTask<string?> GetIdAsync(TToken token, CancellationToken cancellationToken)
        => new(token.Id);

    public ValueTask<string?> GetPayloadAsync(TToken token, CancellationToken cancellationToken)
        => new(token.Payload);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TToken token, CancellationToken cancellationToken)
        => new(JsonSerializationHelper.DeserializeProperties(token.Properties));

    public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(TToken token, CancellationToken cancellationToken)
        => new(token.RedemptionDate);

    public ValueTask<string?> GetReferenceIdAsync(TToken token, CancellationToken cancellationToken)
        => new(token.ReferenceId);

    public ValueTask<string?> GetStatusAsync(TToken token, CancellationToken cancellationToken)
        => new(token.Status);

    public ValueTask<string?> GetSubjectAsync(TToken token, CancellationToken cancellationToken)
        => new(token.Subject);

    public ValueTask<string?> GetTypeAsync(TToken token, CancellationToken cancellationToken)
        => new(token.Type);

    public ValueTask<TToken> InstantiateAsync(CancellationToken cancellationToken)
        => new(new TToken { ConcurrencyToken = Guid.NewGuid().ToString() });

    public async IAsyncEnumerable<TToken> ListAsync(
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
                TableName = _options.TokensTableName,
                FilterExpression = "sk = :meta",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":meta"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSk)
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
                yield return DynamoDbHelper.ToToken<TToken>(item);
            }

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null && (!count.HasValue || yielded < count.Value));
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<TToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
        => throw new NotSupportedException("DynamoDB does not support IQueryable-based queries.");

    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        var segments = Math.Max(_options.PruneParallelSegments, 1);
        var tasks = Enumerable.Range(0, segments)
            .Select(segment => ScanPruneCandidatesAsync(threshold, segment, segments, cancellationToken))
            .ToArray();

        var candidateSets = await Task.WhenAll(tasks);
        var candidates = candidateSets
            .SelectMany(static tokens => tokens)
            .Where(static token => IsPrunable(token))
            .Take(Math.Max(_options.PruneBatchSize, 0));

        long deleted = 0;
        var batch = new List<WriteRequest>(25);

        foreach (var token in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var requestsNeeded = token.ReferenceId is null ? 1 : 2;
            if (batch.Count + requestsNeeded > 25)
            {
                await FlushDeleteBatchAsync(batch, cancellationToken);
            }

            batch.Add(new WriteRequest
            {
                DeleteRequest = new DeleteRequest
                {
                    Key = CreateKey(token.Id)
                }
            });

            if (token.ReferenceId is not null)
            {
                batch.Add(new WriteRequest
                {
                    DeleteRequest = new DeleteRequest
                    {
                        Key = CreateReferenceKey(token.ReferenceId)
                    }
                });
            }

            deleted++;
        }

        if (batch.Count > 0)
        {
            await FlushDeleteBatchAsync(batch, cancellationToken);
        }

        return deleted;
    }

    public async ValueTask<long> RevokeByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken)
    {
        long count = 0;

        await foreach (var token in QueryByAuthorizationAsync(identifier, cancellationToken).WithCancellation(cancellationToken))
        {
            if (await RevokeTokenAsync(token.Id, cancellationToken))
            {
                count++;
            }
        }

        return count;
    }

    public async ValueTask<long> RevokeAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken)
    {
        long count = 0;

        await foreach (var token in FindCoreAsync(subject, client, status, type, cancellationToken).WithCancellation(cancellationToken))
        {
            if (await RevokeTokenAsync(token.Id, cancellationToken))
            {
                count++;
            }
        }

        return count;
    }

    public async ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken)
    {
        long count = 0;

        await foreach (var token in QueryByApplicationAsync(identifier, cancellationToken).WithCancellation(cancellationToken))
        {
            if (await RevokeTokenAsync(token.Id, cancellationToken))
            {
                count++;
            }
        }

        return count;
    }

    public async ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken)
    {
        long count = 0;

        await foreach (var token in QueryBySubjectAsync(subject, cancellationToken).WithCancellation(cancellationToken))
        {
            if (await RevokeTokenAsync(token.Id, cancellationToken))
            {
                count++;
            }
        }

        return count;
    }

    public ValueTask SetApplicationIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
    {
        token.ApplicationId = identifier;
        return default;
    }

    public ValueTask SetAuthorizationIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
    {
        token.AuthorizationId = identifier;
        return default;
    }

    public ValueTask SetCreationDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        token.CreationDate = date;
        return default;
    }

    public ValueTask SetExpirationDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        token.ExpirationDate = date;
        return default;
    }

    public ValueTask SetPayloadAsync(TToken token, string? payload, CancellationToken cancellationToken)
    {
        token.Payload = payload;
        return default;
    }

    public ValueTask SetPropertiesAsync(TToken token, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        token.Properties = JsonSerializationHelper.SerializeProperties(properties);
        return default;
    }

    public ValueTask SetRedemptionDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        token.RedemptionDate = date;
        return default;
    }

    public ValueTask SetReferenceIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
    {
        token.ReferenceId = identifier;
        return default;
    }

    public ValueTask SetStatusAsync(TToken token, string? status, CancellationToken cancellationToken)
    {
        token.Status = status;
        return default;
    }

    public ValueTask SetSubjectAsync(TToken token, string? subject, CancellationToken cancellationToken)
    {
        token.Subject = subject;
        return default;
    }

    public ValueTask SetTypeAsync(TToken token, string? type, CancellationToken cancellationToken)
    {
        token.Type = type;
        return default;
    }

    public async ValueTask UpdateAsync(TToken token, CancellationToken cancellationToken)
    {
        var oldResponse = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TokensTableName,
            Key = CreateKey(token.Id),
            ConsistentRead = true
        }, cancellationToken);

        var oldToken = oldResponse.Item is null || oldResponse.Item.Count == 0 ? null : DynamoDbHelper.ToToken<TToken>(oldResponse.Item);
        var expected = token.ConcurrencyToken;
        var previousReferenceId = oldToken?.ReferenceId;
        var newReferenceId = token.ReferenceId;

        token.ConcurrencyToken = Guid.NewGuid().ToString();
        var transactItems = new List<TransactWriteItem>
        {
            new()
            {
                Put = new Put
                {
                    TableName = _options.TokensTableName,
                    Item = ToItem(token),
                    ConditionExpression = "#token = :expected",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        ["#token"] = "concurrency_token"
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":expected"] = DynamoDbHelper.ToAttr(expected)
                    }
                }
            }
        };

        if (!string.Equals(previousReferenceId, newReferenceId, StringComparison.Ordinal))
        {
            if (previousReferenceId is not null)
            {
                transactItems.Add(new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = _options.TokensTableName,
                        Key = CreateReferenceKey(previousReferenceId)
                    }
                });
            }

            if (newReferenceId is not null)
            {
                transactItems.Add(new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _options.TokensTableName,
                        Item = ToReferenceItem(newReferenceId, token.Id, token.ExpirationDate),
                        ConditionExpression = "attribute_not_exists(pk)"
                    }
                });
            }
        }
        else if (newReferenceId is not null && oldToken?.ExpirationDate != token.ExpirationDate)
        {
            transactItems.Add(new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = _options.TokensTableName,
                    Item = ToReferenceItem(newReferenceId, token.Id, token.ExpirationDate)
                }
            });
        }

        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                ClientRequestToken = Guid.NewGuid().ToString(),
                TransactItems = transactItems
            }, cancellationToken);
        }
        catch (ConditionalCheckFailedException exception)
        {
            token.ConcurrencyToken = expected;
            throw CreateConcurrencyException(exception);
        }
        catch (TransactionCanceledException exception) when (HasConditionalCheckFailure(exception))
        {
            token.ConcurrencyToken = expected;
            throw CreateConcurrencyException(exception);
        }
    }

    private Dictionary<string, AttributeValue> ToItem(TToken token)
    {
        token.TTL = GetTtl(token.ExpirationDate);
        var attributes = DynamoDbHelper.ToAttributes(token);

        attributes["pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenPk(token.Id));
        attributes["sk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSk);

        if (token.Subject is not null && token.ApplicationId is not null)
        {
            attributes["gsi1_pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSubjectAppIndexPk(token.Subject, token.ApplicationId));
            attributes["gsi1_sk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSubjectAppIndexSk(token.Status, token.Type, token.Id));
        }

        if (token.Subject is not null)
        {
            attributes["gsi2_pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSubjectIndexPk(token.Subject));
            attributes["gsi2_sk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSubjectIndexSk(token.Id));
        }

        if (token.ApplicationId is not null)
        {
            attributes["gsi3_pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenApplicationShardedIndexPk(token.ApplicationId, token.Id, _options.TokenApplicationShardCount));
            attributes["gsi3_sk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenApplicationShardedIndexSk(token.Status, token.Type, token.Id));
        }

        if (token.AuthorizationId is not null)
        {
            attributes["gsi4_pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenAuthorizationIndexPk(token.AuthorizationId));
            attributes["gsi4_sk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenAuthorizationIndexSk(token.Id));
        }

        return attributes;
    }

    private static Dictionary<string, AttributeValue> ToReferenceItem(string referenceId, string tokenId, DateTimeOffset? expirationDate)
    {
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ReferenceIdPk(referenceId)),
            ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ReferenceIdSk),
            ["token_id"] = DynamoDbHelper.ToAttr(tokenId)
        };

        var ttl = GetTtl(expirationDate);
        if (ttl.HasValue)
        {
            attributes["ttl"] = DynamoDbHelper.ToAttr(ttl);
        }

        return attributes;
    }

    private static Dictionary<string, AttributeValue> CreateKey(string identifier)
        => new()
        {
            ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenPk(identifier)),
            ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSk)
        };

    private static Dictionary<string, AttributeValue> CreateReferenceKey(string referenceId)
        => new()
        {
            ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ReferenceIdPk(referenceId)),
            ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ReferenceIdSk)
        };

    private async IAsyncEnumerable<TToken> QueryBySubjectAndApplicationAsync(
        string subject,
        string applicationId,
        string? status,
        string? type,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var expressionValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSubjectAppIndexPk(subject, applicationId))
            };

            var request = new QueryRequest
            {
                TableName = _options.TokensTableName,
                IndexName = SubjectAppIndexName,
                KeyConditionExpression = "gsi1_pk = :pk",
                ExpressionAttributeValues = expressionValues,
                ExclusiveStartKey = lastKey
            };

            var skPrefix = CreateStatusTypePrefix(status, type);
            if (skPrefix is not null)
            {
                expressionValues[":sk"] = DynamoDbHelper.ToAttr(skPrefix);
                request.KeyConditionExpression += " AND begins_with(gsi1_sk, :sk)";
            }

            var response = await _client.QueryAsync(request, cancellationToken);

            foreach (var item in response.Items ?? [])
            {
                yield return DynamoDbHelper.ToToken<TToken>(item);
            }

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);
    }

    private async IAsyncEnumerable<TToken> QueryBySubjectAsync(
        string subject,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var response = await _client.QueryAsync(new QueryRequest
            {
                TableName = _options.TokensTableName,
                IndexName = SubjectIndexName,
                KeyConditionExpression = "gsi2_pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSubjectIndexPk(subject))
                },
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            foreach (var item in response.Items ?? [])
            {
                yield return DynamoDbHelper.ToToken<TToken>(item);
            }

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);
    }

    private async IAsyncEnumerable<TToken> QueryByApplicationAsync(
        string applicationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tasks = Enumerable.Range(0, Math.Max(_options.TokenApplicationShardCount, 1))
            .Select(shard => QueryApplicationShardAsync(applicationId, shard, cancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        foreach (var token in results.SelectMany(static tokens => tokens))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return token;
        }
    }

    private async Task<List<TToken>> QueryApplicationShardAsync(string applicationId, int shard, CancellationToken cancellationToken)
    {
        var tokens = new List<TToken>();
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var response = await _client.QueryAsync(new QueryRequest
            {
                TableName = _options.TokensTableName,
                IndexName = ApplicationShardedIndexName,
                KeyConditionExpression = "gsi3_pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = DynamoDbHelper.ToAttr($"APP#{applicationId}#S#{shard}")
                },
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            tokens.AddRange((response.Items ?? []).Select(DynamoDbHelper.ToToken<TToken>));
            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);

        return tokens;
    }

    private async IAsyncEnumerable<TToken> QueryByAuthorizationAsync(
        string authorizationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Dictionary<string, AttributeValue>? lastKey = null;

        do
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
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            foreach (var item in response.Items ?? [])
            {
                yield return DynamoDbHelper.ToToken<TToken>(item);
            }

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);
    }

    private async IAsyncEnumerable<TToken> ScanTokensAsync(
        string? status,
        string? type,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var expressionNames = new Dictionary<string, string>();
            var expressionValues = new Dictionary<string, AttributeValue>
            {
                [":meta"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSk)
            };
            var filterExpression = "sk = :meta";

            if (status is not null)
            {
                expressionNames["#status"] = "status";
                expressionValues[":status"] = DynamoDbHelper.ToAttr(status);
                filterExpression += " AND #status = :status";
            }

            if (type is not null)
            {
                expressionNames["#type"] = "type";
                expressionValues[":type"] = DynamoDbHelper.ToAttr(type);
                filterExpression += " AND #type = :type";
            }

            var response = await _client.ScanAsync(new ScanRequest
            {
                TableName = _options.TokensTableName,
                FilterExpression = filterExpression,
                ExpressionAttributeNames = expressionNames.Count == 0 ? null : expressionNames,
                ExpressionAttributeValues = expressionValues,
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            foreach (var item in response.Items ?? [])
            {
                yield return DynamoDbHelper.ToToken<TToken>(item);
            }

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);
    }

    private async Task<List<TToken>> ScanPruneCandidatesAsync(
        DateTimeOffset threshold,
        int segment,
        int totalSegments,
        CancellationToken cancellationToken)
    {
        var tokens = new List<TToken>();
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var response = await _client.ScanAsync(new ScanRequest
            {
                TableName = _options.TokensTableName,
                Segment = segment,
                TotalSegments = totalSegments,
                FilterExpression = "sk = :meta AND creation_date < :threshold AND (attribute_not_exists(#status) OR (#status <> :inactive AND #status <> :valid) OR expiration_date < :now)",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "status"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":meta"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSk),
                    [":threshold"] = DynamoDbHelper.ToAttr(threshold),
                    [":inactive"] = DynamoDbHelper.ToAttr("inactive"),
                    [":valid"] = DynamoDbHelper.ToAttr("valid"),
                    [":now"] = DynamoDbHelper.ToAttr(DateTimeOffset.UtcNow)
                },
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            tokens.AddRange((response.Items ?? []).Select(DynamoDbHelper.ToToken<TToken>));
            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null && tokens.Count < Math.Max(_options.PruneBatchSize, 0));

        return tokens;
    }

    private async Task FlushDeleteBatchAsync(List<WriteRequest> batch, CancellationToken cancellationToken)
    {
        var requestItems = new Dictionary<string, List<WriteRequest>>
        {
            [_options.TokensTableName] = batch
        };

        do
        {
            var response = await _client.BatchWriteItemAsync(new BatchWriteItemRequest
            {
                RequestItems = requestItems
            }, cancellationToken);

            requestItems = response.UnprocessedItems ?? [];
        }
        while (requestItems.Count > 0 && requestItems.Values.Any(static items => items.Count > 0));

        batch.Clear();
    }

    private async Task<bool> RevokeTokenAsync(string tokenId, CancellationToken cancellationToken)
    {
        try
        {
            await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _options.TokensTableName,
                Key = CreateKey(tokenId),
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

    private static string? CreateStatusTypePrefix(string? status, string? type)
    {
        if (status is null)
        {
            return null;
        }

        return type is null
            ? $"STATUS#{status}#"
            : $"STATUS#{status}#TYPE#{type ?? KeyHelper.NullSentinel}#";
    }

    private static bool Matches(TToken token, string? status, string? type)
    {
        if (status is not null && !string.Equals(token.Status, status, StringComparison.Ordinal))
        {
            return false;
        }

        if (type is not null && !string.Equals(token.Type, type, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool IsExpired(TToken token, DateTimeOffset now)
        => token.ExpirationDate.HasValue && token.ExpirationDate.Value < now;

    private static bool IsPrunable(TToken token)
        => !string.Equals(token.Status, "inactive", StringComparison.Ordinal) &&
           !string.Equals(token.Status, "valid", StringComparison.Ordinal) ||
           IsExpired(token, DateTimeOffset.UtcNow);

    private static long? GetTtl(DateTimeOffset? expirationDate)
        => expirationDate.HasValue ? expirationDate.Value.ToUnixTimeSeconds() : null;

    private static bool HasConditionalCheckFailure(TransactionCanceledException exception)
        => exception.CancellationReasons is null ||
           exception.CancellationReasons.Any(static reason => string.Equals(reason.Code, "ConditionalCheckFailed", StringComparison.Ordinal));

    private static OpenIddictExceptions.ConcurrencyException CreateConcurrencyException(Exception exception)
        => new("The token was concurrently updated and cannot be persisted in its current state.", exception);
}
