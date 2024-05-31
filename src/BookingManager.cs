using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using appointment_scheduler.types;

namespace appointment_scheduler.functions;

public class BookingManager
{
    [Function("GetEventsByLegalService")]
    public async Task<IActionResult> GetEventsByLegalService(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "events/legal-service/{legalServiceId}")]
        HttpRequest req,
        FunctionContext context,
        string legalServiceId)
    {
        var logger = context.GetLogger(nameof(GetEventsByLegalService));

        try 
        {
            var container = CosmosClientManager.Instance.GetContainer("appointment_scheduler_db","event");
            var query = new QueryDefinition("SELECT * FROM c WHERE c.extendedProps.legalService = @legalServiceId")
                .WithParameter("@legalServiceId", legalServiceId);

            var iterator = container.GetItemQueryIterator<EventApi>(query);
            var results = new List<EventApi>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return new OkObjectResult(results);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unexpected error occurred.");
            
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}
