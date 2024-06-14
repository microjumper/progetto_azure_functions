using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace AppointmentScheduler.Utils;

public static class QueryExecutor
{
    public static async Task<T> RetrieveItemAsync<T>(Container container, string id, string partitionKey, ILogger logger)
    {
        try
        {
            return (await container.ReadItemAsync<T>(id, new PartitionKey(partitionKey))).Resource;
        }
        catch (CosmosException cosmosException)
        {
            logger.LogError(cosmosException, "Cosmos DB error occurred while retrieving item: {Message}", cosmosException.Message);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while retrieving item: {Message}", e.Message);
            throw;
        }
    }
    public static async Task<List<T>> RetrieveItemsAsync<T>(Container container, QueryDefinition query, ILogger logger)
    {
        var results = new List<T>();

        try
        {
            using var iterator = container.GetItemQueryIterator<T>(query);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }
        }
        catch (CosmosException cosmosException)
        {
            logger.LogError(cosmosException, "Cosmos DB error occurred while retrieving items: {Message}", cosmosException.Message);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while retrieving items: {Message}", e.Message);
            throw;
        }

        return results;
    }

    public static async Task<T> CreateItemAsync<T>(Container container, T item, string partitionKey, ILogger logger)
    {
        try
        {
            var response = await container.CreateItemAsync(item, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException cosmosException)
        {
            logger.LogError(cosmosException, "Cosmos DB error occurred while creating item: {Message}", cosmosException.Message);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while creating item: {Message}", e.Message);
            throw;
        }
    }

    public static async Task<T> UpdateItemAsync<T>(Container container, T updatedItem, string itemId, string partitionKey, ILogger logger)
    {
        try
        {
            var response = await container.ReplaceItemAsync(updatedItem, itemId, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException cosmosException)
        {
            logger.LogError(cosmosException, "Cosmos DB error occurred while updating item: {Message}", cosmosException.Message);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while updating item: {Message}", e.Message);
            throw;
        }
    }

    public static async Task<T> DeleteItemAsync<T>(Container container, string itemId, string partitionKey, ILogger logger)
    {
        try
        {
            var response = await container.DeleteItemAsync<T>(itemId, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException cosmosException)
        {
            logger.LogError(cosmosException, "Cosmos DB error occurred while deleting item: {Message}", cosmosException.Message);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while deleting item: {Message}", e.Message);
            throw;
        }
    }
}