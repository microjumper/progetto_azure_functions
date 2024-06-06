using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace appointment_scheduler.functions;

public class DocumentManager
{
    private const string containerName = "documents";

    [Function("Upload")]
    public static async Task<IActionResult> Upload(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "documents/upload")]
        HttpRequest req,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(Upload));

        var containerClient = BlobServiceClientSingleton.Instance.GetBlobContainerClient(containerName);

        try
        {
            var formCollection = await req.ReadFormAsync();

            if(formCollection.Files.Count > 0)
            {
                var accountId = formCollection["accountId"].ToString();
                var accountEmail = formCollection["accountEmail"].ToString();
                
                var fileMetadatas = new List<Dictionary<string, string>>(); // File details to return

                foreach (var file in formCollection.Files)
                {
                    if (file != null && file.Length > 0)
                    {
                        var fileMetadata = await WriteFile(containerClient, file, accountId, accountEmail, logger);

                        fileMetadatas.Add(fileMetadata);
                    }
                }
                
                return new OkObjectResult(fileMetadatas);
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

    private static async Task<Dictionary<string, string>> WriteFile(BlobContainerClient containerClient, IFormFile file, string accountId, string accountEmail, ILogger logger)
    {
        var fileId = Guid.NewGuid().ToString();

        var fileExtension = Path.GetExtension(file.FileName);
        var fileName = fileId + fileExtension;

        // Get a reference to a blob
        BlobClient blobClient = containerClient.GetBlobClient(fileName);

        // Upload the file stream to the blob
        using (var stream = file.OpenReadStream())
        {
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
        }

        var metadata = new Dictionary<string, string>
        {
            { "fileName", file.FileName },
            { "fileUrl", blobClient.Uri.ToString() },
            { "accountId", accountId },
            { "accountEmail", accountEmail }
        };

        // Set metadata
        await blobClient.SetMetadataAsync(metadata);

        logger.LogInformation($"File {file.FileName} uploaded successfully to container {containerName}");

        return metadata;
    }
}
