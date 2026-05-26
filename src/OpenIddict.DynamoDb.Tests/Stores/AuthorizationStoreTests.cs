using Amazon.DynamoDBv2.Model;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;
using OpenIddict.DynamoDb.Tests.Fixtures;
using OpenIddict.DynamoDb.Tests.Helpers;
using System.Collections.Immutable;
using System.Text.Json;

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

    [Fact]
    public async Task FindAsync_BySubjectAndClient_ReturnsMatching()
    {
        var subject = TestEntities.NewId("subject");
        var applicationId = TestEntities.NewId("app");
        var matching = TestEntities.CreateAuthorization(subject: subject, applicationId: applicationId);
        var nonMatching = TestEntities.CreateAuthorization(subject: subject);
        await _store.CreateAsync(matching, CancellationToken.None);
        await _store.CreateAsync(nonMatching, CancellationToken.None);

        var authorizations = await _store.FindAsync(subject, applicationId, CancellationToken.None).ToListAsync();

        Assert.Single(authorizations);
        Assert.Equal(matching.Id, authorizations[0].Id);
    }

    [Fact]
    public async Task FindAsync_BySubjectAndStatusAndType_ReturnsMatching()
    {
        var subject = TestEntities.NewId("subject");
        var matching = TestEntities.CreateAuthorization(subject: subject);
        matching.Type = OpenIddictConstants.AuthorizationTypes.Permanent;
        var nonMatching = TestEntities.CreateAuthorization(subject: subject);
        nonMatching.Type = "ad-hoc";
        await _store.CreateAsync(matching, CancellationToken.None);
        await _store.CreateAsync(nonMatching, CancellationToken.None);

        var authorizations = await _store.FindAsync(subject, client: null, OpenIddictConstants.Statuses.Valid, OpenIddictConstants.AuthorizationTypes.Permanent, CancellationToken.None).ToListAsync();

        Assert.Single(authorizations);
        Assert.Equal(matching.Id, authorizations[0].Id);
    }

    [Fact]
    public async Task FindAsync_ByScopes_ReturnsOnlyMatchingAuthorizations()
    {
        var subject = TestEntities.NewId("subject");
        var matching = TestEntities.CreateAuthorization(subject: subject);
        matching.Scopes = TestEntities.JsonArray("openid", "profile", "email");
        var nonMatching = TestEntities.CreateAuthorization(subject: subject);
        nonMatching.Scopes = TestEntities.JsonArray("openid");
        await _store.CreateAsync(matching, CancellationToken.None);
        await _store.CreateAsync(nonMatching, CancellationToken.None);

        var authorizations = await _store.FindAsync(
            subject, client: null, status: null, type: null,
            scopes: ImmutableArray.Create("openid", "profile", "email"),
            CancellationToken.None).ToListAsync();

        Assert.Single(authorizations);
        Assert.Equal(matching.Id, authorizations[0].Id);
    }

    [Fact]
    public async Task InstantiateAsync_ReturnsNewInstance()
    {
        var authz = await _store.InstantiateAsync(CancellationToken.None);
        Assert.NotNull(authz);
        Assert.NotNull(authz.ConcurrencyToken);
        Assert.NotEmpty(authz.ConcurrencyToken);
    }

    [Fact]
    public async Task GetApplicationIdAsync_ReturnsApplicationId()
    {
        var authorization = TestEntities.CreateAuthorization();
        var result = await _store.GetApplicationIdAsync(authorization, CancellationToken.None);
        Assert.Equal(authorization.ApplicationId, result);
    }

    [Fact]
    public async Task GetCreationDateAsync_ReturnsCreationDate()
    {
        var authorization = TestEntities.CreateAuthorization();
        var result = await _store.GetCreationDateAsync(authorization, CancellationToken.None);
        Assert.Equal(authorization.CreationDate, result);
    }

    [Fact]
    public async Task GetIdAsync_ReturnsId()
    {
        var authorization = TestEntities.CreateAuthorization();
        var result = await _store.GetIdAsync(authorization, CancellationToken.None);
        Assert.Equal(authorization.Id, result);
    }

    [Fact]
    public async Task GetPropertiesAsync_ReturnsDeserializedDictionary()
    {
        var authorization = TestEntities.CreateAuthorization();
        var result = await _store.GetPropertiesAsync(authorization, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetScopesAsync_ReturnsDeserializedArray()
    {
        var authorization = TestEntities.CreateAuthorization();
        var result = await _store.GetScopesAsync(authorization, CancellationToken.None);
        Assert.False(result.IsDefaultOrEmpty);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsStatus()
    {
        var authorization = TestEntities.CreateAuthorization();
        var result = await _store.GetStatusAsync(authorization, CancellationToken.None);
        Assert.Equal(authorization.Status, result);
    }

    [Fact]
    public async Task GetSubjectAsync_ReturnsSubject()
    {
        var authorization = TestEntities.CreateAuthorization();
        var result = await _store.GetSubjectAsync(authorization, CancellationToken.None);
        Assert.Equal(authorization.Subject, result);
    }

    [Fact]
    public async Task GetTypeAsync_ReturnsType()
    {
        var authorization = TestEntities.CreateAuthorization();
        var result = await _store.GetTypeAsync(authorization, CancellationToken.None);
        Assert.Equal(authorization.Type, result);
    }

    [Fact]
    public async Task SetApplicationIdAsync_UpdatesApplicationId()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        var newAppId = TestEntities.NewId("newapp");
        await _store.SetApplicationIdAsync(authorization, newAppId, CancellationToken.None);
        await _store.UpdateAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newAppId, stored.ApplicationId);
    }

    [Fact]
    public async Task SetCreationDateAsync_UpdatesCreationDate()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        var newDate = DateTimeOffset.UtcNow.AddDays(-10);
        await _store.SetCreationDateAsync(authorization, newDate, CancellationToken.None);
        await _store.UpdateAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newDate, stored.CreationDate);
    }

    [Fact]
    public async Task SetPropertiesAsync_UpdatesProperties()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        using var doc = JsonDocument.Parse("{\"NewProp\":\"NewVal\"}");
        var properties = ImmutableDictionary.CreateRange(new[] { KeyValuePair.Create("NewProp", doc.RootElement) });
        await _store.SetPropertiesAsync(authorization, properties, CancellationToken.None);
        await _store.UpdateAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        var result = await _store.GetPropertiesAsync(stored, CancellationToken.None);
        Assert.True(result.ContainsKey("NewProp"));
    }

    [Fact]
    public async Task SetScopesAsync_UpdatesScopes()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        var newScopes = ImmutableArray.Create("openid", "email", "custom_scope");
        await _store.SetScopesAsync(authorization, newScopes, CancellationToken.None);
        await _store.UpdateAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        var result = await _store.GetScopesAsync(stored, CancellationToken.None);
        Assert.True(newScopes.SequenceEqual(result));
    }

    [Fact]
    public async Task SetStatusAsync_UpdatesStatus()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        await _store.SetStatusAsync(authorization, OpenIddictConstants.Statuses.Revoked, CancellationToken.None);
        await _store.UpdateAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
    }

    [Fact]
    public async Task SetSubjectAsync_UpdatesSubject()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        var newSubject = TestEntities.NewId("newsubject");
        await _store.SetSubjectAsync(authorization, newSubject, CancellationToken.None);
        await _store.UpdateAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newSubject, stored.Subject);
    }

    [Fact]
    public async Task SetTypeAsync_UpdatesType()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        await _store.SetTypeAsync(authorization, "ad-hoc", CancellationToken.None);
        await _store.UpdateAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("ad-hoc", stored.Type);
    }

    [Fact]
    public async Task PruneAsync_RemovesPrunableAuthorizations()
    {
        var authorization = TestEntities.CreateAuthorization();
        authorization.Status = OpenIddictConstants.Statuses.Inactive;
        authorization.CreationDate = DateTimeOffset.UtcNow.AddDays(-30);
        await _store.CreateAsync(authorization, CancellationToken.None);

        var pruned = await _store.PruneAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(1, pruned);
        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.Null(stored);
    }

    [Fact]
    public async Task CountAsync_IncludesCreatedAuthorizations()
    {
        var before = await _store.CountAsync(CancellationToken.None);
        await _store.CreateAsync(TestEntities.CreateAuthorization(), CancellationToken.None);
        await _store.CreateAsync(TestEntities.CreateAuthorization(), CancellationToken.None);

        var after = await _store.CountAsync(CancellationToken.None);

        Assert.Equal(before + 2, after);
    }

    [Fact]
    public async Task ListAsync_ReturnsCreatedAuthorizations()
    {
        var first = TestEntities.CreateAuthorization();
        var second = TestEntities.CreateAuthorization();
        await _store.CreateAsync(first, CancellationToken.None);
        await _store.CreateAsync(second, CancellationToken.None);

        var authorizations = await _store.ListAsync(count: null, offset: null, CancellationToken.None).ToListAsync();

        Assert.Contains(authorizations, a => a.Id == first.Id);
        Assert.Contains(authorizations, a => a.Id == second.Id);
    }

    [Fact]
    public async Task CountAsync_WithIQueryable_ThrowsNotSupportedException()
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => _store.CountAsync<OpenIddictDynamoDbAuthorization>(q => q, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task GetAsync_ThrowsNotSupportedException()
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => _store.GetAsync<object, string>((q, s) => q.Select(_ => _.Id), "state", CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task ListAsync_WithIQueryable_ThrowsNotSupportedException()
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => _store.ListAsync<object, string>((q, s) => q.Select(_ => _.Id), "state", CancellationToken.None).ToListAsync());
    }

}
