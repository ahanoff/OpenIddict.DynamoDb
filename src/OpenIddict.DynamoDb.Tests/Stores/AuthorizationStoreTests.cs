using Amazon.DynamoDBv2.Model;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;
using OpenIddict.DynamoDb.Tests.Fixtures;
using OpenIddict.DynamoDb.Tests.Helpers;
using OpenIddict.DynamoDb.Helpers;
using System.Collections.Immutable;
using System.Text.Json;

namespace OpenIddict.DynamoDb.Tests.Stores;

[Collection(DynamoDbCollection.Name)]
public sealed class AuthorizationStoreTests
{
    private readonly OpenIddictDynamoDbAuthorizationStore<OpenIddictDynamoDbAuthorization> _store;
    private readonly DynamoDbFixture _fixture;

    public AuthorizationStoreTests(DynamoDbFixture fixture)
    {
        _store = new OpenIddictDynamoDbAuthorizationStore<OpenIddictDynamoDbAuthorization>(fixture.Client, fixture.Options);
        _fixture = fixture;
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
    public async Task CreateAsync_DuplicateId_ThrowsConcurrencyException()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);
        var duplicate = TestEntities.CreateAuthorization(id: authorization.Id);

        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.CreateAsync(duplicate, CancellationToken.None).AsTask());

        Assert.IsType<TransactionCanceledException>(exception.InnerException);
        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(authorization.ConcurrencyToken, stored.ConcurrencyToken);
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
    public async Task DeleteAsync_LinkedToken_PreventsOrphan()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);
        var tokenStore = new OpenIddictDynamoDbTokenStore<OpenIddictDynamoDbToken>(_fixture.Client, _fixture.Options);
        var token = TestEntities.CreateToken(authorizationId: authorization.Id);
        await tokenStore.CreateAsync(token, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.DeleteAsync(authorization, CancellationToken.None).AsTask());

        Assert.IsType<TransactionCanceledException>(exception.InnerException);
        Assert.NotNull(await _store.FindByIdAsync(authorization.Id, CancellationToken.None));

        await tokenStore.DeleteAsync(token, CancellationToken.None);
        await _store.DeleteAsync(authorization, CancellationToken.None);
        Assert.Null(await _store.FindByIdAsync(authorization.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_MissingCounter_FailsClosed()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);
        await _fixture.Client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationTokenCountPk(authorization.Id)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationTokenCountSk)
            }
        });

        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.DeleteAsync(authorization, CancellationToken.None).AsTask());

        Assert.IsType<TransactionCanceledException>(exception.InnerException);
        Assert.NotNull(await _store.FindByIdAsync(authorization.Id, CancellationToken.None));
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
    public async Task RevokeByApplicationIdAsync_AuthorizationHiddenFromGsi_IsStillRevokedOnRead()
    {
        var applicationId = TestEntities.NewId("app");
        var authorization = TestEntities.CreateAuthorization(applicationId: applicationId);
        await _store.CreateAsync(authorization, CancellationToken.None);
        await _fixture.Client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _fixture.Options.AuthorizationsTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationPk(authorization.Id)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationSk)
            },
            UpdateExpression = "REMOVE gsi2_pk, gsi2_sk"
        });

        await _store.RevokeByApplicationIdAsync(applicationId, CancellationToken.None);
        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        var enumerated = await _store.FindBySubjectAsync(authorization.Subject!, CancellationToken.None)
            .SingleAsync(item => item.Id == authorization.Id);

        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, enumerated.Status);
    }

    [Fact]
    public async Task RevokeByApplicationIdAsync_DoesNotRegressNewerMarker()
    {
        var applicationId = TestEntities.NewId("app");
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(5);
        var marker = RevocationMarkerHelper.CreateItem(
            "AUTHORIZATION", null, applicationId, null, null, null, cutoff);
        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _fixture.Options.AuthorizationsTableName,
            Item = marker
        });

        await _store.RevokeByApplicationIdAsync(applicationId, CancellationToken.None);

        var response = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _fixture.Options.AuthorizationsTableName,
            Key = RevocationMarkerHelper.CreateKey("AUTHORIZATION", null, applicationId, null, null, null),
            ConsistentRead = true
        });
        Assert.Equal(cutoff.ToUnixTimeMilliseconds(), DynamoDbHelper.GetLong(response.Item, "revoked_at"));
    }

    [Fact]
    public async Task UpdateAsync_FilterChangeCannotClearRevocationMarker()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);
        await RevocationMarkerHelper.WriteAsync(
            _fixture.Client, _fixture.Options.AuthorizationsTableName, "AUTHORIZATION",
            authorization.Subject, authorization.ApplicationId, null, authorization.Status, authorization.Type,
            DateTimeOffset.UtcNow.AddSeconds(1), CancellationToken.None);
        authorization.Status = OpenIddictConstants.Statuses.Inactive;
        authorization.Type = OpenIddictConstants.AuthorizationTypes.AdHoc;

        await _store.UpdateAsync(authorization, CancellationToken.None);

        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
        Assert.Equal(OpenIddictConstants.AuthorizationTypes.AdHoc, stored.Type);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentFilteredRevocation_FailsClosed()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);
        using var client = new RevocationRaceClient(_fixture.Client, cancellationToken =>
            RevocationMarkerHelper.WriteAsync(
                _fixture.Client, _fixture.Options.AuthorizationsTableName, "AUTHORIZATION",
                authorization.Subject, authorization.ApplicationId, null, OpenIddictConstants.Statuses.Valid,
                OpenIddictConstants.AuthorizationTypes.Permanent,
                DateTimeOffset.UtcNow.AddSeconds(1), cancellationToken));
        var store = new OpenIddictDynamoDbAuthorizationStore<OpenIddictDynamoDbAuthorization>(client, _fixture.Options);
        authorization.Status = OpenIddictConstants.Statuses.Inactive;
        authorization.Type = OpenIddictConstants.AuthorizationTypes.AdHoc;

        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => store.UpdateAsync(authorization, CancellationToken.None).AsTask());

        Assert.IsType<TransactionCanceledException>(exception.InnerException);
        var stored = await _store.FindByIdAsync(authorization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
        Assert.Equal(OpenIddictConstants.AuthorizationTypes.Permanent, stored.Type);
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

        Assert.IsType<TransactionCanceledException>(exception.InnerException);
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

        Assert.IsType<TransactionCanceledException>(exception.InnerException);
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
    public async Task PruneAsync_AuthorizationWithTokenHiddenFromGsi_IsPreserved()
    {
        var authorization = TestEntities.CreateAuthorization();
        authorization.Status = OpenIddictConstants.Statuses.Inactive;
        authorization.CreationDate = DateTimeOffset.UtcNow.AddDays(-30);
        await _store.CreateAsync(authorization, CancellationToken.None);
        var tokenStore = new OpenIddictDynamoDbTokenStore<OpenIddictDynamoDbToken>(_fixture.Client, _fixture.Options);
        var token = TestEntities.CreateToken(authorizationId: authorization.Id);
        await tokenStore.CreateAsync(token, CancellationToken.None);
        await _fixture.Client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenPk(token.Id)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSk)
            },
            UpdateExpression = "REMOVE gsi4_pk, gsi4_sk"
        });

        await _store.PruneAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.NotNull(await _store.FindByIdAsync(authorization.Id, CancellationToken.None));
    }

    [Fact]
    public async Task PruneAsync_MissingCounter_PreservesAuthorization()
    {
        var authorization = TestEntities.CreateAuthorization();
        authorization.Status = OpenIddictConstants.Statuses.Inactive;
        authorization.CreationDate = DateTimeOffset.UtcNow.AddDays(-30);
        await _store.CreateAsync(authorization, CancellationToken.None);
        await _fixture.Client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationTokenCountPk(authorization.Id)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationTokenCountSk)
            }
        });

        var pruned = await _store.PruneAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(0, pruned);
        Assert.NotNull(await _store.FindByIdAsync(authorization.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CustomAuthorizationProperties_RoundTrip()
    {
        var store = new OpenIddictDynamoDbAuthorizationStore<CustomAuthorization>(_fixture.Client, _fixture.Options);
        var authorization = new CustomAuthorization
        {
            Id = TestEntities.NewId("authorization"),
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            CreationDate = DateTimeOffset.UtcNow,
            ConcurrencyToken = Guid.NewGuid().ToString(),
            TenantId = "tenant-42"
        };

        await store.CreateAsync(authorization, CancellationToken.None);
        var stored = await store.FindByIdAsync(authorization.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal("tenant-42", stored.TenantId);
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
    public async Task IQueryableOverloads_ExecuteAgainstMaterializedResults()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _store.CreateAsync(authorization, CancellationToken.None);

        Assert.True(await _store.CountAsync(q => q.Where(item => item.Id == authorization.Id), CancellationToken.None) >= 1);
        Assert.Equal(authorization.Id, await _store.GetAsync((q, id) => q.Where(item => item.Id == id).Select(item => item.Id), authorization.Id, CancellationToken.None));
        Assert.Equal([authorization.Id], await _store.ListAsync((q, id) => q.Where(item => item.Id == id).Select(item => item.Id), authorization.Id, CancellationToken.None).ToListAsync());
    }

}
