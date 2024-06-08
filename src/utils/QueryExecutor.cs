using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace appointment_scheduler.utils;

public static class QueryExecutor
{
    public static async Task<List<T>> ExecuteRetrivingQueryAsync<T>(Container container, QueryDefinition query, ILogger logger)
    {
        var iterator = container.GetItemQueryIterator<T>(query);
        var results = new List<T>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public static async Task<T> CreateItemAsync<T>(Container container, T item, string partitionKey, ILogger logger)
    {
        var response = await container.CreateItemAsync(item, new PartitionKey(partitionKey));

        return response.Resource;
    }
}