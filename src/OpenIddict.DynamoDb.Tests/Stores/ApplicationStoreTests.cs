using Amazon.DynamoDBv2.Model;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;
using OpenIddict.DynamoDb.Tests.Fixtures;
using OpenIddict.DynamoDb.Tests.Helpers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

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

    [Fact]
    public async Task FindByRedirectUriAsync_ReturnsMatchingApplications()
    {
        var redirectUri = $"https://example.com/{Guid.NewGuid():N}/callback";
        var application = TestEntities.CreateApplication();
        application.RedirectUris = TestEntities.JsonArray(redirectUri);
        await _store.CreateAsync(application, CancellationToken.None);

        var applications = await _store.FindByRedirectUriAsync(redirectUri, CancellationToken.None).ToListAsync();

        Assert.Single(applications);
        Assert.Equal(application.Id, applications[0].Id);
    }

    [Fact]
    public async Task FindByPostLogoutRedirectUriAsync_ReturnsMatchingApplications()
    {
        var uri = $"https://example.com/{Guid.NewGuid():N}/signout-callback";
        var application = TestEntities.CreateApplication();
        application.PostLogoutRedirectUris = TestEntities.JsonArray(uri);
        await _store.CreateAsync(application, CancellationToken.None);

        var applications = await _store.FindByPostLogoutRedirectUriAsync(uri, CancellationToken.None).ToListAsync();

        Assert.Single(applications);
        Assert.Equal(application.Id, applications[0].Id);
    }

    [Fact]
    public async Task InstantiateAsync_ReturnsNewInstance()
    {
        var application = await _store.InstantiateAsync(CancellationToken.None);

        Assert.NotNull(application);
        Assert.NotNull(application.ConcurrencyToken);
        Assert.NotEmpty(application.ConcurrencyToken);
    }

    [Fact]
    public async Task GetApplicationTypeAsync_ReturnsApplicationType()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetApplicationTypeAsync(application, CancellationToken.None);
        Assert.Equal(application.ApplicationType, result);
    }

    [Fact]
    public async Task GetClientIdAsync_ReturnsClientId()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetClientIdAsync(application, CancellationToken.None);
        Assert.Equal(application.ClientId, result);
    }

    [Fact]
    public async Task GetClientSecretAsync_ReturnsClientSecret()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetClientSecretAsync(application, CancellationToken.None);
        Assert.Equal(application.ClientSecret, result);
    }

    [Fact]
    public async Task GetClientTypeAsync_ReturnsClientType()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetClientTypeAsync(application, CancellationToken.None);
        Assert.Equal(application.ClientType, result);
    }

    [Fact]
    public async Task GetConsentTypeAsync_ReturnsConsentType()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetConsentTypeAsync(application, CancellationToken.None);
        Assert.Equal(application.ConsentType, result);
    }

    [Fact]
    public async Task GetDisplayNameAsync_ReturnsDisplayName()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetDisplayNameAsync(application, CancellationToken.None);
        Assert.Equal(application.DisplayName, result);
    }

    [Fact]
    public async Task GetDisplayNamesAsync_ReturnsDeserializedDictionary()
    {
        var application = TestEntities.CreateApplication();
        var names = ImmutableDictionary.CreateBuilder<CultureInfo, string>();
        names[CultureInfo.GetCultureInfo("en")] = "English Name";
        names[CultureInfo.GetCultureInfo("fr")] = "French Name";
        await _store.SetDisplayNamesAsync(application, names.ToImmutable(), CancellationToken.None);
        await _store.CreateAsync(application, CancellationToken.None);
        var stored = await _store.FindByIdAsync(application.Id, CancellationToken.None);

        var result = await _store.GetDisplayNamesAsync(stored!, CancellationToken.None);

        Assert.Equal("English Name", result[CultureInfo.GetCultureInfo("en")]);
        Assert.Equal("French Name", result[CultureInfo.GetCultureInfo("fr")]);
    }

    [Fact]
    public async Task GetIdAsync_ReturnsId()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetIdAsync(application, CancellationToken.None);
        Assert.Equal(application.Id, result);
    }

    [Fact]
    public async Task GetJsonWebKeySetAsync_ReturnsDeserializedJsonWebKeySet()
    {
        var application = TestEntities.CreateApplication();
        await _store.SetJsonWebKeySetAsync(application, new JsonWebKeySet("{\"keys\":[]}"), CancellationToken.None);
        await _store.CreateAsync(application, CancellationToken.None);
        var stored = await _store.FindByIdAsync(application.Id, CancellationToken.None);

        var result = await _store.GetJsonWebKeySetAsync(stored!, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result.Keys);
    }

    [Fact]
    public async Task GetPermissionsAsync_ReturnsDeserializedArray()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetPermissionsAsync(application, CancellationToken.None);
        Assert.Contains(OpenIddictConstants.Permissions.Prefixes.Endpoint + "authorization", result);
        Assert.Contains(OpenIddictConstants.Permissions.Prefixes.Scope + "openid", result);
    }

    [Fact]
    public async Task GetPostLogoutRedirectUrisAsync_ReturnsDeserializedArray()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetPostLogoutRedirectUrisAsync(application, CancellationToken.None);
        Assert.Contains($"https://example.com/{application.Id}/signout-callback", result);
    }

    [Fact]
    public async Task GetPropertiesAsync_ReturnsDeserializedDictionary()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetPropertiesAsync(application, CancellationToken.None);
        Assert.Equal("value", result["property"].GetString());
    }

    [Fact]
    public async Task GetRedirectUrisAsync_ReturnsDeserializedArray()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetRedirectUrisAsync(application, CancellationToken.None);
        Assert.Contains($"https://example.com/{application.Id}/callback", result);
    }

    [Fact]
    public async Task GetRequirementsAsync_ReturnsDeserializedArray()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetRequirementsAsync(application, CancellationToken.None);
        Assert.True(ImmutableArray.Create("pkce").SequenceEqual(result));
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsDeserializedDictionary()
    {
        var application = await CreateAndReloadApplicationAsync();
        var result = await _store.GetSettingsAsync(application, CancellationToken.None);
        Assert.Equal("value", result["setting"]);
    }

    [Fact]
    public async Task SetApplicationTypeAsync_UpdatesApplicationType()
    {
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetApplicationTypeAsync(application, "native", token));
        Assert.Equal("native", stored.ApplicationType);
    }

    [Fact]
    public async Task SetClientIdAsync_UpdatesClientId()
    {
        var clientId = TestEntities.NewId("client");
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetClientIdAsync(application, clientId, token));

        Assert.Equal(clientId, stored.ClientId);
        var found = await _store.FindByClientIdAsync(clientId, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(stored.Id, found.Id);
    }

    [Fact]
    public async Task SetClientSecretAsync_UpdatesClientSecret()
    {
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetClientSecretAsync(application, "new-secret", token));
        Assert.Equal("new-secret", stored.ClientSecret);
    }

    [Fact]
    public async Task SetClientTypeAsync_UpdatesClientType()
    {
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetClientTypeAsync(application, OpenIddictConstants.ClientTypes.Public, token));
        Assert.Equal(OpenIddictConstants.ClientTypes.Public, stored.ClientType);
    }

    [Fact]
    public async Task SetConsentTypeAsync_UpdatesConsentType()
    {
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetConsentTypeAsync(application, OpenIddictConstants.ConsentTypes.Implicit, token));
        Assert.Equal(OpenIddictConstants.ConsentTypes.Implicit, stored.ConsentType);
    }

    [Fact]
    public async Task SetDisplayNameAsync_UpdatesDisplayName()
    {
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetDisplayNameAsync(application, "New Display Name", token));
        Assert.Equal("New Display Name", stored.DisplayName);
    }

    [Fact]
    public async Task SetDisplayNamesAsync_UpdatesDisplayNames()
    {
        var names = ImmutableDictionary.CreateBuilder<CultureInfo, string>();
        names[CultureInfo.GetCultureInfo("en")] = "English Name";
        names[CultureInfo.GetCultureInfo("de")] = "German Name";
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetDisplayNamesAsync(application, names.ToImmutable(), token));

        var result = await _store.GetDisplayNamesAsync(stored, CancellationToken.None);
        Assert.Equal("English Name", result[CultureInfo.GetCultureInfo("en")]);
        Assert.Equal("German Name", result[CultureInfo.GetCultureInfo("de")]);
    }

    [Fact]
    public async Task SetJsonWebKeySetAsync_UpdatesJsonWebKeySet()
    {
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetJsonWebKeySetAsync(application, new JsonWebKeySet("{\"keys\":[]}"), token));

        var result = await _store.GetJsonWebKeySetAsync(stored, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Empty(result.Keys);
    }

    [Fact]
    public async Task SetPermissionsAsync_UpdatesPermissions()
    {
        var permissions = ImmutableArray.Create("endpoint:token", "grant_type:client_credentials");
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetPermissionsAsync(application, permissions, token));

        var result = await _store.GetPermissionsAsync(stored, CancellationToken.None);
        Assert.True(permissions.SequenceEqual(result));
    }

    [Fact]
    public async Task SetPostLogoutRedirectUrisAsync_UpdatesPostLogoutRedirectUris()
    {
        var uri = $"https://example.com/{Guid.NewGuid():N}/logout";
        var uris = ImmutableArray.Create(uri);
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetPostLogoutRedirectUrisAsync(application, uris, token));

        var result = await _store.GetPostLogoutRedirectUrisAsync(stored, CancellationToken.None);
        Assert.True(uris.SequenceEqual(result));
        var applications = await _store.FindByPostLogoutRedirectUriAsync(uri, CancellationToken.None).ToListAsync();
        Assert.Single(applications);
        Assert.Equal(stored.Id, applications[0].Id);
    }

    [Fact]
    public async Task SetPropertiesAsync_UpdatesProperties()
    {
        using var doc = JsonDocument.Parse("{\"NewProp\":\"NewVal\"}");
        var properties = ImmutableDictionary.CreateRange(new[] { KeyValuePair.Create("NewProp", doc.RootElement) });
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetPropertiesAsync(application, properties, token));

        var result = await _store.GetPropertiesAsync(stored, CancellationToken.None);
        Assert.Equal("NewVal", result["NewProp"].GetString());
    }

    [Fact]
    public async Task SetRedirectUrisAsync_UpdatesRedirectUris()
    {
        var uri = $"https://example.com/{Guid.NewGuid():N}/callback";
        var uris = ImmutableArray.Create(uri);
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetRedirectUrisAsync(application, uris, token));

        var result = await _store.GetRedirectUrisAsync(stored, CancellationToken.None);
        Assert.True(uris.SequenceEqual(result));
        var applications = await _store.FindByRedirectUriAsync(uri, CancellationToken.None).ToListAsync();
        Assert.Single(applications);
        Assert.Equal(stored.Id, applications[0].Id);
    }

    [Fact]
    public async Task SetRequirementsAsync_UpdatesRequirements()
    {
        var requirements = ImmutableArray.Create("pkce", "mfa");
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetRequirementsAsync(application, requirements, token));

        var result = await _store.GetRequirementsAsync(stored, CancellationToken.None);
        Assert.True(requirements.SequenceEqual(result));
    }

    [Fact]
    public async Task SetSettingsAsync_UpdatesSettings()
    {
        var settings = ImmutableDictionary.CreateRange(new[]
        {
            KeyValuePair.Create("setting", "new-value"),
            KeyValuePair.Create("another", "value")
        });
        var stored = await SetAndReloadApplicationAsync((application, token) => _store.SetSettingsAsync(application, settings, token));

        var result = await _store.GetSettingsAsync(stored, CancellationToken.None);
        Assert.Equal("new-value", result["setting"]);
        Assert.Equal("value", result["another"]);
    }

    [Fact]
    public async Task CountAsync_WithIQueryable_ThrowsNotSupportedException()
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => _store.CountAsync<OpenIddictDynamoDbApplication>(q => q, CancellationToken.None).AsTask());
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

    [Fact]
    public async Task UpdateAsync_ChangingClientId_OldClientIdLookupStopsResolving()
    {
        var application = TestEntities.CreateApplication();
        await _store.CreateAsync(application, CancellationToken.None);

        var oldClientId = application.ClientId!;
        var newClientId = TestEntities.NewId("new-client");
        await _store.SetClientIdAsync(application, newClientId, CancellationToken.None);
        await _store.UpdateAsync(application, CancellationToken.None);

        var byOld = await _store.FindByClientIdAsync(oldClientId, CancellationToken.None);
        Assert.Null(byOld);

        var byNew = await _store.FindByClientIdAsync(newClientId, CancellationToken.None);
        Assert.NotNull(byNew);
        Assert.Equal(application.Id, byNew.Id);
    }

    [Fact]
    public async Task UpdateAsync_ChangingRedirectUris_OldUriLookupStopsResolving()
    {
        var oldUri = $"https://example.com/{Guid.NewGuid():N}/callback";
        var application = TestEntities.CreateApplication();
        application.RedirectUris = TestEntities.JsonArray(oldUri);
        await _store.CreateAsync(application, CancellationToken.None);

        var newUri = $"https://example.com/{Guid.NewGuid():N}/callback";
        await _store.SetRedirectUrisAsync(application, ImmutableArray.Create(newUri), CancellationToken.None);
        await _store.UpdateAsync(application, CancellationToken.None);

        var byOldUri = await _store.FindByRedirectUriAsync(oldUri, CancellationToken.None).ToListAsync();
        Assert.Empty(byOldUri);

        var byNewUri = await _store.FindByRedirectUriAsync(newUri, CancellationToken.None).ToListAsync();
        Assert.Single(byNewUri);
        Assert.Equal(application.Id, byNewUri[0].Id);
    }

    [Fact]
    public async Task DeleteAsync_CleansUpAllLookups()
    {
        var redirectUri = $"https://example.com/{Guid.NewGuid():N}/callback";
        var postLogoutUri = $"https://example.com/{Guid.NewGuid():N}/signout-callback";
        var application = TestEntities.CreateApplication();
        application.RedirectUris = TestEntities.JsonArray(redirectUri);
        application.PostLogoutRedirectUris = TestEntities.JsonArray(postLogoutUri);
        await _store.CreateAsync(application, CancellationToken.None);

        await _store.DeleteAsync(application, CancellationToken.None);

        Assert.Null(await _store.FindByClientIdAsync(application.ClientId!, CancellationToken.None));
        Assert.Empty(await _store.FindByRedirectUriAsync(redirectUri, CancellationToken.None).ToListAsync());
        Assert.Empty(await _store.FindByPostLogoutRedirectUriAsync(postLogoutUri, CancellationToken.None).ToListAsync());
    }

    private async Task<OpenIddictDynamoDbApplication> CreateAndReloadApplicationAsync()
    {
        var application = TestEntities.CreateApplication();
        await _store.CreateAsync(application, CancellationToken.None);

        var stored = await _store.FindByIdAsync(application.Id, CancellationToken.None);
        Assert.NotNull(stored);
        return stored;
    }

    private async Task<OpenIddictDynamoDbApplication> SetAndReloadApplicationAsync(
        Func<OpenIddictDynamoDbApplication, CancellationToken, ValueTask> setter)
    {
        var application = TestEntities.CreateApplication();
        await _store.CreateAsync(application, CancellationToken.None);

        await setter(application, CancellationToken.None);
        await _store.UpdateAsync(application, CancellationToken.None);

        var stored = await _store.FindByIdAsync(application.Id, CancellationToken.None);
        Assert.NotNull(stored);
        return stored;
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
