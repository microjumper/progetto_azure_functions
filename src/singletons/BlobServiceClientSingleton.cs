using Azure.Storage.Blobs;

namespace appointment_scheduler.functions;

public static class BlobServiceClientSingleton
{
    private static readonly Lazy<BlobServiceClient> _lazyClient = new(InitializeBlobServiceClient);

    public static BlobServiceClient Instance => _lazyClient.Value;

    private static BlobServiceClient InitializeBlobServiceClient()
    {
        var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("AzureWebJobsStorage connection string is null or empty.");
        }

        try
        {
            return new BlobServiceClient(connectionString);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize BlobServiceClient.", ex);
        }
    }
}
