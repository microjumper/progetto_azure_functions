using appointment_scheduler.types;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace appointment_scheduler.functions;

public class LegalServiceManager
{
    [Function("GetAll")]
    public static async Task<IActionResult> GetAll([HttpTrigger(AuthorizationLevel.Function, "get", Route = "legalServices/all")] HttpRequest req, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(GetAll));

        try 
        {
            Container container = CosmosClientManager.Instance.GetContainer("appointment_scheduler_db", "legal_service");

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
}
