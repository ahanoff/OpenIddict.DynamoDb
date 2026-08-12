using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace OpenIddict.DynamoDb.Helpers;

internal static class RevocationMarkerHelper
{
    internal static Dictionary<string, AttributeValue> CreateKey(
        string entity,
        string? subject,
        string? applicationId,
        string? authorizationId,
        string? status,
        string? type)
        => new()
        {
            ["pk"] = DynamoDbHelper.ToAttr(CreatePartitionKey(entity, subject, applicationId, authorizationId)),
            ["sk"] = DynamoDbHelper.ToAttr($"#FILTER#{HashNullable(status)}#{HashNullable(type)}")
        };

    internal static IEnumerable<Dictionary<string, AttributeValue>> CreateCandidateKeys(
        string entity,
        string? subject,
        string? applicationId,
        string? authorizationId,
        string? status,
        string? type)
    {
        var dimensions = new (string? Subject, string? ApplicationId, string? AuthorizationId)[]
        {
            (null, null, null),
            (subject, null, null),
            (null, applicationId, null),
            (subject, applicationId, null),
            (null, null, authorizationId)
        };
        var filters = new (string? Status, string? Type)[]
        {
            (null, null),
            (status, null),
            (null, type),
            (status, type)
        };

        return dimensions
            .SelectMany(dimension => filters.Select(filter =>
                CreateKey(entity, dimension.Subject, dimension.ApplicationId, dimension.AuthorizationId, filter.Status, filter.Type)))
            .DistinctBy(key => $"{DynamoDbHelper.GetString(key, "pk")}\0{DynamoDbHelper.GetString(key, "sk")}", StringComparer.Ordinal);
    }

    internal static Dictionary<string, AttributeValue> CreateItem(
        string entity,
        string? subject,
        string? applicationId,
        string? authorizationId,
        string? status,
        string? type,
        DateTimeOffset cutoff)
    {
        var item = CreateKey(entity, subject, applicationId, authorizationId, status, type);
        item["revoked_at"] = new AttributeValue { N = cutoff.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture) };
        return item;
    }

    internal static async Task WriteAsync(
        IAmazonDynamoDB client,
        string tableName,
        string entity,
        string? subject,
        string? applicationId,
        string? authorizationId,
        string? status,
        string? type,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var item = CreateItem(entity, subject, applicationId, authorizationId, status, type, cutoff);
        try
        {
            await client.PutItemAsync(new PutItemRequest
            {
                TableName = tableName,
                Item = item,
                ConditionExpression = "attribute_not_exists(#cutoff) OR #cutoff < :cutoff",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#cutoff"] = "revoked_at"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":cutoff"] = item["revoked_at"]
                }
            }, cancellationToken);
        }
        catch (ConditionalCheckFailedException)
        {
            // A concurrent revocation already stored the same or a later cutoff.
        }
    }

    internal static bool Applies(IEnumerable<Dictionary<string, AttributeValue>> markers, DateTimeOffset? creationDate)
    {
        var createdAt = creationDate?.ToUnixTimeMilliseconds();
        return markers.Any(marker =>
            DynamoDbHelper.GetLong(marker, "revoked_at") is long cutoff &&
            (!createdAt.HasValue || createdAt.Value <= cutoff));
    }

    internal static IEnumerable<TransactWriteItem> CreateNonApplicableConditions(
        string tableName,
        string entity,
        string? subject,
        string? applicationId,
        string? authorizationId,
        string? status,
        string? type,
        DateTimeOffset? creationDate)
    {
        foreach (var key in CreateCandidateKeys(entity, subject, applicationId, authorizationId, status, type))
        {
            var check = new ConditionCheck
            {
                TableName = tableName,
                Key = key,
                ConditionExpression = "attribute_not_exists(revoked_at)"
            };
            if (creationDate.HasValue)
            {
                check.ConditionExpression += " OR revoked_at < :created_at";
                check.ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":created_at"] = new AttributeValue
                    {
                        N = creationDate.Value.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture)
                    }
                };
            }

            yield return new TransactWriteItem { ConditionCheck = check };
        }
    }

    private static string CreatePartitionKey(string entity, string? subject, string? applicationId, string? authorizationId)
    {
        var value = $"{Encode(subject)}{Encode(applicationId)}{Encode(authorizationId)}";
        return $"REVOCATION#{entity}#{KeyHelper.Sha256(value)}";
    }

    private static string Encode(string? value)
        => value is null ? "-1:" : $"{value.Length}:{value}";

    private static string HashNullable(string? value)
        => value is null ? KeyHelper.NullSentinel : KeyHelper.Sha256(value);
}
