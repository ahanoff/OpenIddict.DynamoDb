using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb;

public class OpenIddictDynamoDbAuthorizationStore<TToken> : IOpenIddictApplicationStore<TToken>
    where TToken : OpenIddictDynamoDbAuthorization, new()
{
    public ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<TToken>, IQueryable<TResult>> query, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask CreateAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask DeleteAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TToken?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TToken?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TToken> FindByPostLogoutRedirectUriAsync(string uri, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TToken> FindByRedirectUriAsync(string uri, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetApplicationTypeAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<TToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetClientIdAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetClientSecretAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetClientTypeAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetConsentTypeAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetDisplayNameAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetIdAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TToken> InstantiateAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TToken> ListAsync(int? count, int? offset, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<TToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetApplicationTypeAsync(TToken application, string? type, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetClientIdAsync(TToken application, string? identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetClientSecretAsync(TToken application, string? secret, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetClientTypeAsync(TToken application, string? type, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetConsentTypeAsync(TToken application, string? type, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetDisplayNameAsync(TToken application, string? name, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetDisplayNamesAsync(TToken application, ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetJsonWebKeySetAsync(TToken application, JsonWebKeySet? set, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPermissionsAsync(TToken application, ImmutableArray<string> permissions, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPostLogoutRedirectUrisAsync(TToken application, ImmutableArray<string> uris, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPropertiesAsync(TToken application, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetRedirectUrisAsync(TToken application, ImmutableArray<string> uris, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetRequirementsAsync(TToken application, ImmutableArray<string> requirements, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetSettingsAsync(TToken application, ImmutableDictionary<string, string> settings, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask UpdateAsync(TToken application, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}