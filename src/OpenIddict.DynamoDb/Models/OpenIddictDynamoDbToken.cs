namespace OpenIddict.DynamoDb.Models;

/// <summary>
/// Represents a OpenIddict token
/// </summary>
public class OpenIddictDynamoDbToken
{
    public virtual string? ApplicationId { get; set; }
    public virtual string? AuthorizationId { get; set; }
    public virtual string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();
    public virtual DateTimeOffset? CreationDate { get; set; }
    public virtual DateTimeOffset? ExpirationDate { get; set; }
    public virtual string Id { get; set; } = null!;
    public virtual string? Payload { get; set; }
    public virtual string? Properties { get; set; }
    public virtual DateTimeOffset? RedemptionDate { get; set; }
    public virtual string? ReferenceId { get; set; }
    public virtual string? Status { get; set; }
    public virtual string? Subject { get; set; }
    public virtual long? TTL { get; set; }
    public virtual string? Type { get; set; }
}
