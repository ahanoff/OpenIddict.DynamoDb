using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Helpers;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb;

public class OpenIddictDynamoDbScopeStore<TScope> : IOpenIddictScopeStore<TScope>
    where TScope : OpenIddictDynamoDbScope, new()
{
    private readonly IAmazonDynamoDB _client;
    private readonly OpenIddictDynamoDbOptions _options;

    public OpenIddictDynamoDbScopeStore(IAmazonDynamoDB client, OpenIddictDynamoDbOptions options)
    {
        _client = client;
        options.Validate();
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
                TableName = _options.ScopesTableName,
                Select = Select.COUNT,
                FilterExpression = "sk = :meta",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":meta"] = DynamoDbHelper.ToAttr(KeyHelper.ScopeSk) },
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            count += response.Count ?? 0;
            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);

        return count;
    }

    public async ValueTask<long> CountAsync<TResult>(Func<IQueryable<TScope>, IQueryable<TResult>> query, CancellationToken cancellationToken)
    {
        var scopes = await ListAsync(null, null, cancellationToken).ToListAsync(cancellationToken);
        return query(scopes.AsQueryable()).LongCount();
    }

    public async ValueTask CreateAsync(TScope scope, CancellationToken cancellationToken)
    {
        var resources = GetDistinctResources(scope);
        ValidateResourceCount(resources);
        var items = new List<TransactWriteItem>
        {
            new() { Put = new Put { TableName = _options.ScopesTableName, Item = ToItem(scope), ConditionExpression = "attribute_not_exists(pk)" } }
        };

        if (scope.Name is not null)
        {
            items.Add(new TransactWriteItem { Put = new Put { TableName = _options.ScopesTableName, Item = CreateNameLookupItem(scope.Name, scope.Id), ConditionExpression = "attribute_not_exists(pk)" } });
        }

        items.AddRange(resources.Select(resource => new TransactWriteItem { Put = new Put { TableName = _options.ScopesTableName, Item = CreateResourceLookupItem(resource, scope.Id) } }));
        ValidateTransactionSize(items.Count);

        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest { ClientRequestToken = Guid.NewGuid().ToString(), TransactItems = items }, cancellationToken);
        }
        catch (ConditionalCheckFailedException exception)
        {
            throw CreateDuplicateNameException(scope.Name, exception);
        }
        catch (TransactionCanceledException exception) when (HasConditionalFailure(exception))
        {
            throw CreateDuplicateNameException(scope.Name, exception);
        }
    }

    public async ValueTask DeleteAsync(TScope scope, CancellationToken cancellationToken)
    {
        var resources = GetDistinctResources(scope);
        ValidateResourceCount(resources);
        var items = new List<TransactWriteItem>
        {
            new()
            {
                Delete = new Delete
                {
                    TableName = _options.ScopesTableName,
                    Key = CreateScopeKey(scope.Id),
                    ConditionExpression = "#token = :expected",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#token"] = "concurrency_token" },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":expected"] = DynamoDbHelper.ToAttr(scope.ConcurrencyToken) }
                }
            }
        };

        if (scope.Name is not null)
        {
            items.Add(new TransactWriteItem
            {
                Delete = new Delete
                {
                    TableName = _options.ScopesTableName,
                    Key = CreateNameLookupKey(scope.Name),
                    ConditionExpression = "scope_id = :id",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":id"] = DynamoDbHelper.ToAttr(scope.Id) }
                }
            });
        }

        items.AddRange(resources.Select(resource => new TransactWriteItem { Delete = new Delete { TableName = _options.ScopesTableName, Key = CreateResourceLookupKey(resource, scope.Id) } }));
        ValidateTransactionSize(items.Count);

        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest { ClientRequestToken = Guid.NewGuid().ToString(), TransactItems = items }, cancellationToken);
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

    public async ValueTask<TScope?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest { TableName = _options.ScopesTableName, Key = CreateScopeKey(identifier), ConsistentRead = true }, cancellationToken);
        return response.Item is null || response.Item.Count == 0 ? null : DynamoDbHelper.ToScope<TScope>(response.Item);
    }

    public async ValueTask<TScope?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        var lookup = await _client.GetItemAsync(new GetItemRequest { TableName = _options.ScopesTableName, Key = CreateNameLookupKey(name), ConsistentRead = true }, cancellationToken);
        if (lookup.Item is null || lookup.Item.Count == 0)
        {
            return null;
        }

        var scopeId = DynamoDbHelper.GetString(lookup.Item, "scope_id");
        if (scopeId is null || !string.Equals(DynamoDbHelper.GetString(lookup.Item, "name"), name, StringComparison.Ordinal))
        {
            return null;
        }

        var scope = await FindByIdAsync(scopeId, cancellationToken);
        return string.Equals(scope?.Name, name, StringComparison.Ordinal) ? scope : null;
    }

    public async IAsyncEnumerable<TScope> FindByNamesAsync(ImmutableArray<string> names, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (names.IsDefaultOrEmpty) yield break;

        var requestedNames = names.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var lookups = await BatchGetAsync(requestedNames.Select(CreateNameLookupKey).ToList(), cancellationToken);
        lookups = lookups
            .Where(item => DynamoDbHelper.GetString(item, "name") is string name && requestedNames.Contains(name))
            .ToList();
        var scopeKeys = lookups.Select(item => DynamoDbHelper.GetString(item, "scope_id")).Where(id => id is not null).Distinct(StringComparer.Ordinal).Select(id => CreateScopeKey(id!)).ToList();

        foreach (var item in await BatchGetAsync(scopeKeys, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scope = DynamoDbHelper.ToScope<TScope>(item);
            if (scope.Name is not null && requestedNames.Contains(scope.Name))
            {
                yield return scope;
            }
        }
    }

    public async IAsyncEnumerable<TScope> FindByResourceAsync(string resource, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var scopeIds = new List<string>();
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var response = await _client.QueryAsync(new QueryRequest
            {
                TableName = _options.ScopesTableName,
                KeyConditionExpression = "pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = DynamoDbHelper.ToAttr(KeyHelper.ScopeResourcePk(resource)) },
                ConsistentRead = true,
                ExclusiveStartKey = lastKey
            }, cancellationToken);

            scopeIds.AddRange((response.Items ?? [])
                .Where(item => string.Equals(DynamoDbHelper.GetString(item, "resource"), resource, StringComparison.Ordinal))
                .Select(item => DynamoDbHelper.GetString(item, "sk"))
                .Where(sk => sk is not null)
                .Select(sk => KeyHelper.ExtractId(sk!, "SCOPE#")));
            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null);

        var scopeKeys = scopeIds.Distinct(StringComparer.Ordinal).Select(CreateScopeKey).ToList();
        foreach (var item in await BatchGetAsync(scopeKeys, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scope = DynamoDbHelper.ToScope<TScope>(item);
            if (GetDistinctResources(scope).Contains(resource, StringComparer.Ordinal))
            {
                yield return scope;
            }
        }
    }

    public async ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<TScope>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
    {
        var scopes = await ListAsync(null, null, cancellationToken).ToListAsync(cancellationToken);
        return query(scopes.AsQueryable(), state).FirstOrDefault();
    }

    public ValueTask<string?> GetDescriptionAsync(TScope scope, CancellationToken cancellationToken) => new(scope.Description);
    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(TScope scope, CancellationToken cancellationToken) => new(JsonSerializationHelper.DeserializeCultureInfoDictionary(scope.Descriptions));
    public ValueTask<string?> GetDisplayNameAsync(TScope scope, CancellationToken cancellationToken) => new(scope.DisplayName);
    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TScope scope, CancellationToken cancellationToken) => new(JsonSerializationHelper.DeserializeCultureInfoDictionary(scope.DisplayNames));
    public ValueTask<string?> GetIdAsync(TScope scope, CancellationToken cancellationToken) => new(scope.Id);
    public ValueTask<string?> GetNameAsync(TScope scope, CancellationToken cancellationToken) => new(scope.Name);
    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TScope scope, CancellationToken cancellationToken) => new(JsonSerializationHelper.DeserializeProperties(scope.Properties));
    public ValueTask<ImmutableArray<string>> GetResourcesAsync(TScope scope, CancellationToken cancellationToken) => new(JsonSerializationHelper.DeserializeArray(scope.Resources));
    public ValueTask<TScope> InstantiateAsync(CancellationToken cancellationToken) => new(new TScope { ConcurrencyToken = Guid.NewGuid().ToString() });

    public async IAsyncEnumerable<TScope> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var skipped = 0;
        var yielded = 0;
        var skip = Math.Max(offset ?? 0, 0);
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var request = new ScanRequest
            {
                TableName = _options.ScopesTableName,
                FilterExpression = "sk = :meta",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":meta"] = DynamoDbHelper.ToAttr(KeyHelper.ScopeSk) },
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

                if (count.HasValue && yielded >= count.Value) yield break;

                yielded++;
                yield return DynamoDbHelper.ToScope<TScope>(item);
            }

            lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (lastKey is not null && (!count.HasValue || yielded < count.Value));
    }

    public async IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<TScope>, TState, IQueryable<TResult>> query, TState state, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var scopes = await ListAsync(null, null, cancellationToken).ToListAsync(cancellationToken);
        foreach (var result in query(scopes.AsQueryable(), state))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return result;
        }
    }

    public ValueTask SetDescriptionAsync(TScope scope, string? description, CancellationToken cancellationToken) { scope.Description = description; return default; }
    public ValueTask SetDescriptionsAsync(TScope scope, ImmutableDictionary<CultureInfo, string> descriptions, CancellationToken cancellationToken) { scope.Descriptions = JsonSerializationHelper.SerializeCultureInfoDictionary(descriptions); return default; }
    public ValueTask SetDisplayNameAsync(TScope scope, string? name, CancellationToken cancellationToken) { scope.DisplayName = name; return default; }
    public ValueTask SetDisplayNamesAsync(TScope scope, ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken) { scope.DisplayNames = JsonSerializationHelper.SerializeCultureInfoDictionary(names); return default; }
    public ValueTask SetNameAsync(TScope scope, string? name, CancellationToken cancellationToken) { scope.Name = name; return default; }
    public ValueTask SetPropertiesAsync(TScope scope, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken) { scope.Properties = JsonSerializationHelper.SerializeProperties(properties); return default; }
    public ValueTask SetResourcesAsync(TScope scope, ImmutableArray<string> resources, CancellationToken cancellationToken) { scope.Resources = JsonSerializationHelper.SerializeArray(resources); return default; }

    public async ValueTask UpdateAsync(TScope scope, CancellationToken cancellationToken)
    {
        var existing = await FindByIdAsync(scope.Id, cancellationToken);
        if (existing is null) throw CreateConcurrencyException(null);

        var oldResources = GetDistinctResources(existing).ToHashSet(StringComparer.Ordinal);
        var newResources = GetDistinctResources(scope).ToHashSet(StringComparer.Ordinal);
        ValidateResourceCount(newResources);

        var expected = scope.ConcurrencyToken;
        scope.ConcurrencyToken = Guid.NewGuid().ToString();
        var items = new List<TransactWriteItem>
        {
            new()
            {
                Put = new Put
                {
                    TableName = _options.ScopesTableName,
                    Item = ToItem(scope),
                    ConditionExpression = "#token = :expected",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#token"] = "concurrency_token" },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":expected"] = DynamoDbHelper.ToAttr(expected) }
                }
            }
        };

        if (!string.Equals(existing.Name, scope.Name, StringComparison.Ordinal))
        {
            if (existing.Name is not null)
            {
                items.Add(new TransactWriteItem { Delete = new Delete { TableName = _options.ScopesTableName, Key = CreateNameLookupKey(existing.Name), ConditionExpression = "scope_id = :id", ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":id"] = DynamoDbHelper.ToAttr(scope.Id) } } });
            }

            if (scope.Name is not null)
            {
                items.Add(new TransactWriteItem { Put = new Put { TableName = _options.ScopesTableName, Item = CreateNameLookupItem(scope.Name, scope.Id), ConditionExpression = "attribute_not_exists(pk)" } });
            }
        }

        items.AddRange(oldResources.Except(newResources, StringComparer.Ordinal).Select(resource => new TransactWriteItem { Delete = new Delete { TableName = _options.ScopesTableName, Key = CreateResourceLookupKey(resource, scope.Id), ConditionExpression = "scope_id = :id", ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":id"] = DynamoDbHelper.ToAttr(scope.Id) } } }));
        items.AddRange(newResources.Except(oldResources, StringComparer.Ordinal).Select(resource => new TransactWriteItem { Put = new Put { TableName = _options.ScopesTableName, Item = CreateResourceLookupItem(resource, scope.Id) } }));
        ValidateTransactionSize(items.Count);

        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest { ClientRequestToken = Guid.NewGuid().ToString(), TransactItems = items }, cancellationToken);
        }
        catch (ConditionalCheckFailedException exception)
        {
            scope.ConcurrencyToken = expected;
            throw CreateConcurrencyException(exception);
        }
        catch (TransactionCanceledException exception) when (HasConditionalFailure(exception))
        {
            scope.ConcurrencyToken = expected;
            throw CreateConcurrencyException(exception);
        }
        catch
        {
            scope.ConcurrencyToken = expected;
            throw;
        }
    }

    private Dictionary<string, AttributeValue> ToItem(TScope scope)
    {
        var attributes = DynamoDbHelper.ToAttributes(scope);
        attributes["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ScopePk(scope.Id));
        attributes["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ScopeSk);
        return attributes;
    }

    private static Dictionary<string, AttributeValue> CreateScopeKey(string identifier) => new() { ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ScopePk(identifier)), ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ScopeSk) };
    private static Dictionary<string, AttributeValue> CreateNameLookupKey(string name) => new() { ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ScopeNamePk(name)), ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ScopeNameSk) };
    private static Dictionary<string, AttributeValue> CreateNameLookupItem(string name, string scopeId) { var item = CreateNameLookupKey(name); item["scope_id"] = DynamoDbHelper.ToAttr(scopeId); item["name"] = DynamoDbHelper.ToAttr(name); return item; }
    private static Dictionary<string, AttributeValue> CreateResourceLookupKey(string resource, string scopeId) => new() { ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ScopeResourcePk(resource)), ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ScopeResourceSk(scopeId)) };
    private static Dictionary<string, AttributeValue> CreateResourceLookupItem(string resource, string scopeId) { var item = CreateResourceLookupKey(resource, scopeId); item["scope_id"] = DynamoDbHelper.ToAttr(scopeId); item["resource"] = DynamoDbHelper.ToAttr(resource); return item; }

    private async Task<List<Dictionary<string, AttributeValue>>> BatchGetAsync(List<Dictionary<string, AttributeValue>> keys, CancellationToken cancellationToken)
        => await DynamoDbBatchHelper.GetItemsAsync(_client, _options.ScopesTableName, keys, cancellationToken);

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> items, int size)
    {
        for (var index = 0; index < items.Count; index += size)
        {
            var batch = new List<T>(Math.Min(size, items.Count - index));
            for (var offset = 0; offset < size && index + offset < items.Count; offset++) batch.Add(items[index + offset]);
            yield return batch;
        }
    }

    private static IReadOnlyList<string> GetDistinctResources(TScope scope)
        => JsonSerializationHelper.DeserializeArray(scope.Resources).Where(resource => !string.IsNullOrEmpty(resource)).Distinct(StringComparer.Ordinal).ToArray();

    private void ValidateResourceCount(IReadOnlyCollection<string> resources)
    {
        if (resources.Count > _options.MaxResourcesPerScope) throw new InvalidOperationException($"The scope cannot reference more than {_options.MaxResourcesPerScope} resources.");
    }

    private static void ValidateTransactionSize(int count)
    {
        if (count > 100) throw new InvalidOperationException("The scope operation exceeds DynamoDB's 100-item transaction limit.");
    }

    private static bool HasConditionalFailure(TransactionCanceledException exception)
        => exception.CancellationReasons is null || exception.CancellationReasons.Count == 0 || exception.CancellationReasons.Any(reason => string.Equals(reason.Code, "ConditionalCheckFailed", StringComparison.Ordinal));

    private static InvalidOperationException CreateDuplicateNameException(string? name, Exception exception)
        => new($"A scope with the name '{name}' already exists.", exception);

    private static OpenIddictExceptions.ConcurrencyException CreateConcurrencyException(Exception? exception)
        => new("The scope was concurrently updated and cannot be persisted in its current state.", exception);
}
