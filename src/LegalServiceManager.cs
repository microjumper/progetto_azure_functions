using appointment_scheduler.types;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace appointment_scheduler.functions;

public class LegalServiceManager
{
    [Function("GetAll")]
    public static async Task<IActionResult> GetAll([HttpTrigger(AuthorizationLevel.Function, "get", Route = "legalServices/all")] HttpRequest req, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(GetAll));

        try 
        {
            Container container = CosmosClientSingleton.Instance.GetContainer("appointment_scheduler_db", "legal_service");

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
    public static async Task<IActionResult> AddEvent([HttpTrigger(AuthorizationLevel.Function, "post", Route = "legalServices/add")] HttpRequest req, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(AddEvent));

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

        var newService = JsonConvert.DeserializeObject<LegalService>(requestBody);

        try {
            var container = CosmosClientSingleton.Instance.GetContainer("appointment_scheduler_db", "legal_service");
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
    public static async Task<IActionResult> RemoveLegalService([HttpTrigger(AuthorizationLevel.Function, "delete", Route = "legalServices/delete/{id}")] HttpRequest req, FunctionContext context, string id)
    {
        var logger = context.GetLogger(nameof(RemoveLegalService));

        try
        {
            var container = CosmosClientSingleton.Instance.GetContainer("appointment_scheduler_db", "legal_service");
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
