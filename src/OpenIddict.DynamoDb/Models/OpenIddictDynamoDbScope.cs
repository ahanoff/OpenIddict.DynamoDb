namespace OpenIddict.DynamoDb.Models;

/// <summary>
/// Represents a OpenIddict scope
/// </summary>
public class OpenIddictDynamoDbScope
{
    public virtual string Id { get; set; } = null!;
    public virtual string? ConcurrencyToken { get; set; }
    public virtual string? Description { get; set; }
    public virtual string? Descriptions { get; set; }
    public virtual string? DisplayName { get; set; }
    public virtual string? DisplayNames { get; set; }
    public virtual string? Name { get; set; }
    public virtual string? Properties { get; set; }
    public virtual string? Resources { get; set; }
}
