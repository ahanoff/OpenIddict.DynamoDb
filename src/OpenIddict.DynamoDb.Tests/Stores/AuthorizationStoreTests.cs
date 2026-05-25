using Amazon.DynamoDBv2.Model;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;
using OpenIddict.DynamoDb.Tests.Fixtures;
using OpenIddict.DynamoDb.Tests.Helpers;

namespace OpenIddict.DynamoDb.Tests.Stores;

[Collection(DynamoDbCollection.Name)]
public sealed class AuthorizationStoreTests
{
    private readonly OpenIddictDynamoDbAuthorizationStore<OpenIddictDynamoDbAuthorization> _store;

    public AuthorizationStoreTests(DynamoDbFixture fixture)
    {
        _store = new OpenIddictDynamoDbAuthorizationStore<OpenIddictDynamoDbAuthorization>(fixture.Client, fixture.Options);
    }

    [Fact]
    public async Task CreateAsync_StoresAuthorization()
    {
        var authorization = TestEntities.CreateAuthorization();

        await _store.CreateAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(authorization.Subject, stored.Subject);
        Assert.Equal(authorization.ApplicationId, stored.ApplicationId);
        Assert.Equal(authorization.Scopes, stored.Scopes);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsAuthorization()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(authorization.Id, stored.Id);
    }

    [Fact]
    public async Task FindBySubjectAsync_ReturnsAuthorizationsForSubject()
    {
        var subject = TestEntities.NewId("subject");
        var authorization = TestEntities.CreateAuthorization(subject: subject);
        await _store.CreateAsync(authorization, CancellationToken.None);

        var authorizations = await _store.FindBySubjectAsync(subject, CancellationToken.None).ToListAsync();

        Assert.Contains(authorizations, item => item.Id == authorization.Id);
    }

    [Fact]
    public async Task FindByApplicationIdAsync_ReturnsAuthorizationsForApplication()
    {
        var applicationId = TestEntities.NewId("app");
        var authorization = TestEntities.CreateAuthorization(applicationId: applicationId);
        await _store.CreateAsync(authorization, CancellationToken.None);

        var authorizations = await _store.FindByApplicationIdAsync(applicationId, CancellationToken.None).ToListAsync();

        Assert.Contains(authorizations, item => item.Id == authorization.Id);
    }

    [Fact]
    public async Task UpdateAsync_PersistsStatus()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        authorization.Status = OpenIddictConstants.Statuses.Inactive;
        await _store.UpdateAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Inactive, stored.Status);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAuthorization()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        await _store.DeleteAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.Null(stored);
    }

    [Fact]
    public async Task RevokeAsync_RevokesMatchingAuthorization()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        var count = await _store.RevokeAsync(authorization.Subject, authorization.ApplicationId, authorization.Status, authorization.Type, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.Equal(1, count);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
    }

    [Fact]
    public async Task RevokeByApplicationIdAsync_RevokesAuthorizationsForApplication()
    {
        var applicationId = TestEntities.NewId("app");
        var authorization = TestEntities.CreateAuthorization(applicationId: applicationId);
        await _store.CreateAsync(authorization, CancellationToken.None);

        var count = await _store.RevokeByApplicationIdAsync(applicationId, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.Equal(1, count);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
    }

    [Fact]
    public async Task RevokeBySubjectAsync_RevokesAuthorizationsForSubject()
    {
        var subject = TestEntities.NewId("subject");
        var authorization = TestEntities.CreateAuthorization(subject: subject);
        await _store.CreateAsync(authorization, CancellationToken.None);

        var count = await _store.RevokeBySubjectAsync(subject, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.Equal(1, count);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
    }
    [Fact]
    public async Task FindByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _store.FindByIdAsync("nonexistent-id", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindBySubjectAsync_NotFound_ReturnsEmpty()
    {
        var authorizations = await _store.FindBySubjectAsync("nonexistent-subject", CancellationToken.None).ToListAsync();

        Assert.Empty(authorizations);
    }

    [Fact]
    public async Task FindByApplicationIdAsync_NotFound_ReturnsEmpty()
    {
        var authorizations = await _store.FindByApplicationIdAsync("nonexistent-application", CancellationToken.None).ToListAsync();

        Assert.Empty(authorizations);
    }

    [Fact]
    public async Task UpdateAsync_StaleConcurrencyToken_Throws()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        var originalToken = authorization.ConcurrencyToken;

        authorization.Status = OpenIddictConstants.Statuses.Inactive;
        await _store.UpdateAsync(authorization, CancellationToken.None);

        authorization.ConcurrencyToken = originalToken;
        authorization.Status = OpenIddictConstants.Statuses.Revoked;
        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.UpdateAsync(authorization, CancellationToken.None).AsTask());

        Assert.IsType<ConditionalCheckFailedException>(exception.InnerException);
    }

    [Fact]
    public async Task DeleteAsync_StaleConcurrencyToken_Throws()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        var originalToken = authorization.ConcurrencyToken;

        authorization.Status = OpenIddictConstants.Statuses.Inactive;
        await _store.UpdateAsync(authorization, CancellationToken.None);

        authorization.ConcurrencyToken = originalToken;
        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.DeleteAsync(authorization, CancellationToken.None).AsTask());

        Assert.IsType<ConditionalCheckFailedException>(exception.InnerException);
    }

    [Fact]
    public async Task FindAsync_BySubjectAndStatus_ReturnsMatching()
    {
        var subject = TestEntities.NewId("subject");
        var matching = TestEntities.CreateAuthorization(subject: subject);
        var nonMatching = TestEntities.CreateAuthorization(subject: subject);
        nonMatching.Status = OpenIddictConstants.Statuses.Inactive;
        await _store.CreateAsync(matching, CancellationToken.None);
        await _store.CreateAsync(nonMatching, CancellationToken.None);

        var authorizations = await _store.FindAsync(subject, client: null, OpenIddictConstants.Statuses.Valid, CancellationToken.None).ToListAsync();

        Assert.Single(authorizations);
        Assert.Equal(matching.Id, authorizations[0].Id);
    }

}
