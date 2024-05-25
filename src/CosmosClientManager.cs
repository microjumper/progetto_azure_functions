using System;
using Microsoft.Azure.Cosmos;

namespace appointment_scheduler.functions;

public static class CosmosClientManager
{
    private static readonly Lazy<CosmosClient> lazyClient = new (InitializeCosmosClient);
    
    public static CosmosClient Instance => lazyClient.Value;
    
    private static CosmosClient InitializeCosmosClient()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING_SETTING");
        
        var clientOptions = new CosmosClientOptions()
        {
            SerializerOptions = new CosmosSerializationOptions()
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };

        return new CosmosClient(connectionString, clientOptions);
    }
}