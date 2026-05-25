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
