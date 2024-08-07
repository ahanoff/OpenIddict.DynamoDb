using System.Collections.Immutable;
using System.Text.Json;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb;

public class OpenIddictionDynamoDbTokenStore<TToken> : IOpenIddictTokenStore<TToken>
    where TToken : OpenIddictDynamoDbToken, new()
{
    public ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<TToken>, IQueryable<TResult>> query, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask CreateAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask DeleteAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TToken> FindAsync(string subject, string client, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TToken> FindAsync(string subject, string client, string status, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TToken> FindAsync(string subject, string client, string status, string type,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TToken> FindByApplicationIdAsync(string identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TToken> FindByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TToken?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TToken?> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TToken> FindBySubjectAsync(string subject, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetApplicationIdAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<TToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetAuthorizationIdAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<DateTimeOffset?> GetExpirationDateAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetIdAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetPayloadAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetReferenceIdAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetStatusAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetSubjectAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string?> GetTypeAsync(TToken token, CancellationToken cancellationToken)
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

    public ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<long> RevokeByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetApplicationIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetAuthorizationIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetCreationDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetExpirationDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPayloadAsync(TToken token, string? payload, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPropertiesAsync(TToken token, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetRedemptionDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetReferenceIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetStatusAsync(TToken token, string? status, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetSubjectAsync(TToken token, string? subject, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetTypeAsync(TToken token, string? type, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask UpdateAsync(TToken token, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}