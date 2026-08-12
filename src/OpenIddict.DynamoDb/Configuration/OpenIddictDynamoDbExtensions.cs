using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Models;

namespace OpenIddict.DynamoDb;

public static class OpenIddictDynamoDbExtensions
{
    public static OpenIddictDynamoDbBuilder AddDynamoDb(this OpenIddictCoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.SetDefaultApplicationEntity<OpenIddictDynamoDbApplication>();
        builder.SetDefaultAuthorizationEntity<OpenIddictDynamoDbAuthorization>();
        builder.SetDefaultScopeEntity<OpenIddictDynamoDbScope>();
        builder.SetDefaultTokenEntity<OpenIddictDynamoDbToken>();
        AddStores(builder.Services);
        builder.Services.TryAddSingleton(static _ => new OpenIddictDynamoDbOptions());

        return new OpenIddictDynamoDbBuilder(builder);
    }

    public static OpenIddictDynamoDbBuilder AddDynamoDb(
        this OpenIddictCoreBuilder builder,
        Action<OpenIddictDynamoDbOptions> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new OpenIddictDynamoDbOptions();
        configuration(options);
        options.Validate();

        builder.AddDynamoDb();
        builder.Services.Replace(ServiceDescriptor.Singleton(options));

        return new OpenIddictDynamoDbBuilder(builder);
    }

    public static OpenIddictDynamoDbBuilder AddDynamoDb(this OpenIddictBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddCore().AddDynamoDb();
    }

    public static OpenIddictDynamoDbBuilder AddDynamoDb(
        this OpenIddictBuilder builder,
        Action<OpenIddictDynamoDbOptions> configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        return builder.AddCore().AddDynamoDb(configuration);
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
    private readonly OpenIddictCoreBuilder _builder;

    public OpenIddictDynamoDbBuilder(OpenIddictCoreBuilder builder)
    {
        _builder = builder;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultApplicationEntity<TApplication>()
        where TApplication : OpenIddictDynamoDbApplication, new()
    {
        _builder.SetDefaultApplicationEntity<TApplication>();
        return this;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultAuthorizationEntity<TAuthorization>()
        where TAuthorization : OpenIddictDynamoDbAuthorization, new()
    {
        _builder.SetDefaultAuthorizationEntity<TAuthorization>();
        return this;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultScopeEntity<TScope>()
        where TScope : OpenIddictDynamoDbScope, new()
    {
        _builder.SetDefaultScopeEntity<TScope>();
        return this;
    }

    public OpenIddictDynamoDbBuilder ReplaceDefaultTokenEntity<TToken>()
        where TToken : OpenIddictDynamoDbToken, new()
    {
        _builder.SetDefaultTokenEntity<TToken>();
        return this;
    }
}
