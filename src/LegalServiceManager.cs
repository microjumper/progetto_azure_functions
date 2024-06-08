using appointment_scheduler.types;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace appointment_scheduler.functions;

public class LegalServiceManager(CosmosClient cosmosClient, ILogger<LegalServiceManager> logger)
{
    private const string DatabaseId = "appointment_scheduler_db";
    private const string ContainerId = "legal_service";
    private readonly Container container = cosmosClient.GetContainer(DatabaseId, ContainerId);

    [Function("GetAll")]
    public async Task<IActionResult> GetAll([HttpTrigger(AuthorizationLevel.Function, "get", Route = "legalServices/all")] HttpRequest req)
    {
        try 
        {
            var query = new QueryDefinition("SELECT * FROM c");
            var response = await container.GetItemQueryIterator<LegalService>(query).ReadNextAsync();

            return new OkObjectResult(response.ToList());
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unexpected error occurred.");
            
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [Function("AddLegalService")]
    public async Task<IActionResult> AddEvent([HttpTrigger(AuthorizationLevel.Function, "post", Route = "legalServices/add")] HttpRequest req)
    {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

        var newService = JsonConvert.DeserializeObject<LegalService>(requestBody);

        try {
            newService.Id = Guid.NewGuid().ToString();
            var response = await container.CreateItemAsync(newService, new PartitionKey(newService.Id));

            return new OkObjectResult(response.Resource);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);

            return new StatusCodeResult(500);
        }
    }

    [Function("RemoveLegalService")]
    public async Task<IActionResult> RemoveLegalService(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "legalServices/delete/{id}")] HttpRequest req,
        string id)
    {
        try
        {
            var response = await container.DeleteItemAsync<LegalService>(id, new PartitionKey(id));

            return new OkObjectResult(response.Resource);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unexpected error occurred.");
            
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}
