using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;
using OpenIddict.DynamoDb.Tests.Fixtures;
using OpenIddict.DynamoDb.Tests.Helpers;

namespace OpenIddict.DynamoDb.Tests.Stores;

[Collection(DynamoDbCollection.Name)]
public sealed class ScopeStoreTests
{
    private readonly OpenIddictDynamoDbScopeStore<OpenIddictDynamoDbScope> _store;
    private readonly DynamoDbFixture _fixture;

    public ScopeStoreTests(DynamoDbFixture fixture)
    {
        _store = new OpenIddictDynamoDbScopeStore<OpenIddictDynamoDbScope>(fixture.Client, fixture.Options);
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAsync_StoresScope()
    {
        var scope = TestEntities.CreateScope();

        await _store.CreateAsync(scope, CancellationToken.None);

        var stored = await _store.FindByIdAsync(scope.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(scope.Name, stored.Name);
        Assert.Equal(scope.Resources, stored.Resources);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsScope()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        var stored = await _store.FindByIdAsync(scope.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(scope.Id, stored.Id);
    }

    [Fact]
    public async Task FindByNameAsync_ReturnsScope()
    {
        var scope = TestEntities.CreateScope(name: TestEntities.NewId("scope-name"));
        await _store.CreateAsync(scope, CancellationToken.None);

        var stored = await _store.FindByNameAsync(scope.Name!, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(scope.Id, stored.Id);
    }

    [Fact]
    public async Task UpdateAsync_PersistsDisplayName()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        scope.DisplayName = "Updated scope";
        await _store.UpdateAsync(scope, CancellationToken.None);

        var stored = await _store.FindByIdAsync(scope.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Updated scope", stored.DisplayName);
    }

    [Fact]
    public async Task DeleteAsync_RemovesScope()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        await _store.DeleteAsync(scope, CancellationToken.None);

        var stored = await _store.FindByIdAsync(scope.Id, CancellationToken.None);
        Assert.Null(stored);
    }

    [Fact]
    public async Task ListAsync_ReturnsCreatedScopes()
    {
        var first = TestEntities.CreateScope();
        var second = TestEntities.CreateScope();
        await _store.CreateAsync(first, CancellationToken.None);
        await _store.CreateAsync(second, CancellationToken.None);

        var scopes = await _store.ListAsync(count: null, offset: null, CancellationToken.None).ToListAsync();

        Assert.Contains(scopes, scope => scope.Id == first.Id);
        Assert.Contains(scopes, scope => scope.Id == second.Id);
    }
    [Fact]
    public async Task FindByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _store.FindByIdAsync("nonexistent-id", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByNameAsync_NotFound_ReturnsNull()
    {
        var result = await _store.FindByNameAsync("nonexistent-name", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CountAsync_EmptyTable_ReturnsZero()
    {
        var store = await CreateEmptyStoreAsync();

        var count = await store.CountAsync(CancellationToken.None);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ListAsync_EmptyTable_ReturnsEmpty()
    {
        var store = await CreateEmptyStoreAsync();

        var scopes = await store.ListAsync(count: null, offset: null, CancellationToken.None).ToListAsync();

        Assert.Empty(scopes);
    }

    [Fact]
    public async Task UpdateAsync_StaleConcurrencyToken_Throws()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        var originalToken = scope.ConcurrencyToken;

        scope.DisplayName = "Updated scope";
        await _store.UpdateAsync(scope, CancellationToken.None);

        scope.ConcurrencyToken = originalToken;
        scope.DisplayName = "Should fail";
        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.UpdateAsync(scope, CancellationToken.None).AsTask());

        AssertDynamoDbConcurrencyFailure(exception);
    }

    [Fact]
    public async Task DeleteAsync_StaleConcurrencyToken_Throws()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        var originalToken = scope.ConcurrencyToken;

        scope.DisplayName = "Updated scope";
        await _store.UpdateAsync(scope, CancellationToken.None);

        scope.ConcurrencyToken = originalToken;
        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.DeleteAsync(scope, CancellationToken.None).AsTask());

        AssertDynamoDbConcurrencyFailure(exception);
    }


    [Fact]
    public async Task FindByNamesAsync_ReturnsMatchingScopes()
    {
        var name1 = TestEntities.NewId("scope-name");
        var name2 = TestEntities.NewId("scope-name");
        var scope1 = TestEntities.CreateScope(name: name1);
        var scope2 = TestEntities.CreateScope(name: name2);
        var scope3 = TestEntities.CreateScope();
        await _store.CreateAsync(scope1, CancellationToken.None);
        await _store.CreateAsync(scope2, CancellationToken.None);
        await _store.CreateAsync(scope3, CancellationToken.None);

        var results = await _store.FindByNamesAsync(ImmutableArray.Create(name1, name2), CancellationToken.None).ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, s => s.Id == scope1.Id);
        Assert.Contains(results, s => s.Id == scope2.Id);
    }

    [Fact]
    public async Task FindByResourceAsync_ReturnsMatchingScopes()
    {
        var resource = $"resource-{Guid.NewGuid():N}";
        var scope = TestEntities.CreateScope();
        scope.Resources = TestEntities.JsonArray(resource);
        await _store.CreateAsync(scope, CancellationToken.None);

        var results = await _store.FindByResourceAsync(resource, CancellationToken.None).ToListAsync();

        Assert.Single(results);
        Assert.Equal(scope.Id, results[0].Id);
    }

    [Fact]
    public async Task InstantiateAsync_ReturnsNewInstance()
    {
        var scope = await _store.InstantiateAsync(CancellationToken.None);
        Assert.NotNull(scope);
        Assert.NotNull(scope.ConcurrencyToken);
        Assert.NotEmpty(scope.ConcurrencyToken);
    }

    [Fact]
    public async Task GetDescriptionAsync_ReturnsDescription()
    {
        var scope = TestEntities.CreateScope();
        var result = await _store.GetDescriptionAsync(scope, CancellationToken.None);
        Assert.Equal(scope.Description, result);
    }

    [Fact]
    public async Task GetDescriptionsAsync_ReturnsDeserializedDictionary()
    {
        var scope = TestEntities.CreateScope();
        var result = await _store.GetDescriptionsAsync(scope, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetDisplayNameAsync_ReturnsDisplayName()
    {
        var scope = TestEntities.CreateScope();
        var result = await _store.GetDisplayNameAsync(scope, CancellationToken.None);
        Assert.Equal(scope.DisplayName, result);
    }

    [Fact]
    public async Task GetDisplayNamesAsync_ReturnsDeserializedDictionary()
    {
        var scope = TestEntities.CreateScope();
        var result = await _store.GetDisplayNamesAsync(scope, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetIdAsync_ReturnsId()
    {
        var scope = TestEntities.CreateScope();
        var result = await _store.GetIdAsync(scope, CancellationToken.None);
        Assert.Equal(scope.Id, result);
    }

    [Fact]
    public async Task GetNameAsync_ReturnsName()
    {
        var scope = TestEntities.CreateScope();
        var result = await _store.GetNameAsync(scope, CancellationToken.None);
        Assert.Equal(scope.Name, result);
    }

    [Fact]
    public async Task GetPropertiesAsync_ReturnsDeserializedDictionary()
    {
        var scope = TestEntities.CreateScope();
        var result = await _store.GetPropertiesAsync(scope, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetResourcesAsync_ReturnsDeserializedArray()
    {
        var scope = TestEntities.CreateScope();
        var result = await _store.GetResourcesAsync(scope, CancellationToken.None);
        Assert.False(result.IsDefaultOrEmpty);
    }

    [Fact]
    public async Task SetDescriptionAsync_UpdatesDescription()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        await _store.SetDescriptionAsync(scope, "New description", CancellationToken.None);
        await _store.UpdateAsync(scope, CancellationToken.None);

        var stored = await _store.FindByIdAsync(scope.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("New description", stored.Description);
    }

    [Fact]
    public async Task SetDescriptionsAsync_UpdatesDescriptions()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        var descriptions = ImmutableDictionary.CreateBuilder<CultureInfo, string>();
        descriptions[CultureInfo.GetCultureInfo("en")] = "English Description";
        await _store.SetDescriptionsAsync(scope, descriptions.ToImmutable(), CancellationToken.None);
        await _store.UpdateAsync(scope, CancellationToken.None);

        var stored = await _store.FindByIdAsync(scope.Id, CancellationToken.None);
        Assert.NotNull(stored);
        var result = await _store.GetDescriptionsAsync(stored, CancellationToken.None);
        Assert.Equal("English Description", result[CultureInfo.GetCultureInfo("en")]);
    }

    [Fact]
    public async Task SetDisplayNameAsync_UpdatesDisplayName()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        await _store.SetDisplayNameAsync(scope, "New Display Name", CancellationToken.None);
        await _store.UpdateAsync(scope, CancellationToken.None);

        var stored = await _store.FindByIdAsync(scope.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("New Display Name", stored.DisplayName);
    }

    [Fact]
    public async Task SetDisplayNamesAsync_UpdatesDisplayNames()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        var names = ImmutableDictionary.CreateBuilder<CultureInfo, string>();
        names[CultureInfo.GetCultureInfo("en")] = "English Name";
        names[CultureInfo.GetCultureInfo("de")] = "German Name";
        await _store.SetDisplayNamesAsync(scope, names.ToImmutable(), CancellationToken.None);
        await _store.UpdateAsync(scope, CancellationToken.None);

        var stored = await _store.FindByIdAsync(scope.Id, CancellationToken.None);
        Assert.NotNull(stored);
        var result = await _store.GetDisplayNamesAsync(stored, CancellationToken.None);
        Assert.Equal("English Name", result[CultureInfo.GetCultureInfo("en")]);
        Assert.Equal("German Name", result[CultureInfo.GetCultureInfo("de")]);
    }

    [Fact]
    public async Task SetNameAsync_UpdatesName()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        var newName = TestEntities.NewId("new-scope-name");
        await _store.SetNameAsync(scope, newName, CancellationToken.None);
        await _store.UpdateAsync(scope, CancellationToken.None);

        var stored = await _store.FindByIdAsync(scope.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newName, stored.Name);
    }

    [Fact]
    public async Task SetPropertiesAsync_UpdatesProperties()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        using var doc = JsonDocument.Parse("{\"NewProp\":\"NewVal\"}");
        var properties = ImmutableDictionary.CreateRange(new[] { KeyValuePair.Create("NewProp", doc.RootElement) });
        await _store.SetPropertiesAsync(scope, properties, CancellationToken.None);
        await _store.UpdateAsync(scope, CancellationToken.None);

        var stored = await _store.FindByIdAsync(scope.Id, CancellationToken.None);
        Assert.NotNull(stored);
        var result = await _store.GetPropertiesAsync(stored, CancellationToken.None);
        Assert.True(result.ContainsKey("NewProp"));
    }

    [Fact]
    public async Task SetResourcesAsync_UpdatesResources()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        var newResources = ImmutableArray.Create($"resource-{Guid.NewGuid():N}", $"resource-{Guid.NewGuid():N}");
        await _store.SetResourcesAsync(scope, newResources, CancellationToken.None);
        await _store.UpdateAsync(scope, CancellationToken.None);

        var stored = await _store.FindByIdAsync(scope.Id, CancellationToken.None);
        Assert.NotNull(stored);
        var result = await _store.GetResourcesAsync(stored, CancellationToken.None);
        Assert.Equal(newResources, result);
    }

    [Fact]
    public async Task CountAsync_WithIQueryable_ThrowsNotSupportedException()
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => _store.CountAsync<OpenIddictDynamoDbScope>(q => q, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task GetAsync_ThrowsNotSupportedException()
    {
        var scope = TestEntities.CreateScope();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => _store.GetAsync<object, string>((q, s) => q.Select(_ => _.Id), "state", CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task ListAsync_WithIQueryable_ThrowsNotSupportedException()
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => _store.ListAsync<object, string>((q, s) => q.Select(_ => _.Id), "state", CancellationToken.None).ToListAsync());
    }

    [Fact]
    public async Task FindByNamesAsync_EmptyArray_ReturnsEmpty()
    {
        var scope = TestEntities.CreateScope();
        await _store.CreateAsync(scope, CancellationToken.None);

        var results = await _store.FindByNamesAsync(ImmutableArray<string>.Empty, CancellationToken.None).ToListAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindByResourceAsync_NotFound_ReturnsEmpty()
    {
        var results = await _store.FindByResourceAsync("nonexistent-resource", CancellationToken.None).ToListAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task UpdateAsync_RenamingScope_OldNameLookupStopsResolving()
    {
        var originalName = TestEntities.NewId("scope-name");
        var scope = TestEntities.CreateScope(name: originalName);
        await _store.CreateAsync(scope, CancellationToken.None);

        var newName = TestEntities.NewId("renamed-scope");
        await _store.SetNameAsync(scope, newName, CancellationToken.None);
        await _store.UpdateAsync(scope, CancellationToken.None);

        var byOldName = await _store.FindByNameAsync(originalName, CancellationToken.None);
        Assert.Null(byOldName);

        var byNewName = await _store.FindByNameAsync(newName, CancellationToken.None);
        Assert.NotNull(byNewName);
        Assert.Equal(scope.Id, byNewName.Id);
    }

    [Fact]
    public async Task UpdateAsync_RemovingResource_ResourceLookupStopsResolving()
    {
        var resource = $"resource-{Guid.NewGuid():N}";
        var scope = TestEntities.CreateScope();
        scope.Resources = TestEntities.JsonArray(resource);
        await _store.CreateAsync(scope, CancellationToken.None);

        await _store.SetResourcesAsync(scope, ImmutableArray<string>.Empty, CancellationToken.None);
        await _store.UpdateAsync(scope, CancellationToken.None);

        var byResource = await _store.FindByResourceAsync(resource, CancellationToken.None).ToListAsync();
        Assert.Empty(byResource);
    }

    private async Task<OpenIddictDynamoDbScopeStore<OpenIddictDynamoDbScope>> CreateEmptyStoreAsync()
    {
        var options = new OpenIddictDynamoDbOptions
        {
            ApplicationsTableName = $"{_fixture.Options.ApplicationsTableName}-empty-{Guid.NewGuid():N}",
            AuthorizationsTableName = $"{_fixture.Options.AuthorizationsTableName}-empty-{Guid.NewGuid():N}",
            ScopesTableName = $"{_fixture.Options.ScopesTableName}-empty-{Guid.NewGuid():N}",
            TokensTableName = $"{_fixture.Options.TokensTableName}-empty-{Guid.NewGuid():N}"
        };

        await OpenIddictDynamoDbTableCreator.CreateTablesAsync(_fixture.Client, options, CancellationToken.None);

        return new OpenIddictDynamoDbScopeStore<OpenIddictDynamoDbScope>(_fixture.Client, options);
    }

    private static void AssertDynamoDbConcurrencyFailure(OpenIddictExceptions.ConcurrencyException exception)
        => Assert.True(exception.InnerException is ConditionalCheckFailedException or TransactionCanceledException);

}
