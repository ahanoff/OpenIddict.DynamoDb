using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Helpers;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb;

public class OpenIddictDynamoDbApplicationStore<TApplication> : IOpenIddictApplicationStore<TApplication>
    where TApplication : OpenIddictDynamoDbApplication, new()
{
    private readonly IAmazonDynamoDB _client;
    private readonly OpenIddictDynamoDbOptions _options;

    public OpenIddictDynamoDbApplicationStore(IAmazonDynamoDB client, OpenIddictDynamoDbOptions options)
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
                TableName = _options.ApplicationsTableName,
                Select = Select.COUNT,
                FilterExpression = "sk = :meta",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":meta"] = DynamoDbHelper.ToAttr(KeyHelper.ApplicationSk)
                },
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            count += response.Count ?? 0;
            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);

        return count;
    }

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<TApplication>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        => throw new NotSupportedException("DynamoDB does not support IQueryable-based queries.");

    public async ValueTask CreateAsync(TApplication application, CancellationToken cancellationToken)
    {
        var redirectUris = GetDistinctRedirectUris(application);
        var postLogoutRedirectUris = GetDistinctPostLogoutRedirectUris(application);
        ValidateRedirectUriCount(redirectUris.Count + postLogoutRedirectUris.Count);

        var items = new List<TransactWriteItem>
        {
            new()
            {
                Put = new Put
                {
                    TableName = _options.ApplicationsTableName,
                    Item = ToItem(application),
                    ConditionExpression = "attribute_not_exists(pk)"
                }
            }
        };

        if (application.ClientId is not null)
        {
            items.Add(new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = _options.ApplicationsTableName,
                    Item = CreateClientIdLookupItem(application.ClientId, application.Id),
                    ConditionExpression = "attribute_not_exists(pk)"
                }
            });
        }

        items.AddRange(redirectUris.Select(uri => new TransactWriteItem
        {
            Put = new Put { TableName = _options.ApplicationsTableName, Item = CreateRedirectLookupItem(uri, application.Id) }
        }));
        items.AddRange(postLogoutRedirectUris.Select(uri => new TransactWriteItem
        {
            Put = new Put { TableName = _options.ApplicationsTableName, Item = CreatePostLogoutRedirectLookupItem(uri, application.Id) }
        }));

        ValidateTransactionSize(items.Count);

        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                ClientRequestToken = Guid.NewGuid().ToString(),
                TransactItems = items
            }, cancellationToken);
        }
        catch (ConditionalCheckFailedException exception)
        {
            throw CreateConcurrencyException(exception);
        }
        catch (TransactionCanceledException exception) when (HasConditionalFailure(exception))
        {
            throw CreateConcurrencyException(exception);
        }
    }

    public async ValueTask DeleteAsync(TApplication application, CancellationToken cancellationToken)
    {
        var items = new List<TransactWriteItem>
        {
            new()
            {
                Delete = new Delete
                {
                    TableName = _options.ApplicationsTableName,
                    Key = CreateApplicationKey(application.Id),
                    ConditionExpression = "#token = :expected",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#token"] = "concurrency_token" },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":expected"] = DynamoDbHelper.ToAttr(application.ConcurrencyToken) }
                }
            }
        };

        if (application.ClientId is not null)
        {
            items.Add(new TransactWriteItem
            {
                Delete = new Delete
                {
                    TableName = _options.ApplicationsTableName,
                    Key = CreateClientIdLookupKey(application.ClientId),
                    ConditionExpression = "ApplicationId = :id",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":id"] = DynamoDbHelper.ToAttr(application.Id) }
                }
            });
        }

        items.AddRange(GetDistinctRedirectUris(application).Select(uri => new TransactWriteItem
        {
            Delete = new Delete { TableName = _options.ApplicationsTableName, Key = CreateRedirectLookupKey(uri, application.Id) }
        }));
        items.AddRange(GetDistinctPostLogoutRedirectUris(application).Select(uri => new TransactWriteItem
        {
            Delete = new Delete { TableName = _options.ApplicationsTableName, Key = CreatePostLogoutRedirectLookupKey(uri, application.Id) }
        }));

        try
        {
            foreach (var batch in Chunk(items, 100))
            {
                await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
                {
                    ClientRequestToken = Guid.NewGuid().ToString(),
                    TransactItems = batch
                }, cancellationToken);
            }
        }
        catch (ConditionalCheckFailedException exception)
        {
            throw CreateConcurrencyException(exception);
        }
        catch (TransactionCanceledException exception) when (HasConditionalFailure(exception))
        {
            throw CreateConcurrencyException(exception);
        }
    }

    public async ValueTask<TApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.ApplicationsTableName,
            Key = CreateApplicationKey(identifier),
            ConsistentRead = true
        }, cancellationToken);

        return response.Item is null || response.Item.Count == 0 ? null : DynamoDbHelper.ToApplication<TApplication>(response.Item);
    }

    public async ValueTask<TApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
    {
        var lookup = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.ApplicationsTableName,
            Key = CreateClientIdLookupKey(identifier),
            ConsistentRead = true
        }, cancellationToken);

        if (lookup.Item is null || lookup.Item.Count == 0)
        {
            return null;
        }

        var applicationId = DynamoDbHelper.GetString(lookup.Item, "ApplicationId");
        return applicationId is null ? null : await FindByIdAsync(applicationId, cancellationToken);
    }

    public IAsyncEnumerable<TApplication> FindByPostLogoutRedirectUriAsync(string uri, CancellationToken cancellationToken)
        => FindByUriAsync(uri, KeyHelper.PostLogoutRedirectPk(uri), cancellationToken);

    public IAsyncEnumerable<TApplication> FindByRedirectUriAsync(string uri, CancellationToken cancellationToken)
        => FindByUriAsync(uri, KeyHelper.RedirectPk(uri), cancellationToken);

    public ValueTask<string?> GetApplicationTypeAsync(TApplication application, CancellationToken cancellationToken)
        => new(application.ApplicationType);

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
        => throw new NotSupportedException("DynamoDB does not support IQueryable-based queries.");

    public ValueTask<string?> GetClientIdAsync(TApplication application, CancellationToken cancellationToken)
        => new(application.ClientId);

    public ValueTask<string?> GetClientSecretAsync(TApplication application, CancellationToken cancellationToken)
        => new(application.ClientSecret);

    public ValueTask<string?> GetClientTypeAsync(TApplication application, CancellationToken cancellationToken)
        => new(application.ClientType);

    public ValueTask<string?> GetConsentTypeAsync(TApplication application, CancellationToken cancellationToken)
        => new(application.ConsentType);

    public ValueTask<string?> GetDisplayNameAsync(TApplication application, CancellationToken cancellationToken)
        => new(application.DisplayName);

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TApplication application, CancellationToken cancellationToken)
        => new(JsonSerializationHelper.DeserializeCultureInfoDictionary(application.DisplayNames));

    public ValueTask<string?> GetIdAsync(TApplication application, CancellationToken cancellationToken)
        => new(application.Id);

    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(TApplication application, CancellationToken cancellationToken)
        => new(JsonSerializationHelper.DeserializeJsonWebKeySet(application.JsonWebKeySet));

    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(TApplication application, CancellationToken cancellationToken)
        => new(JsonSerializationHelper.DeserializeArray(application.Permissions));

    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(TApplication application, CancellationToken cancellationToken)
        => new(JsonSerializationHelper.DeserializeArray(application.PostLogoutRedirectUris));

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TApplication application, CancellationToken cancellationToken)
        => new(JsonSerializationHelper.DeserializeProperties(application.Properties));

    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(TApplication application, CancellationToken cancellationToken)
        => new(JsonSerializationHelper.DeserializeArray(application.RedirectUris));

    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(TApplication application, CancellationToken cancellationToken)
        => new(JsonSerializationHelper.DeserializeArray(application.Requirements));

    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(TApplication application, CancellationToken cancellationToken)
        => new(JsonSerializationHelper.DeserializeStringDictionary(application.Settings));

    public ValueTask<TApplication> InstantiateAsync(CancellationToken cancellationToken)
        => new(new TApplication { ConcurrencyToken = Guid.NewGuid().ToString() });

    public async IAsyncEnumerable<TApplication> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var skipped = 0;
        var yielded = 0;
        var skip = Math.Max(offset ?? 0, 0);
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var request = new ScanRequest
            {
                TableName = _options.ApplicationsTableName,
                FilterExpression = "sk = :meta",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":meta"] = DynamoDbHelper.ToAttr(KeyHelper.ApplicationSk)
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
                yield return DynamoDbHelper.ToApplication<TApplication>(item);
            }

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null && (!count.HasValue || yielded < count.Value));
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
        => throw new NotSupportedException("DynamoDB does not support IQueryable-based queries.");

    public ValueTask SetApplicationTypeAsync(TApplication application, string? type, CancellationToken cancellationToken)
    {
        application.ApplicationType = type;
        return default;
    }

    public ValueTask SetClientIdAsync(TApplication application, string? identifier, CancellationToken cancellationToken)
    {
        application.ClientId = identifier;
        return default;
    }

    public ValueTask SetClientSecretAsync(TApplication application, string? secret, CancellationToken cancellationToken)
    {
        application.ClientSecret = secret;
        return default;
    }

    public ValueTask SetClientTypeAsync(TApplication application, string? type, CancellationToken cancellationToken)
    {
        application.ClientType = type;
        return default;
    }

    public ValueTask SetConsentTypeAsync(TApplication application, string? type, CancellationToken cancellationToken)
    {
        application.ConsentType = type;
        return default;
    }

    public ValueTask SetDisplayNameAsync(TApplication application, string? name, CancellationToken cancellationToken)
    {
        application.DisplayName = name;
        return default;
    }

    public ValueTask SetDisplayNamesAsync(TApplication application, ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
    {
        application.DisplayNames = JsonSerializationHelper.SerializeCultureInfoDictionary(names);
        return default;
    }

    public ValueTask SetJsonWebKeySetAsync(TApplication application, JsonWebKeySet? set, CancellationToken cancellationToken)
    {
        application.JsonWebKeySet = JsonSerializationHelper.SerializeJsonWebKeySet(set);
        return default;
    }

    public ValueTask SetPermissionsAsync(TApplication application, ImmutableArray<string> permissions, CancellationToken cancellationToken)
    {
        application.Permissions = JsonSerializationHelper.SerializeArray(permissions);
        return default;
    }

    public ValueTask SetPostLogoutRedirectUrisAsync(TApplication application, ImmutableArray<string> uris, CancellationToken cancellationToken)
    {
        application.PostLogoutRedirectUris = JsonSerializationHelper.SerializeArray(uris);
        return default;
    }

    public ValueTask SetPropertiesAsync(TApplication application, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        application.Properties = JsonSerializationHelper.SerializeProperties(properties);
        return default;
    }

    public ValueTask SetRedirectUrisAsync(TApplication application, ImmutableArray<string> uris, CancellationToken cancellationToken)
    {
        application.RedirectUris = JsonSerializationHelper.SerializeArray(uris);
        return default;
    }

    public ValueTask SetRequirementsAsync(TApplication application, ImmutableArray<string> requirements, CancellationToken cancellationToken)
    {
        application.Requirements = JsonSerializationHelper.SerializeArray(requirements);
        return default;
    }

    public ValueTask SetSettingsAsync(TApplication application, ImmutableDictionary<string, string> settings, CancellationToken cancellationToken)
    {
        application.Settings = JsonSerializationHelper.SerializeDictionary(settings);
        return default;
    }

    public async ValueTask UpdateAsync(TApplication application, CancellationToken cancellationToken)
    {
        var existing = await FindByIdAsync(application.Id, cancellationToken);
        if (existing is null)
        {
            throw CreateConcurrencyException(null);
        }

        var oldRedirectUris = GetDistinctRedirectUris(existing).ToHashSet(StringComparer.Ordinal);
        var newRedirectUris = GetDistinctRedirectUris(application).ToHashSet(StringComparer.Ordinal);
        var oldPostLogoutRedirectUris = GetDistinctPostLogoutRedirectUris(existing).ToHashSet(StringComparer.Ordinal);
        var newPostLogoutRedirectUris = GetDistinctPostLogoutRedirectUris(application).ToHashSet(StringComparer.Ordinal);
        ValidateRedirectUriCount(newRedirectUris.Count + newPostLogoutRedirectUris.Count);

        var expected = application.ConcurrencyToken;
        application.ConcurrencyToken = Guid.NewGuid().ToString();

        var items = new List<TransactWriteItem>
        {
            new()
            {
                Put = new Put
                {
                    TableName = _options.ApplicationsTableName,
                    Item = ToItem(application),
                    ConditionExpression = "#token = :expected",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#token"] = "concurrency_token" },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":expected"] = DynamoDbHelper.ToAttr(expected) }
                }
            }
        };

        if (!string.Equals(existing.ClientId, application.ClientId, StringComparison.Ordinal))
        {
            if (existing.ClientId is not null)
            {
                items.Add(new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = _options.ApplicationsTableName,
                        Key = CreateClientIdLookupKey(existing.ClientId),
                        ConditionExpression = "ApplicationId = :id",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":id"] = DynamoDbHelper.ToAttr(application.Id) }
                    }
                });
            }

            if (application.ClientId is not null)
            {
                items.Add(new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _options.ApplicationsTableName,
                        Item = CreateClientIdLookupItem(application.ClientId, application.Id),
                        ConditionExpression = "attribute_not_exists(pk)"
                    }
                });
            }
        }

        items.AddRange(oldRedirectUris.Except(newRedirectUris, StringComparer.Ordinal).Select(uri => new TransactWriteItem
        {
            Delete = new Delete { TableName = _options.ApplicationsTableName, Key = CreateRedirectLookupKey(uri, application.Id) }
        }));
        items.AddRange(newRedirectUris.Except(oldRedirectUris, StringComparer.Ordinal).Select(uri => new TransactWriteItem
        {
            Put = new Put { TableName = _options.ApplicationsTableName, Item = CreateRedirectLookupItem(uri, application.Id) }
        }));
        items.AddRange(oldPostLogoutRedirectUris.Except(newPostLogoutRedirectUris, StringComparer.Ordinal).Select(uri => new TransactWriteItem
        {
            Delete = new Delete { TableName = _options.ApplicationsTableName, Key = CreatePostLogoutRedirectLookupKey(uri, application.Id) }
        }));
        items.AddRange(newPostLogoutRedirectUris.Except(oldPostLogoutRedirectUris, StringComparer.Ordinal).Select(uri => new TransactWriteItem
        {
            Put = new Put { TableName = _options.ApplicationsTableName, Item = CreatePostLogoutRedirectLookupItem(uri, application.Id) }
        }));

        try
        {
            foreach (var batch in Chunk(items, 100))
            {
                await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
                {
                    ClientRequestToken = Guid.NewGuid().ToString(),
                    TransactItems = batch
                }, cancellationToken);
            }
        }
        catch (ConditionalCheckFailedException exception)
        {
            application.ConcurrencyToken = expected;
            throw CreateConcurrencyException(exception);
        }
        catch (TransactionCanceledException exception) when (HasConditionalFailure(exception))
        {
            application.ConcurrencyToken = expected;
            throw CreateConcurrencyException(exception);
        }
    }

    private Dictionary<string, AttributeValue> ToItem(TApplication application)
    {
        var attributes = DynamoDbHelper.ToAttributes(application);
        attributes["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ApplicationPk(application.Id));
        attributes["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ApplicationSk);
        return attributes;
    }

    private async IAsyncEnumerable<TApplication> FindByUriAsync(string uri, string pk, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var applicationIds = new List<string>();
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var response = await _client.QueryAsync(new QueryRequest
            {
                TableName = _options.ApplicationsTableName,
                KeyConditionExpression = "pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = DynamoDbHelper.ToAttr(pk) },
                ConsistentRead = true,
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            applicationIds.AddRange((response.Items ?? [])
                .Where(item => string.Equals(DynamoDbHelper.GetString(item, "OriginalUri"), uri, StringComparison.Ordinal))
                .Select(item => DynamoDbHelper.GetString(item, "sk"))
                .Where(sk => sk is not null)
                .Select(sk => KeyHelper.ExtractId(sk!, "APP#")));

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);

        var applicationKeys = applicationIds.Distinct(StringComparer.Ordinal).Select(CreateApplicationKey).ToList();
        foreach (var item in await BatchGetAsync(applicationKeys, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return DynamoDbHelper.ToApplication<TApplication>(item);
        }
    }

    private async Task<List<Dictionary<string, AttributeValue>>> BatchGetAsync(List<Dictionary<string, AttributeValue>> keys, CancellationToken cancellationToken)
    {
        var items = new List<Dictionary<string, AttributeValue>>();
        foreach (var batch in Chunk(keys, 100))
        {
            var requestItems = new Dictionary<string, KeysAndAttributes>
            {
                [_options.ApplicationsTableName] = new() { ConsistentRead = true, Keys = batch }
            };

            do
            {
                var response = await _client.BatchGetItemAsync(new BatchGetItemRequest { RequestItems = requestItems }, cancellationToken);
                if ((response.Responses ?? []).TryGetValue(_options.ApplicationsTableName, out var responseItems))
                {
                    items.AddRange(responseItems);
                }

                requestItems = response.UnprocessedKeys ?? [];
            }
            while (requestItems.Count > 0 && requestItems.Values.Any(value => value.Keys.Count > 0));
        }

        return items;
    }

    private static Dictionary<string, AttributeValue> CreateApplicationKey(string identifier)
        => new()
        {
            ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ApplicationPk(identifier)),
            ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ApplicationSk)
        };

    private static Dictionary<string, AttributeValue> CreateClientIdLookupKey(string clientId)
        => new()
        {
            ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ClientIdPk(clientId)),
            ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ClientIdSk)
        };

    private static Dictionary<string, AttributeValue> CreateClientIdLookupItem(string clientId, string applicationId)
    {
        var item = CreateClientIdLookupKey(clientId);
        item["ApplicationId"] = DynamoDbHelper.ToAttr(applicationId);
        return item;
    }

    private static Dictionary<string, AttributeValue> CreateRedirectLookupKey(string uri, string applicationId)
        => new()
        {
            ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.RedirectPk(uri)),
            ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.RedirectSk(applicationId))
        };

    private static Dictionary<string, AttributeValue> CreateRedirectLookupItem(string uri, string applicationId)
    {
        var item = CreateRedirectLookupKey(uri, applicationId);
        item["OriginalUri"] = DynamoDbHelper.ToAttr(uri);
        return item;
    }

    private static Dictionary<string, AttributeValue> CreatePostLogoutRedirectLookupKey(string uri, string applicationId)
        => new()
        {
            ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.PostLogoutRedirectPk(uri)),
            ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.PostLogoutRedirectSk(applicationId))
        };

    private static Dictionary<string, AttributeValue> CreatePostLogoutRedirectLookupItem(string uri, string applicationId)
    {
        var item = CreatePostLogoutRedirectLookupKey(uri, applicationId);
        item["OriginalUri"] = DynamoDbHelper.ToAttr(uri);
        return item;
    }

    private static IReadOnlyList<string> GetDistinctRedirectUris(TApplication application)
        => JsonSerializationHelper.DeserializeArray(application.RedirectUris).Where(uri => !string.IsNullOrEmpty(uri)).Distinct(StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<string> GetDistinctPostLogoutRedirectUris(TApplication application)
        => JsonSerializationHelper.DeserializeArray(application.PostLogoutRedirectUris).Where(uri => !string.IsNullOrEmpty(uri)).Distinct(StringComparer.Ordinal).ToArray();

    private void ValidateRedirectUriCount(int count)
    {
        if (count > _options.MaxRedirectUrisPerApplication)
        {
            throw new InvalidOperationException($"The application cannot reference more than {_options.MaxRedirectUrisPerApplication} redirect URIs.");
        }
    }

    private static void ValidateTransactionSize(int count)
    {
        if (count > 100)
        {
            throw new InvalidOperationException("The application operation exceeds DynamoDB's 100-item transaction limit.");
        }
    }

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> items, int size)
    {
        for (var index = 0; index < items.Count; index += size)
        {
            var batch = new List<T>(Math.Min(size, items.Count - index));
            for (var offset = 0; offset < size && index + offset < items.Count; offset++)
            {
                batch.Add(items[index + offset]);
            }

            yield return batch;
        }
    }

    private static bool HasConditionalFailure(TransactionCanceledException exception)
        => exception.CancellationReasons is null ||
           exception.CancellationReasons.Count == 0 ||
           exception.CancellationReasons.Any(static reason => string.Equals(reason.Code, "ConditionalCheckFailed", StringComparison.Ordinal));

    private static OpenIddictExceptions.ConcurrencyException CreateConcurrencyException(Exception? exception)
        => new("The application was concurrently updated and cannot be persisted in its current state.", exception);
}
