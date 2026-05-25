namespace OpenIddict.DynamoDb.Tests.Helpers;

public static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var results = new List<T>();

        await foreach (var item in source)
        {
            results.Add(item);
        }

        return results;
    }
}
