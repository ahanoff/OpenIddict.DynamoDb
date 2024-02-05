using System.Collections.Immutable;
using Amazon.DynamoDBv2.DataModel;

namespace OpenIddict.DynamoDb.Models;

/// <summary>
/// Represents a OpenIddict authorization
/// </summary>
public class OpenIddictDynamoDbAuthorization
{
    public virtual required string ApplicationId { get; set; }
    public virtual string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();
    public virtual DateTime? CreationDate { get; set; }
    public virtual required string Id { get; set; }
    public virtual string? Properties { get; set; }
    public virtual IReadOnlyList<string>? Scopes { get; set; } = ImmutableList.Create<string>();
    public virtual string? Status { get; set; }
    public virtual string? Subject { get; set; }
    public virtual string? Type { get; set; }
}