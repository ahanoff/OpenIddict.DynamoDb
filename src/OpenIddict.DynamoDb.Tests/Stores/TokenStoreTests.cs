using Amazon.DynamoDBv2.Model;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;
using OpenIddict.DynamoDb.Tests.Fixtures;
using OpenIddict.DynamoDb.Tests.Helpers;

namespace OpenIddict.DynamoDb.Tests.Stores;

[Collection(DynamoDbCollection.Name)]
public sealed class TokenStoreTests
{
    private readonly OpenIddictDynamoDbTokenStore<OpenIddictDynamoDbToken> _store;

    public TokenStoreTests(DynamoDbFixture fixture)
    {
        _store = new OpenIddictDynamoDbTokenStore<OpenIddictDynamoDbToken>(fixture.Client, fixture.Options);
    }

    [Fact]
    public async Task CreateAsync_StoresToken()
    {
        var token = TestEntities.CreateToken();

        await _store.CreateAsync(token, CancellationToken.None);
        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(token.Subject, stored.Subject);
        Assert.Equal(token.ApplicationId, stored.ApplicationId);
        Assert.Equal(token.AuthorizationId, stored.AuthorizationId);
        Assert.Equal(token.ReferenceId, stored.ReferenceId);
        Assert.Equal(token.Type, stored.Type);
        Assert.Equal(token.Status, stored.Status);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsToken()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(token.Id, stored.Id);
    }

    [Fact]
    public async Task FindByReferenceIdAsync_ReturnsToken()
    {
        var token = TestEntities.CreateToken(referenceId: TestEntities.NewId("reference"));
        await _store.CreateAsync(token, CancellationToken.None);

        var stored = await _store.FindByReferenceIdAsync(token.ReferenceId!, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(token.Id, stored.Id);
    }

    [Fact]
    public async Task FindBySubjectAsync_ReturnsTokensForSubject()
    {
        var subject = TestEntities.NewId("subject");
        var token = TestEntities.CreateToken(subject: subject);
        await _store.CreateAsync(token, CancellationToken.None);

        var tokens = await _store.FindBySubjectAsync(subject, CancellationToken.None).ToListAsync();

        Assert.Contains(tokens, item => item.Id == token.Id);
    }

    [Fact]
    public async Task FindByApplicationIdAsync_ReturnsTokensForApplication()
    {
        var applicationId = TestEntities.NewId("app");
        var token = TestEntities.CreateToken(applicationId: applicationId);
        await _store.CreateAsync(token, CancellationToken.None);

        var tokens = await _store.FindByApplicationIdAsync(applicationId, CancellationToken.None).ToListAsync();

        Assert.Contains(tokens, item => item.Id == token.Id);
    }

    [Fact]
    public async Task FindByAuthorizationIdAsync_ReturnsTokensForAuthorization()
    {
        var authorizationId = TestEntities.NewId("authz");
        var token = TestEntities.CreateToken(authorizationId: authorizationId);
        await _store.CreateAsync(token, CancellationToken.None);

        var tokens = await _store.FindByAuthorizationIdAsync(authorizationId, CancellationToken.None).ToListAsync();

        Assert.Contains(tokens, item => item.Id == token.Id);
    }

    [Fact]
    public async Task UpdateAsync_PersistsStatus()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        token.Status = OpenIddictConstants.Statuses.Inactive;
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Inactive, stored.Status);
    }

    [Fact]
    public async Task DeleteAsync_RemovesToken()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        await _store.DeleteAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.Null(stored);
    }

    [Fact]
    public async Task RevokeAsync_RevokesMatchingToken()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        var count = await _store.RevokeAsync(token.Subject, token.ApplicationId, token.Status, token.Type, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.Equal(1, count);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
    }

    [Fact]
    public async Task RevokeByApplicationIdAsync_RevokesTokensForApplication()
    {
        var applicationId = TestEntities.NewId("app");
        var token = TestEntities.CreateToken(applicationId: applicationId);
        await _store.CreateAsync(token, CancellationToken.None);

        var count = await _store.RevokeByApplicationIdAsync(applicationId, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.Equal(1, count);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
    }

    [Fact]
    public async Task RevokeBySubjectAsync_RevokesTokensForSubject()
    {
        var subject = TestEntities.NewId("subject");
        var token = TestEntities.CreateToken(subject: subject);
        await _store.CreateAsync(token, CancellationToken.None);

        var count = await _store.RevokeBySubjectAsync(subject, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.Equal(1, count);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
    }

    [Fact]
    public async Task RevokeByAuthorizationIdAsync_RevokesTokensForAuthorization()
    {
        var authorizationId = TestEntities.NewId("authz");
        var token = TestEntities.CreateToken(authorizationId: authorizationId);
        await _store.CreateAsync(token, CancellationToken.None);

        var count = await _store.RevokeByAuthorizationIdAsync(authorizationId, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
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
    public async Task FindByReferenceIdAsync_NotFound_ReturnsNull()
    {
        var result = await _store.FindByReferenceIdAsync("nonexistent-reference", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindBySubjectAsync_NotFound_ReturnsEmpty()
    {
        var tokens = await _store.FindBySubjectAsync("nonexistent-subject", CancellationToken.None).ToListAsync();

        Assert.Empty(tokens);
    }

    [Fact]
    public async Task FindByApplicationIdAsync_NotFound_ReturnsEmpty()
    {
        var tokens = await _store.FindByApplicationIdAsync("nonexistent-application", CancellationToken.None).ToListAsync();

        Assert.Empty(tokens);
    }

    [Fact]
    public async Task FindByAuthorizationIdAsync_NotFound_ReturnsEmpty()
    {
        var tokens = await _store.FindByAuthorizationIdAsync("nonexistent-authorization", CancellationToken.None).ToListAsync();

        Assert.Empty(tokens);
    }

    [Fact]
    public async Task UpdateAsync_StaleConcurrencyToken_Throws()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        var originalToken = token.ConcurrencyToken;

        token.Status = OpenIddictConstants.Statuses.Inactive;
        await _store.UpdateAsync(token, CancellationToken.None);

        token.ConcurrencyToken = originalToken;
        token.Status = OpenIddictConstants.Statuses.Revoked;
        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.UpdateAsync(token, CancellationToken.None).AsTask());

        AssertDynamoDbConcurrencyFailure(exception);
    }

    [Fact]
    public async Task DeleteAsync_StaleConcurrencyToken_Throws()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        var originalToken = token.ConcurrencyToken;

        token.Status = OpenIddictConstants.Statuses.Inactive;
        await _store.UpdateAsync(token, CancellationToken.None);

        token.ConcurrencyToken = originalToken;
        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.DeleteAsync(token, CancellationToken.None).AsTask());

        AssertDynamoDbConcurrencyFailure(exception);
    }

    [Fact]
    public async Task FindAsync_BySubjectAndClient_ReturnsMatching()
    {
        var subject = TestEntities.NewId("subject");
        var applicationId = TestEntities.NewId("app");
        var matching = TestEntities.CreateToken(subject: subject, applicationId: applicationId);
        var nonMatching = TestEntities.CreateToken(subject: subject, applicationId: TestEntities.NewId("app"));
        await _store.CreateAsync(matching, CancellationToken.None);
        await _store.CreateAsync(nonMatching, CancellationToken.None);

        var tokens = await _store.FindAsync(subject, applicationId, CancellationToken.None).ToListAsync();

        Assert.Single(tokens);
        Assert.Equal(matching.Id, tokens[0].Id);
    }

    [Fact]
    public async Task FindAsync_BySubjectAndStatus_ReturnsMatching()
    {
        var subject = TestEntities.NewId("subject");
        var matching = TestEntities.CreateToken(subject: subject);
        var nonMatching = TestEntities.CreateToken(subject: subject);
        nonMatching.Status = OpenIddictConstants.Statuses.Inactive;
        await _store.CreateAsync(matching, CancellationToken.None);
        await _store.CreateAsync(nonMatching, CancellationToken.None);

        var tokens = await _store.FindAsync(subject, client: null, OpenIddictConstants.Statuses.Valid, CancellationToken.None).ToListAsync();

        Assert.Single(tokens);
        Assert.Equal(matching.Id, tokens[0].Id);
    }

    private static void AssertDynamoDbConcurrencyFailure(OpenIddictExceptions.ConcurrencyException exception)
        => Assert.True(exception.InnerException is ConditionalCheckFailedException or TransactionCanceledException);

}
