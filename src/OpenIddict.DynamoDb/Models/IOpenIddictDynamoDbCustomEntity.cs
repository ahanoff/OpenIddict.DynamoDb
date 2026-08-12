namespace OpenIddict.DynamoDb.Models;

internal interface IOpenIddictDynamoDbCustomEntity
{
    Dictionary<string, string> CustomPropertyData { get; }
}
