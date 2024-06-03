using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace appointment_scheduler.functions;

public class DocumentManager
{
    [Function("Upload")]
    public static async Task<IActionResult> Upload(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "documents/upload")]
        HttpRequest req,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(Upload));

        var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        var containerName = "documents";

        // Initialize Blob service and container clients
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        try
        {
            var formCollection = await req.ReadFormAsync();

            foreach (var file in formCollection.Files)
            {
                if (file != null && file.Length > 0)
                {
                    // Get a reference to a blob
                    BlobClient blobClient = containerClient.GetBlobClient(file.FileName);

                    // Upload the file stream to the blob
                    using (var stream = file.OpenReadStream())
                    {
                        await blobClient.UploadAsync(stream, overwrite: true);
                    }

                    logger.LogInformation($"File {file.FileName} uploaded successfully to container {containerName}");

                    string fileUrl = blobClient.Uri.ToString();

                    return new OkObjectResult(new { fileUrl });
                }
            }
            logger.LogWarning("No file was uploaded in the request.");
            return new BadRequestObjectResult("No file was uploaded in the request.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading file");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}
