using Microsoft.Azure.Cosmos;

namespace appointment_scheduler.functions;

public static class CosmosClientManager
{
    private static readonly Lazy<CosmosClient> lazyClient = new (InitializeCosmosClient);
    
    public static CosmosClient Instance => lazyClient.Value;
    
    private static CosmosClient InitializeCosmosClient()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING_SETTING");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string is null or empty.");
        }
        
        var clientOptions = new CosmosClientOptions()
        {
            SerializerOptions = new CosmosSerializationOptions()
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };

        try
        {
            return new CosmosClient(connectionString, clientOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize Cosmos client.", ex);
        }
    }
}