using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb;

public static class OpenIddictDynamoDbExtensions
{
    public static OpenIddictDynamoDbBuilder AddDynamoDb(this OpenIddictBuilder builder)
    {
        builder.Services.TryAddSingleton<OpenIddictDynamoDbOptions>();
        
        builder.Services.TryAddTransient(
            typeof(IOpenIddictApplicationStore<>),
            typeof(OpenIddictDynamoDbApplicationStore<>));
        
        builder.Services.TryAddTransient(
            typeof(IOpenIddictAuthorizationStore<>),
            typeof(OpenIddictDynamoDbAuthorizationStore<>));
        
        builder.Services.TryAddTransient(
            typeof(IOpenIddictScopeStore<>),
            typeof(OpenIddictDynamoDbScopeStore<>));
        
        builder.Services.TryAddTransient(
            typeof(IOpenIddictTokenStore<>),
            typeof(OpenIddictDynamoDbTokenStore<>));
        
        return new OpenIddictDynamoDbBuilder(builder);
    }

    public static OpenIddictBuilder AddDynamoDb(
        this OpenIddictBuilder builder,
        Action<OpenIddictDynamoDbOptions> configuration)
    {
        builder.AddDynamoDb();
        builder.Services.Configure(configuration);
        return builder;
    }
}

public class OpenIddictDynamoDbBuilder
{
    private readonly OpenIddictBuilder _builder;

    public OpenIddictDynamoDbBuilder(OpenIddictBuilder builder)
    {
        _builder = builder;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultApplicationEntity<TApplication>()
        where TApplication : OpenIddictDynamoDbApplication, new()
    {
        _builder.Services.AddSingleton(
            typeof(IOpenIddictApplicationStore<TApplication>),
            typeof(OpenIddictDynamoDbApplicationStore<TApplication>));
        return this;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultAuthorizationEntity<TAuthorization>()
        where TAuthorization : OpenIddictDynamoDbAuthorization, new()
    {
        _builder.Services.AddSingleton(
            typeof(IOpenIddictAuthorizationStore<TAuthorization>),
            typeof(OpenIddictDynamoDbAuthorizationStore<TAuthorization>));
        return this;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultScopeEntity<TScope>()
        where TScope : OpenIddictDynamoDbScope, new()
    {
        _builder.Services.AddSingleton(
            typeof(IOpenIddictScopeStore<TScope>),
            typeof(OpenIddictDynamoDbScopeStore<TScope>));
        return this;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultTokenEntity<TToken>()
        where TToken : OpenIddictDynamoDbToken, new()
    {
        _builder.Services.AddSingleton(
            typeof(IOpenIddictTokenStore<TToken>),
            typeof(OpenIddictDynamoDbTokenStore<TToken>));
        return this;
    }
}
