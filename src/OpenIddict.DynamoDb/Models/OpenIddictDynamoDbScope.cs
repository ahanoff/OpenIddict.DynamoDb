using System.Collections.ObjectModel;

namespace OpenIddict.DynamoDb.Models;

/// <summary>
/// Represents a OpenIddict scope
/// </summary>
public class OpenIddictDynamoDbScope
{
    public required string Id { get; set; }
    public string? ConcurrencyToken { get; set; }
    public string? Description { get; set; }
    public IReadOnlyCollection<string> Descriptions { get; set; } = ReadOnlyCollection<string>.Empty;
    public string? DisplayName { get; set; }
    public IReadOnlyCollection<string> DisplayNames { get; set; } = ReadOnlyCollection<string>.Empty;
    public required string Name { get; set; }
    public string? Properties { get; set; }
    public string? Resources { get; set; }
}