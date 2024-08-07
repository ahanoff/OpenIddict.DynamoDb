using System.Collections.Immutable;

namespace OpenIddict.DynamoDb.Models;

/// <summary>
/// Represents a OpenIddict application
/// </summary>
public class OpenIddictDynamoDbApplication
{
    public required string Id { get; set; }
    public required string ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? ConcurrencyToken { get; set; }
    public string? ConsentType { get; set; }
    public string? DisplayName { get; set; }
    public IReadOnlyList<string> DisplayNames { get; set; } = ImmutableList<string>.Empty;
    public IReadOnlyList<string> Permissions { get; set; } = ImmutableList<string>.Empty;
    public IReadOnlyList<string> PostLogoutRedirectUris { get; set; } = ImmutableList<string>.Empty;
    public string? Properties { get; set; }
    public IReadOnlyList<string> RedirectUris { get; set; } = ImmutableList<string>.Empty;
    public IReadOnlyList<string> Requirements { get; set; } = ImmutableList<string>.Empty;
    public string? ClientType { get; set; }
    public string? ApplicationType { get; set; }
    public string? JsonWebKeySet { get; set; }
    public IReadOnlyDictionary<string, string>? Settings { get; set; }
}