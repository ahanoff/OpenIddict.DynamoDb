using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.DynamoDb.Helpers;
using OpenIddict.DynamoDb.Models;
using OpenIddict.DynamoDb.Tests.Fixtures;
using OpenIddict.DynamoDb.Tests.Helpers;

namespace OpenIddict.DynamoDb.Tests;

[Collection(DynamoDbCollection.Name)]
public sealed class ConfigurationAndKeyTests
{
    private readonly DynamoDbFixture _fixture;

    public ConfigurationAndKeyTests(DynamoDbFixture fixture)
        => _fixture = fixture;

    [Fact]
    public void AddDynamoDb_RegistersAllUntypedManagers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAmazonDynamoDB>(_fixture.Client);
        services.AddOpenIddict().AddCore(options => options.AddDynamoDb(settings =>
        {
            settings.ApplicationsTableName = _fixture.Options.ApplicationsTableName;
            settings.AuthorizationsTableName = _fixture.Options.AuthorizationsTableName;
            settings.ScopesTableName = _fixture.Options.ScopesTableName;
            settings.TokensTableName = _fixture.Options.TokensTableName;
        }));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsAssignableFrom<IOpenIddictApplicationManager>(scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>());
        Assert.IsAssignableFrom<IOpenIddictAuthorizationManager>(scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>());
        Assert.IsAssignableFrom<IOpenIddictScopeManager>(scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>());
        Assert.IsAssignableFrom<IOpenIddictTokenManager>(scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>());
    }

    [Fact]
    public void AddDynamoDb_WithoutConfiguration_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        services.AddOpenIddict().AddCore(options => options.AddDynamoDb());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<OpenIddictDynamoDbOptions>());
    }

    [Fact]
    public void ReplaceDefaultEntities_RegistersCustomManagers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAmazonDynamoDB>(_fixture.Client);
        services.AddOpenIddict().AddCore(options => options.AddDynamoDb(settings =>
        {
            settings.ApplicationsTableName = _fixture.Options.ApplicationsTableName;
            settings.AuthorizationsTableName = _fixture.Options.AuthorizationsTableName;
            settings.ScopesTableName = _fixture.Options.ScopesTableName;
            settings.TokensTableName = _fixture.Options.TokensTableName;
        })
            .ReplaceDefaultApplicationEntity<CustomApplication>()
            .ReplaceDefaultAuthorizationEntity<CustomAuthorization>()
            .ReplaceDefaultScopeEntity<CustomScope>()
            .ReplaceDefaultTokenEntity<CustomToken>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Contains(nameof(CustomApplication), scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().GetType().FullName);
        Assert.Contains(nameof(CustomAuthorization), scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>().GetType().FullName);
        Assert.Contains(nameof(CustomScope), scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>().GetType().FullName);
        Assert.Contains(nameof(CustomToken), scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>().GetType().FullName);
    }

    [Fact]
    public void CompositeSubjectApplicationKey_IsCollisionSafe()
    {
        Assert.NotEqual(
            KeyHelper.TokenSubjectAppIndexPk("a#b", "c"),
            KeyHelper.TokenSubjectAppIndexPk("a", "b#c"));
    }

    [Fact]
    public void ApplicationShard_UsesStableHashVector()
    {
        Assert.Equal("APP#1fe289205936c3fdb61158223892c7a8bee6ff4dfa085ea1c094ce0294e32114#S#6", KeyHelper.TokenApplicationShardedIndexPk("application", "token-123", 10));
    }

    [Fact]
    public void DateAttributes_AreNormalizedToUtc()
    {
        var value = new DateTimeOffset(2026, 8, 12, 12, 30, 0, TimeSpan.FromHours(8));

        Assert.Equal("2026-08-12T04:30:00.0000000+00:00", DynamoDbHelper.ToAttr(value).S);
    }

    [Fact]
    public void DefaultMutationLimits_FitOneWorstCaseTransaction()
    {
        var options = new OpenIddictDynamoDbOptions();

        Assert.True(1 + 2 + (2 * options.MaxRedirectUrisPerApplication) <= 100);
        Assert.True(1 + 2 + (2 * options.MaxResourcesPerScope) <= 100);
    }

    [Fact]
    public void ZeroShardCount_FailsAtStoreBoundary()
    {
        var options = new OpenIddictDynamoDbOptions
        {
            ApplicationsTableName = _fixture.Options.ApplicationsTableName,
            AuthorizationsTableName = _fixture.Options.AuthorizationsTableName,
            ScopesTableName = _fixture.Options.ScopesTableName,
            TokensTableName = _fixture.Options.TokensTableName,
            TokenApplicationShardCount = 0
        };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OpenIddictDynamoDbTokenStore<OpenIddictDynamoDbToken>(_fixture.Client, options));
    }

    [Fact]
    public async Task TableCreator_IsIdempotentWhenTtlIsAlreadyEnabled()
    {
        await OpenIddictDynamoDbTableCreator.CreateTablesAsync(
            _fixture.Client,
            _fixture.Options,
            CancellationToken.None);
    }
}
