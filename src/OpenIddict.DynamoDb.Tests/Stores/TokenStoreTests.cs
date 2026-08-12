using System.Collections.Immutable;
using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;
using OpenIddict.DynamoDb.Tests.Fixtures;
using OpenIddict.DynamoDb.Tests.Helpers;
using OpenIddict.DynamoDb.Helpers;

namespace OpenIddict.DynamoDb.Tests.Stores;

[Collection(DynamoDbCollection.Name)]
public sealed class TokenStoreTests
{
    private readonly OpenIddictDynamoDbTokenStore<OpenIddictDynamoDbToken> _store;
    private readonly OpenIddictDynamoDbAuthorizationStore<OpenIddictDynamoDbAuthorization> _authorizationStore;
    private readonly DynamoDbFixture _fixture;

    public TokenStoreTests(DynamoDbFixture fixture)
    {
        _store = new OpenIddictDynamoDbTokenStore<OpenIddictDynamoDbToken>(fixture.Client, fixture.Options);
        _authorizationStore = new OpenIddictDynamoDbAuthorizationStore<OpenIddictDynamoDbAuthorization>(fixture.Client, fixture.Options);
        _fixture = fixture;
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
    public async Task CreateAsync_MissingAuthorization_FailsWithoutPersistingToken()
    {
        var token = TestEntities.CreateToken(authorizationId: TestEntities.NewId("missing-authorization"));

        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.CreateAsync(token, CancellationToken.None).AsTask());

        AssertDynamoDbConcurrencyFailure(exception);
        Assert.Null(await _store.FindByIdAsync(token.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_AuthorizationWithoutCounter_FailsWithoutPersistingToken()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _authorizationStore.CreateAsync(authorization, CancellationToken.None);
        await _fixture.Client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationTokenCountPk(authorization.Id)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationTokenCountSk)
            }
        });
        var token = TestEntities.CreateToken(authorizationId: authorization.Id);

        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.CreateAsync(token, CancellationToken.None).AsTask());

        AssertDynamoDbConcurrencyFailure(exception);
        Assert.Null(await _store.FindByIdAsync(token.Id, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_AuthorizationWithoutCounter_FailsClosed()
    {
        var authorization = TestEntities.CreateAuthorization();
        await _authorizationStore.CreateAsync(authorization, CancellationToken.None);
        var token = TestEntities.CreateToken(authorizationId: authorization.Id);
        await _store.CreateAsync(token, CancellationToken.None);
        await _fixture.Client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationTokenCountPk(authorization.Id)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.AuthorizationTokenCountSk)
            }
        });
        token.Status = OpenIddictConstants.Statuses.Revoked;

        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => _store.UpdateAsync(token, CancellationToken.None).AsTask());

        AssertDynamoDbConcurrencyFailure(exception);
        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, stored.Status);
    }

    [Fact]
    public async Task CreateAsync_TtlAppliesOnlyToReferenceLookup()
    {
        var token = TestEntities.CreateToken(referenceId: TestEntities.NewId("reference"));
        token.ExpirationDate = DateTimeOffset.UtcNow.AddHours(1);

        await _store.CreateAsync(token, CancellationToken.None);

        var metadata = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenPk(token.Id)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSk)
            },
            ConsistentRead = true
        });
        var reference = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ReferenceIdPk(token.ReferenceId!)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ReferenceIdSk)
            },
            ConsistentRead = true
        });

        Assert.False(metadata.Item.ContainsKey("ttl"));
        Assert.True(reference.Item.ContainsKey("ttl"));
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
    public async Task FindByReferenceIdAsync_StaleLookupForRotatedToken_ReturnsNull()
    {
        var token = TestEntities.CreateToken(referenceId: TestEntities.NewId("reference"));
        await _store.CreateAsync(token, CancellationToken.None);
        var staleReferenceId = token.ReferenceId!;

        token.ReferenceId = TestEntities.NewId("rotated-reference");
        await _store.UpdateAsync(token, CancellationToken.None);
        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ReferenceIdPk(staleReferenceId)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ReferenceIdSk),
                ["token_id"] = DynamoDbHelper.ToAttr(token.Id)
            }
        });

        Assert.Null(await _store.FindByReferenceIdAsync(staleReferenceId, CancellationToken.None));
    }

    [Fact]
    public async Task FindAsync_CollidingSubjectApplicationValues_RemainIsolated()
    {
        var expected = TestEntities.CreateToken(subject: "a#b", applicationId: "c");
        var other = TestEntities.CreateToken(subject: "a", applicationId: "b#c");
        await _store.CreateAsync(expected, CancellationToken.None);
        await _store.CreateAsync(other, CancellationToken.None);

        var tokens = await _store.FindAsync("a#b", "c", CancellationToken.None).ToListAsync();

        Assert.Contains(tokens, token => token.Id == expected.Id);
        Assert.DoesNotContain(tokens, token => token.Id == other.Id);
    }

    [Fact]
    public async Task CustomTokenProperties_RoundTrip()
    {
        var token = new CustomToken
        {
            Id = TestEntities.NewId("token"),
            ApplicationId = TestEntities.NewId("app"),
            Subject = TestEntities.NewId("subject"),
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeHints.AccessToken,
            CreationDate = DateTimeOffset.UtcNow,
            ExpirationDate = DateTimeOffset.UtcNow.AddHours(1),
            ConcurrencyToken = Guid.NewGuid().ToString(),
            TenantId = "tenant-42"
        };
        var store = new OpenIddictDynamoDbTokenStore<CustomToken>(_fixture.Client, _fixture.Options);

        await store.CreateAsync(token, CancellationToken.None);
        var stored = await store.FindByIdAsync(token.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal("tenant-42", stored.TenantId);
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
        await _authorizationStore.CreateAsync(TestEntities.CreateAuthorization(id: authorizationId), CancellationToken.None);
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
    public async Task RevokeByApplicationIdAsync_TokenHiddenFromGsi_IsStillRevokedOnRead()
    {
        var applicationId = TestEntities.NewId("app");
        var token = TestEntities.CreateToken(applicationId: applicationId);
        await _store.CreateAsync(token, CancellationToken.None);
        await _fixture.Client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenPk(token.Id)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSk)
            },
            UpdateExpression = "REMOVE gsi3_pk, gsi3_sk"
        });

        await _store.RevokeByApplicationIdAsync(applicationId, CancellationToken.None);
        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        var enumerated = await _store.FindBySubjectAsync(token.Subject!, CancellationToken.None)
            .SingleAsync(item => item.Id == token.Id);

        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, enumerated.Status);
    }

    [Fact]
    public async Task RevokeByApplicationIdAsync_DoesNotRevokeTokensCreatedAfterCutoff()
    {
        var applicationId = TestEntities.NewId("app");
        await _store.RevokeByApplicationIdAsync(applicationId, CancellationToken.None);
        var token = TestEntities.CreateToken(applicationId: applicationId);
        token.CreationDate = DateTimeOffset.UtcNow.AddSeconds(1);
        await _store.CreateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, stored.Status);
    }

    [Fact]
    public async Task RevokeByApplicationIdAsync_DoesNotRegressNewerMarker()
    {
        var applicationId = TestEntities.NewId("app");
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(5);
        var marker = RevocationMarkerHelper.CreateItem(
            "TOKEN", null, applicationId, null, null, null, cutoff);
        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Item = marker
        });

        await _store.RevokeByApplicationIdAsync(applicationId, CancellationToken.None);

        var response = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Key = RevocationMarkerHelper.CreateKey("TOKEN", null, applicationId, null, null, null),
            ConsistentRead = true
        });
        Assert.Equal(cutoff.ToUnixTimeMilliseconds(), DynamoDbHelper.GetLong(response.Item, "revoked_at"));
    }

    [Fact]
    public async Task UpdateAsync_FilterChangeCannotClearRevocationMarker()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);
        await RevocationMarkerHelper.WriteAsync(
            _fixture.Client, _fixture.Options.TokensTableName, "TOKEN",
            token.Subject, token.ApplicationId, null, token.Status, token.Type,
            DateTimeOffset.UtcNow.AddSeconds(1), CancellationToken.None);
        token.Status = OpenIddictConstants.Statuses.Inactive;
        token.Type = OpenIddictConstants.TokenTypeHints.RefreshToken;

        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
        Assert.Equal(OpenIddictConstants.TokenTypeHints.RefreshToken, stored.Type);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentFilteredRevocation_FailsClosed()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);
        using var client = new RevocationRaceClient(_fixture.Client, cancellationToken =>
            RevocationMarkerHelper.WriteAsync(
                _fixture.Client, _fixture.Options.TokensTableName, "TOKEN",
                token.Subject, token.ApplicationId, null, OpenIddictConstants.Statuses.Valid,
                OpenIddictConstants.TokenTypeHints.AccessToken,
                DateTimeOffset.UtcNow.AddSeconds(1), cancellationToken));
        var store = new OpenIddictDynamoDbTokenStore<OpenIddictDynamoDbToken>(client, _fixture.Options);
        token.Status = OpenIddictConstants.Statuses.Inactive;
        token.Type = OpenIddictConstants.TokenTypeHints.RefreshToken;

        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(
            () => store.UpdateAsync(token, CancellationToken.None).AsTask());

        AssertDynamoDbConcurrencyFailure(exception);
        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, stored.Status);
        Assert.Equal(OpenIddictConstants.TokenTypeHints.AccessToken, stored.Type);
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
        await _authorizationStore.CreateAsync(TestEntities.CreateAuthorization(id: authorizationId), CancellationToken.None);
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

    [Fact]
    public async Task FindByIdAsync_ExpiredToken_RemainsAvailableToManager()
    {
        var token = TestEntities.CreateToken();
        token.ExpirationDate = DateTimeOffset.UtcNow.AddHours(-1);
        await _store.CreateAsync(token, CancellationToken.None);

        var result = await _store.FindByIdAsync(token.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(token.Id, result.Id);
    }

    [Fact]
    public async Task FindByReferenceIdAsync_ExpiredToken_RemainsAvailableToManager()
    {
        var token = TestEntities.CreateToken(referenceId: TestEntities.NewId("reference"));
        token.ExpirationDate = DateTimeOffset.UtcNow.AddHours(-1);
        await _store.CreateAsync(token, CancellationToken.None);

        var result = await _store.FindByReferenceIdAsync(token.ReferenceId!, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(token.Id, result.Id);
    }

    [Fact]
    public async Task FindBySubjectAsync_ExpiredToken_RemainsAvailableToManager()
    {
        var subject = TestEntities.NewId("subject");
        var validToken = TestEntities.CreateToken(subject: subject);
        var expiredToken = TestEntities.CreateToken(subject: subject);
        expiredToken.ExpirationDate = DateTimeOffset.UtcNow.AddHours(-1);
        await _store.CreateAsync(validToken, CancellationToken.None);
        await _store.CreateAsync(expiredToken, CancellationToken.None);

        var tokens = await _store.FindBySubjectAsync(subject, CancellationToken.None).ToListAsync();

        Assert.Contains(tokens, token => token.Id == validToken.Id);
        Assert.Contains(tokens, token => token.Id == expiredToken.Id);
    }

    [Fact]
    public async Task FindByApplicationIdAsync_ExpiredToken_RemainsAvailableToManager()
    {
        var applicationId = TestEntities.NewId("app");
        var validToken = TestEntities.CreateToken(applicationId: applicationId);
        var expiredToken = TestEntities.CreateToken(applicationId: applicationId);
        expiredToken.ExpirationDate = DateTimeOffset.UtcNow.AddHours(-1);
        await _store.CreateAsync(validToken, CancellationToken.None);
        await _store.CreateAsync(expiredToken, CancellationToken.None);

        var tokens = await _store.FindByApplicationIdAsync(applicationId, CancellationToken.None).ToListAsync();

        Assert.Contains(tokens, token => token.Id == validToken.Id);
        Assert.Contains(tokens, token => token.Id == expiredToken.Id);
    }

    [Fact]
    public async Task FindAsync_ExpiredToken_RemainsAvailableToManager()
    {
        var subject = TestEntities.NewId("subject");
        var validToken = TestEntities.CreateToken(subject: subject);
        var expiredToken = TestEntities.CreateToken(subject: subject);
        expiredToken.ExpirationDate = DateTimeOffset.UtcNow.AddHours(-1);
        await _store.CreateAsync(validToken, CancellationToken.None);
        await _store.CreateAsync(expiredToken, CancellationToken.None);

        var tokens = await _store.FindAsync(subject, client: null, CancellationToken.None).ToListAsync();

        Assert.Contains(tokens, token => token.Id == validToken.Id);
        Assert.Contains(tokens, token => token.Id == expiredToken.Id);
    }

    [Fact]
    public async Task PruneAsync_RemovesPrunableTokens_AndPhysicallyDeletes()
    {
        var token = TestEntities.CreateToken();
        token.Status = OpenIddictConstants.Statuses.Revoked;
        token.CreationDate = DateTimeOffset.UtcNow.AddDays(-30);
        token.ExpirationDate = DateTimeOffset.UtcNow.AddDays(-1);
        await _store.CreateAsync(token, CancellationToken.None);

        var pruned = await _store.PruneAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.True(pruned >= 1);
        var deleted = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task FindByAuthorizationIdAsync_DoesNotFilterExpired()
    {
        var authorizationId = TestEntities.NewId("authz");
        await _authorizationStore.CreateAsync(TestEntities.CreateAuthorization(id: authorizationId), CancellationToken.None);
        var token = TestEntities.CreateToken(authorizationId: authorizationId);
        token.ExpirationDate = DateTimeOffset.UtcNow.AddHours(-1);
        await _store.CreateAsync(token, CancellationToken.None);

        var tokens = await _store.FindByAuthorizationIdAsync(authorizationId, CancellationToken.None).ToListAsync();

        Assert.Single(tokens);
    }

    [Fact]
    public async Task UpdateAsync_RemovingReferenceId_CleansUpLookup()
    {
        var referenceId = TestEntities.NewId("ref");
        var token = TestEntities.CreateToken(referenceId: referenceId);
        await _store.CreateAsync(token, CancellationToken.None);

        await _store.SetReferenceIdAsync(token, null, CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var byOldRef = await _store.FindByReferenceIdAsync(referenceId, CancellationToken.None);
        Assert.Null(byOldRef);
    }

    private static void AssertDynamoDbConcurrencyFailure(OpenIddictExceptions.ConcurrencyException exception)
        => Assert.True(exception.InnerException is ConditionalCheckFailedException or TransactionCanceledException);

    [Fact]
    public async Task FindAsync_BySubjectAndStatusAndType_ReturnsMatching()
    {
        var subject = TestEntities.NewId("subject");
        var matching = TestEntities.CreateToken(subject: subject);
        matching.Type = OpenIddictConstants.TokenTypeHints.AccessToken;
        var nonMatching = TestEntities.CreateToken(subject: subject);
        nonMatching.Type = OpenIddictConstants.TokenTypeHints.RefreshToken;
        await _store.CreateAsync(matching, CancellationToken.None);
        await _store.CreateAsync(nonMatching, CancellationToken.None);

        var tokens = await _store.FindAsync(subject, client: null, OpenIddictConstants.Statuses.Valid, OpenIddictConstants.TokenTypeHints.AccessToken, CancellationToken.None).ToListAsync();

        Assert.Single(tokens);
        Assert.Equal(matching.Id, tokens[0].Id);
    }

    [Fact]
    public async Task ListAsync_ReturnsCreatedTokens()
    {
        var first = TestEntities.CreateToken();
        var second = TestEntities.CreateToken();
        await _store.CreateAsync(first, CancellationToken.None);
        await _store.CreateAsync(second, CancellationToken.None);

        var tokens = await _store.ListAsync(count: null, offset: null, CancellationToken.None).ToListAsync();

        Assert.Contains(tokens, t => t.Id == first.Id);
        Assert.Contains(tokens, t => t.Id == second.Id);
    }

    [Fact]
    public async Task CountAsync_IncludesCreatedTokens()
    {
        var before = await _store.CountAsync(CancellationToken.None);
        await _store.CreateAsync(TestEntities.CreateToken(), CancellationToken.None);
        await _store.CreateAsync(TestEntities.CreateToken(), CancellationToken.None);

        var after = await _store.CountAsync(CancellationToken.None);

        Assert.Equal(before + 2, after);
    }

    [Fact]
    public async Task InstantiateAsync_ReturnsNewInstance()
    {
        var token = await _store.InstantiateAsync(CancellationToken.None);
        Assert.NotNull(token);
        Assert.NotNull(token.ConcurrencyToken);
        Assert.NotEmpty(token.ConcurrencyToken);
    }

    [Fact]
    public async Task GetApplicationIdAsync_ReturnsApplicationId()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetApplicationIdAsync(token, CancellationToken.None);
        Assert.Equal(token.ApplicationId, result);
    }

    [Fact]
    public async Task GetAuthorizationIdAsync_ReturnsAuthorizationId()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetAuthorizationIdAsync(token, CancellationToken.None);
        Assert.Equal(token.AuthorizationId, result);
    }

    [Fact]
    public async Task GetCreationDateAsync_ReturnsCreationDate()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetCreationDateAsync(token, CancellationToken.None);
        Assert.Equal(token.CreationDate, result);
    }

    [Fact]
    public async Task GetExpirationDateAsync_ReturnsExpirationDate()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetExpirationDateAsync(token, CancellationToken.None);
        Assert.Equal(token.ExpirationDate, result);
    }

    [Fact]
    public async Task GetIdAsync_ReturnsId()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetIdAsync(token, CancellationToken.None);
        Assert.Equal(token.Id, result);
    }

    [Fact]
    public async Task GetPayloadAsync_ReturnsPayload()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetPayloadAsync(token, CancellationToken.None);
        Assert.Equal(token.Payload, result);
    }

    [Fact]
    public async Task GetPropertiesAsync_ReturnsDeserializedDictionary()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetPropertiesAsync(token, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetRedemptionDateAsync_ReturnsRedemptionDate()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetRedemptionDateAsync(token, CancellationToken.None);
        Assert.Equal(token.RedemptionDate, result);
    }

    [Fact]
    public async Task GetReferenceIdAsync_ReturnsReferenceId()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetReferenceIdAsync(token, CancellationToken.None);
        Assert.Equal(token.ReferenceId, result);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsStatus()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetStatusAsync(token, CancellationToken.None);
        Assert.Equal(token.Status, result);
    }

    [Fact]
    public async Task GetSubjectAsync_ReturnsSubject()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetSubjectAsync(token, CancellationToken.None);
        Assert.Equal(token.Subject, result);
    }

    [Fact]
    public async Task GetTypeAsync_ReturnsType()
    {
        var token = TestEntities.CreateToken();
        var result = await _store.GetTypeAsync(token, CancellationToken.None);
        Assert.Equal(token.Type, result);
    }

    [Fact]
    public async Task SetApplicationIdAsync_UpdatesApplicationId()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        var newAppId = TestEntities.NewId("newapp");
        await _store.SetApplicationIdAsync(token, newAppId, CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newAppId, stored.ApplicationId);
    }

    [Fact]
    public async Task SetAuthorizationIdAsync_UpdatesAuthorizationId()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        var newAuthzId = TestEntities.NewId("newauthz");
        await _authorizationStore.CreateAsync(TestEntities.CreateAuthorization(id: newAuthzId), CancellationToken.None);
        await _store.SetAuthorizationIdAsync(token, newAuthzId, CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newAuthzId, stored.AuthorizationId);
    }

    [Fact]
    public async Task SetCreationDateAsync_UpdatesCreationDate()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        var newDate = DateTimeOffset.UtcNow.AddDays(-5);
        await _store.SetCreationDateAsync(token, newDate, CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newDate, stored.CreationDate);
    }

    [Fact]
    public async Task SetExpirationDateAsync_UpdatesExpirationDate()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        var newDate = DateTimeOffset.UtcNow.AddDays(7);
        await _store.SetExpirationDateAsync(token, newDate, CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newDate, stored.ExpirationDate);
    }

    [Fact]
    public async Task SetPayloadAsync_UpdatesPayload()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        await _store.SetPayloadAsync(token, "new-payload-data", CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("new-payload-data", stored.Payload);
    }

    [Fact]
    public async Task SetPropertiesAsync_UpdatesProperties()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        using var doc = JsonDocument.Parse("{\"NewProp\":\"NewVal\"}");
        var properties = ImmutableDictionary.CreateRange(new[] { KeyValuePair.Create("NewProp", doc.RootElement) });
        await _store.SetPropertiesAsync(token, properties, CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        var result = await _store.GetPropertiesAsync(stored, CancellationToken.None);
        Assert.True(result.ContainsKey("NewProp"));
    }

    [Fact]
    public async Task SetRedemptionDateAsync_UpdatesRedemptionDate()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        var newDate = DateTimeOffset.UtcNow;
        await _store.SetRedemptionDateAsync(token, newDate, CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newDate, stored.RedemptionDate);
    }

    [Fact]
    public async Task SetReferenceIdAsync_UpdatesReferenceId()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        var newRefId = TestEntities.NewId("newref");
        await _store.SetReferenceIdAsync(token, newRefId, CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newRefId, stored.ReferenceId);
    }

    [Fact]
    public async Task SetStatusAsync_UpdatesStatus()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        await _store.SetStatusAsync(token, OpenIddictConstants.Statuses.Inactive, CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.Statuses.Inactive, stored.Status);
    }

    [Fact]
    public async Task SetSubjectAsync_UpdatesSubject()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        var newSubject = TestEntities.NewId("newsubject");
        await _store.SetSubjectAsync(token, newSubject, CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(newSubject, stored.Subject);
    }

    [Fact]
    public async Task SetTypeAsync_UpdatesType()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        await _store.SetTypeAsync(token, OpenIddictConstants.TokenTypeHints.RefreshToken, CancellationToken.None);
        await _store.UpdateAsync(token, CancellationToken.None);

        var stored = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(OpenIddictConstants.TokenTypeHints.RefreshToken, stored.Type);
    }

    [Fact]
    public async Task PruneAsync_RemovesPrunableTokens()
    {
        var token = TestEntities.CreateToken();
        token.Status = OpenIddictConstants.Statuses.Revoked;
        token.CreationDate = DateTimeOffset.UtcNow.AddDays(-30);
        token.ExpirationDate = DateTimeOffset.UtcNow.AddDays(-1);
        await _store.CreateAsync(token, CancellationToken.None);

        var pruned = await _store.PruneAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.True(pruned >= 1);
        var deleted = await _store.FindByIdAsync(token.Id, CancellationToken.None);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task PruneAsync_ReferenceNowOwnedByAnotherToken_DoesNotDeleteLookup()
    {
        var referenceId = TestEntities.NewId("reference");
        var stale = TestEntities.CreateToken(referenceId: referenceId);
        stale.Status = OpenIddictConstants.Statuses.Revoked;
        stale.CreationDate = DateTimeOffset.UtcNow.AddDays(-30);
        stale.ExpirationDate = DateTimeOffset.UtcNow.AddDays(-1);
        await _store.CreateAsync(stale, CancellationToken.None);
        var owner = TestEntities.CreateToken(referenceId: TestEntities.NewId("temporary-reference"));
        await _store.CreateAsync(owner, CancellationToken.None);
        await _fixture.Client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenPk(owner.Id)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.TokenSk)
            },
            UpdateExpression = "SET reference_id = :reference",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":reference"] = DynamoDbHelper.ToAttr(referenceId)
            }
        });
        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _fixture.Options.TokensTableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = DynamoDbHelper.ToAttr(KeyHelper.ReferenceIdPk(referenceId)),
                ["sk"] = DynamoDbHelper.ToAttr(KeyHelper.ReferenceIdSk),
                ["token_id"] = DynamoDbHelper.ToAttr(owner.Id),
                ["reference_id"] = DynamoDbHelper.ToAttr(referenceId)
            }
        });

        await _store.PruneAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        var stored = await _store.FindByReferenceIdAsync(referenceId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(owner.Id, stored.Id);
    }

    [Fact]
    public async Task IQueryableOverloads_ExecuteAgainstMaterializedResults()
    {
        var token = TestEntities.CreateToken();
        await _store.CreateAsync(token, CancellationToken.None);

        Assert.True(await _store.CountAsync(q => q.Where(item => item.Id == token.Id), CancellationToken.None) >= 1);
        Assert.Equal(token.Id, await _store.GetAsync((q, id) => q.Where(item => item.Id == id).Select(item => item.Id), token.Id, CancellationToken.None));
        Assert.Equal([token.Id], await _store.ListAsync((q, id) => q.Where(item => item.Id == id).Select(item => item.Id), token.Id, CancellationToken.None).ToListAsync());
    }

}
