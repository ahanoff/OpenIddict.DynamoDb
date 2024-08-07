using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb;

public class OpenIddictDynamoDbScopeStore<TToken> : IOpenIddictScopeStore<TToken>
    where TToken : OpenIddictDynamoDbScope, new()
{
    public ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<TToken>, IQueryable<TResult>> query, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask CreateAsync(TToken scope, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask DeleteAsync(TToken scope, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TToken?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TToken?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TToken> FindByNamesAsync(ImmutableArray<string> names, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TToken> FindByResourceAsync(string resource, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<TToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetDescriptionAsync(TToken scope, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(TToken scope, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetDisplayNameAsync(TToken scope, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TToken scope, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetIdAsync(TToken scope, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetNameAsync(TToken scope, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TToken scope, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableArray<string>> GetResourcesAsync(TToken scope, CancellationToken cancellationToken)
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

    public ValueTask SetDescriptionAsync(TToken scope, string? description, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetDescriptionsAsync(TToken scope, ImmutableDictionary<CultureInfo, string> descriptions, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetDisplayNameAsync(TToken scope, string? name, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetDisplayNamesAsync(TToken scope, ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetNameAsync(TToken scope, string? name, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPropertiesAsync(TToken scope, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetResourcesAsync(TToken scope, ImmutableArray<string> resources, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask UpdateAsync(TToken scope, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}