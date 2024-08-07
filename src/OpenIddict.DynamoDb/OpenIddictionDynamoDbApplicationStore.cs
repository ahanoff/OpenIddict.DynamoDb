using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb;

public class OpenIddictDynamoDbOptions
{
    public string ApplicationsTableName { get; set; } = "OpenIddictApplications";
    public string AuthorizationsTableName { get; set; } = "OpenIddictAuthorizations";
    public string ScopesTableName { get; set; } = "OpenIddictScopes";
    public string TokensTableName { get; set; } = "OpenIddictTokens";
}

public class OpenIddictionDynamoDbApplicationStore<TApplication> : IOpenIddictApplicationStore<TApplication>
    where TApplication: OpenIddictDynamoDbApplication, new()
{
    private readonly IAmazonDynamoDB _client;

    public OpenIddictionDynamoDbApplicationStore(IAmazonDynamoDB client)
    {
        _client = client;
    }
    public ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<TApplication>, IQueryable<TResult>> query, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask CreateAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask DeleteAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TApplication> FindByPostLogoutRedirectUriAsync(string uri, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TApplication> FindByRedirectUriAsync(string uri, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetApplicationTypeAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetClientIdAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetClientSecretAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetClientTypeAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetConsentTypeAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetDisplayNameAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetIdAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TApplication> InstantiateAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TApplication> ListAsync(int? count, int? offset, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetApplicationTypeAsync(TApplication application, string? type, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetClientIdAsync(TApplication application, string? identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetClientSecretAsync(TApplication application, string? secret, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetClientTypeAsync(TApplication application, string? type, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetConsentTypeAsync(TApplication application, string? type, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetDisplayNameAsync(TApplication application, string? name, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetDisplayNamesAsync(TApplication application, ImmutableDictionary<CultureInfo, string> names,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetJsonWebKeySetAsync(TApplication application, JsonWebKeySet? set, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPermissionsAsync(TApplication application, ImmutableArray<string> permissions,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPostLogoutRedirectUrisAsync(TApplication application, ImmutableArray<string> uris,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPropertiesAsync(TApplication application, ImmutableDictionary<string, JsonElement> properties,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetRedirectUrisAsync(TApplication application, ImmutableArray<string> uris, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetRequirementsAsync(TApplication application, ImmutableArray<string> requirements,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetSettingsAsync(TApplication application, ImmutableDictionary<string, string> settings, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask UpdateAsync(TApplication application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}