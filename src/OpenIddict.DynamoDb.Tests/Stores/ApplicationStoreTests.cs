using Amazon.DynamoDBv2.Model;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;
using OpenIddict.DynamoDb.Tests.Fixtures;
using OpenIddict.DynamoDb.Tests.Helpers;

namespace OpenIddict.DynamoDb.Tests.Stores;

[Collection(DynamoDbCollection.Name)]
public sealed class ApplicationStoreTests
{
    private readonly OpenIddictDynamoDbApplicationStore<OpenIddictDynamoDbApplication> _store;
    private readonly DynamoDbFixture _fixture;

    public ApplicationStoreTests(DynamoDbFixture fixture)
    {
        _store = new OpenIddictDynamoDbApplicationStore<OpenIddictDynamoDbApplication>(fixture.Client, fixture.Options);
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAsync_StoresApplication()
    {
        var application = TestEntities.CreateApplication();

        await _store.CreateAsync(application, CancellationToken.None);

        var stored = await _store.FindByIdAsync(application.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(application.ClientId, stored.ClientId);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsApplication()
    {
        var application = TestEntities.CreateApplication();
        await _store.CreateAsync(application, CancellationToken.None);

        var stored = await _store.FindByIdAsync(application.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(application.Id, stored.Id);
    }

    [Fact]
    public async Task FindByClientIdAsync_ReturnsApplication()
    {
        var application = TestEntities.CreateApplication(clientId: TestEntities.NewId("client"));
        await _store.CreateAsync(application, CancellationToken.None);

        var stored = await _store.FindByClientIdAsync(application.ClientId!, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(application.Id, stored.Id);
    }

    [Fact]
    public async Task UpdateAsync_PersistsDisplayName()
    {
        var application = TestEntities.CreateApplication();
        await _store.CreateAsync(application, CancellationToken.None);

        application.DisplayName = "Updated application";
        await _store.UpdateAsync(application, CancellationToken.None);

        var stored = await _store.FindByIdAsync(application.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Updated application", stored.DisplayName);
    }

    [Fact]
    public async Task DeleteAsync_RemovesApplication()
    {
        var application = TestEntities.CreateApplication();
        await _store.CreateAsync(application, CancellationToken.None);

        await _store.DeleteAsync(application, CancellationToken.None);

        var stored = await _store.FindByIdAsync(application.Id, CancellationToken.None);
        Assert.Null(stored);
    }

    [Fact]
    public async Task ListAsync_ReturnsCreatedApplications()
    {
        var first = TestEntities.CreateApplication();
        var second = TestEntities.CreateApplication();
        await _store.CreateAsync(first, CancellationToken.None);
        await _store.CreateAsync(second, CancellationToken.None);

        var applications = await _store.ListAsync(count: null, offset: null, CancellationToken.None).ToListAsync();

        Assert.Contains(applications, application => application.Id == first.Id);
        Assert.Contains(applications, application => application.Id == second.Id);
    }

    [Fact]
    public async Task CountAsync_IncludesCreatedApplications()
    {
        var before = await _store.CountAsync(CancellationToken.None);
        await _store.CreateAsync(TestEntities.CreateApplication(), CancellationToken.None);
        await _store.CreateAsync(TestEntities.CreateApplication(), CancellationToken.None);

        var after = await _store.CountAsync(CancellationToken.None);

        Assert.Equal(before + 2, after);
    }
    [Fact]
    public async Task FindByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _store.FindByIdAsync("nonexistent-id", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByClientIdAsync_NotFound_ReturnsNull()
    {
        var result = await _store.FindByClientIdAsync("nonexistent-client", CancellationToken.None);

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

        var applications = await store.ListAsync(count: null, offset: null, CancellationToken.None).ToListAsync();

        Assert.Empty(applications);
    }

    [Fact]
    public async Task UpdateAsync_StaleConcurrencyToken_Throws()
    {
        var application = TestEntities.CreateApplication();
        await _store.CreateAsync(application, CancellationToken.None);

        var originalToken = application.ConcurrencyToken;

        application.DisplayName = "Updated application";
        await _store.UpdateAsync(application, CancellationToken.None);

        application.ConcurrencyToken = originalToken;
        application.DisplayName = "Should fail";
        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.UpdateAsync(application, CancellationToken.None).AsTask());

        AssertDynamoDbConcurrencyFailure(exception);
    }

    [Fact]
    public async Task DeleteAsync_StaleConcurrencyToken_Throws()
    {
        var application = TestEntities.CreateApplication();
        await _store.CreateAsync(application, CancellationToken.None);

        var originalToken = application.ConcurrencyToken;

        application.DisplayName = "Updated application";
        await _store.UpdateAsync(application, CancellationToken.None);

        application.ConcurrencyToken = originalToken;
        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.DeleteAsync(application, CancellationToken.None).AsTask());

        AssertDynamoDbConcurrencyFailure(exception);
    }

    private async Task<OpenIddictDynamoDbApplicationStore<OpenIddictDynamoDbApplication>> CreateEmptyStoreAsync()
    {
        var options = new OpenIddictDynamoDbOptions
        {
            ApplicationsTableName = $"{_fixture.Options.ApplicationsTableName}-empty-{Guid.NewGuid():N}",
            AuthorizationsTableName = $"{_fixture.Options.AuthorizationsTableName}-empty-{Guid.NewGuid():N}",
            ScopesTableName = $"{_fixture.Options.ScopesTableName}-empty-{Guid.NewGuid():N}",
            TokensTableName = $"{_fixture.Options.TokensTableName}-empty-{Guid.NewGuid():N}"
        };

        await OpenIddictDynamoDbTableCreator.CreateTablesAsync(_fixture.Client, options, CancellationToken.None);

        return new OpenIddictDynamoDbApplicationStore<OpenIddictDynamoDbApplication>(_fixture.Client, options);
    }

    private static void AssertDynamoDbConcurrencyFailure(OpenIddictExceptions.ConcurrencyException exception)
        => Assert.True(exception.InnerException is ConditionalCheckFailedException or TransactionCanceledException);

}
