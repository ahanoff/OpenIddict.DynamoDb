using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb;

public static class OpenIddictDynamoDbExtensions
{
    public static OpenIddictDynamoDbBuilder AddDynamoDb(this OpenIddictCoreBuilder builder)
    {
        AddStores(builder.Services);

        return new OpenIddictDynamoDbBuilder(builder.Services);
    }

    public static OpenIddictDynamoDbBuilder AddDynamoDb(
        this OpenIddictCoreBuilder builder,
        Action<OpenIddictDynamoDbOptions> configuration)
    {
        var options = new OpenIddictDynamoDbOptions();
        configuration(options);

        builder.AddDynamoDb();
        builder.Services.TryAddSingleton<OpenIddictDynamoDbOptions>(_ => options);

        return new OpenIddictDynamoDbBuilder(builder.Services);
    }

    public static OpenIddictDynamoDbBuilder AddDynamoDb(this OpenIddictBuilder builder)
    {
        AddStores(builder.Services);

        return new OpenIddictDynamoDbBuilder(builder.Services);
    }

    public static OpenIddictDynamoDbBuilder AddDynamoDb(
        this OpenIddictBuilder builder,
        Action<OpenIddictDynamoDbOptions> configuration)
    {
        var options = new OpenIddictDynamoDbOptions();
        configuration(options);

        builder.AddDynamoDb();
        builder.Services.TryAddSingleton<OpenIddictDynamoDbOptions>(_ => options);

        return new OpenIddictDynamoDbBuilder(builder.Services);
    }

    private static void AddStores(IServiceCollection services)
    {
        services.TryAddTransient(
            typeof(IOpenIddictApplicationStore<>),
            typeof(OpenIddictDynamoDbApplicationStore<>));

        services.TryAddTransient(
            typeof(IOpenIddictAuthorizationStore<>),
            typeof(OpenIddictDynamoDbAuthorizationStore<>));

        services.TryAddTransient(
            typeof(IOpenIddictScopeStore<>),
            typeof(OpenIddictDynamoDbScopeStore<>));

        services.TryAddTransient(
            typeof(IOpenIddictTokenStore<>),
            typeof(OpenIddictDynamoDbTokenStore<>));
    }
}

public class OpenIddictDynamoDbBuilder
{
    private readonly IServiceCollection _services;

    public OpenIddictDynamoDbBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultApplicationEntity<TApplication>()
        where TApplication : OpenIddictDynamoDbApplication, new()
    {
        _services.AddSingleton(
            typeof(IOpenIddictApplicationStore<TApplication>),
            typeof(OpenIddictDynamoDbApplicationStore<TApplication>));
        return this;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultAuthorizationEntity<TAuthorization>()
        where TAuthorization : OpenIddictDynamoDbAuthorization, new()
    {
        _services.AddSingleton(
            typeof(IOpenIddictAuthorizationStore<TAuthorization>),
            typeof(OpenIddictDynamoDbAuthorizationStore<TAuthorization>));
        return this;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultScopeEntity<TScope>()
        where TScope : OpenIddictDynamoDbScope, new()
    {
        _services.AddSingleton(
            typeof(IOpenIddictScopeStore<TScope>),
            typeof(OpenIddictDynamoDbScopeStore<TScope>));
        return this;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultTokenEntity<TToken>()
        where TToken : OpenIddictDynamoDbToken, new()
    {
        _services.AddSingleton(
            typeof(IOpenIddictTokenStore<TToken>),
            typeof(OpenIddictDynamoDbTokenStore<TToken>));
        return this;
    }
}
