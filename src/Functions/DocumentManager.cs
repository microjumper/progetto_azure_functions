using AppointmentScheduler.Types;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AppointmentScheduler.Functions;

public class DocumentManager(BlobServiceClient serviceClient, ILogger<DocumentManager> logger)
{
    private const string ContainerName = "documents";
    private readonly BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(ContainerName);

    [Function("Upload")]
    public async Task<IActionResult> Upload([HttpTrigger(AuthorizationLevel.Function, "post", Route = "documents/upload")] HttpRequest req)
    {
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
        catch (Exception e)
        {
            logger.LogError(e, "Error uploading file");
            
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<Dictionary<string, string>> WriteFile(BlobContainerClient containerClient, IFormFile file, string accountId, string accountEmail, ILogger logger)
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

        var sasToken = GenerateSasToken(blobClient, file.FileName);

        var metadata = new Dictionary<string, string>
        {
            { "originalFileName", file.FileName },
            { "fileUrl", blobClient.Uri.ToString() },
            { "accountId", accountId },
            { "accountEmail", accountEmail },
            { "sasToken", sasToken }
        };

        // Set metadata
        await blobClient.SetMetadataAsync(metadata);

        logger.LogInformation($"File {file.FileName} uploaded successfully to container {ContainerName}");

        return metadata;
    }

    public async Task RemoveFiles(IEnumerable<FileMetadata> fileMetadata)
    {
        foreach (var metadata in fileMetadata)
        {
            var blobUri = new Uri(metadata.FileUrl);
            var blobClient = containerClient.GetBlobClient(blobUri.Segments.Last());

            try
            {
                await blobClient.DeleteIfExistsAsync();

                logger.LogInformation($"Deleted file {metadata.OriginalFileName}");
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Error deleting file {metadata.OriginalFileName}");
            }
        }
    }

    private string GenerateSasToken(BlobClient blobClient, string fileName)
    {
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(14) // Set the expiration time for the SAS token
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        sasBuilder.ContentDisposition = $"inline; filename={fileName}";

        return blobClient.GenerateSasUri(sasBuilder).Query;
    }
}
