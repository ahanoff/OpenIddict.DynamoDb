using System.Collections.Immutable;

namespace OpenIddict.DynamoDb.Models;

/// <summary>
/// Represents a OpenIddict authorization
/// </summary>
public class OpenIddictDynamoDbAuthorization
{
    /// <summary>
    /// Gets or sets the identifier of the application associated with the current authorization.
    /// </summary>
    public virtual required string ApplicationId { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token.
    /// </summary>
    public virtual string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the UTC creation date of the current authorization.
    /// </summary>
    public virtual DateTime? CreationDate { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier associated with the current authorization.
    /// </summary>
    public virtual required string Id { get; set; }

    /// <summary>
    /// Gets or sets the additional properties associated with the current authorization.
    /// </summary>
    public virtual string? Properties { get; set; }

    /// <summary>
    /// Gets or sets the scopes associated with the current authorization.
    /// </summary>
    public virtual IReadOnlyList<string>? Scopes { get; set; } = ImmutableList.Create<string>();

    /// <summary>
    /// Gets or sets the status of the current authorization.
    /// </summary>
    public virtual string? Status { get; set; }

    /// <summary>
    /// Gets or sets the subject associated with the current authorization.
    /// </summary>
    public virtual string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the type of the current authorization.
    /// </summary>
    public virtual string? Type { get; set; }
}