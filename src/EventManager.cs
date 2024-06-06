using appointment_scheduler.types;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace appointment_scheduler.functions;

public class EventManager
{
    private const string DatabaseId = "appointment_scheduler_db";
    private const string ContainerId = "event";

    private static readonly Container container;

    static EventManager() => container = CosmosClientSingleton.Instance.GetContainer(DatabaseId, ContainerId);

    [Function("GetEvents")]
    public static async Task<IActionResult> GetEvents([HttpTrigger(AuthorizationLevel.Function, "get", Route = "events/all")] HttpRequest req, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(GetEvents));

        try 
        {
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
        var newEvent = JsonConvert.DeserializeObject<EventApi>(requestBody);

        try {
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

    [Function("UpdateEvent")]
    public static async Task<IActionResult> UpdateEvent([HttpTrigger(AuthorizationLevel.Function, "put", Route = "events/update/{id}")] HttpRequest req, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(UpdateEvent));

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var updatedEvent = JsonConvert.DeserializeObject<EventApi>(requestBody);

        try {
            var response = await container.ReplaceItemAsync(updatedEvent, updatedEvent.Id, new PartitionKey(updatedEvent.Id));

            return new OkObjectResult(response.Resource);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);

            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteEvent")]
    public static async Task<IActionResult> DeleteEvent([HttpTrigger(AuthorizationLevel.Function, "delete", Route = "events/delete/{id}")] HttpRequest req, FunctionContext context, string id)
    {
        var logger = context.GetLogger(nameof(DeleteEvent));

        try
        {
            var response = await container.DeleteItemAsync<EventApi>(id, new PartitionKey(id));

            return new OkObjectResult(response.Resource);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);

            return new StatusCodeResult(500);
        }
    }    
}
