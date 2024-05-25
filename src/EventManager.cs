using appointment_scheduler.types;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace appointment_scheduler.functions;

public class EventManager
{
    [Function("GetEvents")]
    public static async Task<IActionResult> GetEvents([HttpTrigger(AuthorizationLevel.Function, "get", Route = "events/all")] HttpRequest req, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(GetEvents));

        try 
        {
            Container container = CosmosClientManager.Instance.GetContainer("appointment_scheduler_db", "event");

            var query = new QueryDefinition("SELECT * FROM c");
            var response = await container.GetItemQueryIterator<EventApi>(query).ReadNextAsync();
            
            return new OkObjectResult(response.ToList());
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unexpected error occurred.");
            
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [Function("AddEvent")]
    public static async Task<IActionResult> AddEvent([HttpTrigger(AuthorizationLevel.Function, "post", Route = "events/add")] HttpRequest req, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(AddEvent));

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        var newEvent = JsonConvert.DeserializeObject<EventApi>(requestBody, jsonSettings);

        try {
            var container = CosmosClientManager.Instance.GetContainer("appointment_scheduler_db", "event");

            newEvent.Id = Guid.NewGuid().ToString();
            var response = await container.CreateItemAsync(newEvent, new PartitionKey(newEvent.Id));

            return new OkObjectResult(response.Resource);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);

            return new StatusCodeResult(500);
        }
    }
}
