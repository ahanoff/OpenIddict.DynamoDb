namespace OpenIddict.DynamoDb.Models;

/// <summary>
/// Represents a OpenIddict scope
/// </summary>
public class OpenIddictDynamoDbScope
{
    public required string Id { get; set; }
    public string? ConcurrencyToken { get; set; }
    public string? Description { get; set; }
    public string? Descriptions { get; set; }
    public string? DisplayName { get; set; }
    public string? DisplayNames { get; set; }
    public required string Name { get; set; }
    public string? Properties { get; set; }
    public string? Resources { get; set; }
}