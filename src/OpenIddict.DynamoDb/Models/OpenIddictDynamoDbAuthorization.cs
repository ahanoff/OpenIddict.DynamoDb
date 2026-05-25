namespace OpenIddict.DynamoDb.Models;

/// <summary>
/// Represents a OpenIddict authorization
/// </summary>
public class OpenIddictDynamoDbAuthorization
{
    public virtual string? ApplicationId { get; set; }
    public virtual string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();
    public virtual DateTimeOffset? CreationDate { get; set; }
    public virtual string Id { get; set; } = null!;
    public virtual string? Properties { get; set; }
    public virtual string? Scopes { get; set; }
    public virtual string? Status { get; set; }
    public virtual string? Subject { get; set; }
    public virtual string? Type { get; set; }
}
