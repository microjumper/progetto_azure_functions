using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;

using appointment_scheduler.functions;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        services.AddLogging(logging =>
        {
            logging.AddConsole();
        });

        services.AddSingleton(s =>
        {
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING_SETTING");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string is null or empty.");
            }

            var cosmosClientOptions = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            };

            try
            {
                return new CosmosClient(connectionString, cosmosClientOptions);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Error creating CosmosClient: {e.Message}", e);
            }
        });

        services.AddSingleton(s =>
        {
            var connectionString = Environment.GetEnvironmentVariable("AZURE_WEB_JOBS_STORAGE");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Blob storage connection string is null or empty.");
            }

            try
            {
                return new BlobServiceClient(connectionString);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Error creating BlobServiceClient: {e.Message}", e);
            }
        });

        services.AddSingleton<EventManager>();
        services.AddSingleton<DocumentManager>();
        services.AddSingleton<BookingManager>();
    })
    .Build();

host.Run();
