namespace OpenIddict.DynamoDb.Models;

/// <summary>
/// Represents a OpenIddict token
/// </summary>
public class OpenIddictDynamoDbToken
{
    public virtual required string ApplicationId { get; set; } 
    public virtual required string AuthorizationId { get; set; }
    public virtual string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();
    public virtual DateTime? CreationDate { get; set; }
    public virtual DateTime? ExpirationDate { get; set; }
    public virtual required string Id { get; set; }
    public virtual string? Payload { get; set; }
    public virtual string? Properties { get; set; }
    public virtual DateTime? RedemptionDate { get; set; }
    public virtual string? ReferenceId { get; set; }
    public virtual string? Status { get; set; }
    public virtual string? Subject { get; set; }
    public virtual string? Type { get; set; }
}