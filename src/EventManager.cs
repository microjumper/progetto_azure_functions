using appointment_scheduler.types;
using appointment_scheduler.utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace appointment_scheduler.functions;

public class EventManager(CosmosClient cosmosClient, ILogger<EventManager> logger)
{
    private const string DatabaseId = "appointment_scheduler_db";
    private const string ContainerId = "event";
    private readonly Container container = cosmosClient.GetContainer(DatabaseId, ContainerId);

    [Function("GetEvents")]
    public async Task<IActionResult> GetEvents([HttpTrigger(AuthorizationLevel.Function, "get", Route = "events/all")] HttpRequest req)
    {
        var container = cosmosClient.GetContainer(DatabaseId, ContainerId);
        var query = new QueryDefinition("SELECT * FROM c");

        var response = await QueryExecutor.ExecuteRetrivingQueryAsync<EventApi>(container, query, logger);
        return new OkObjectResult(response);
    }

    [Function("GetEventsByLegalService")]
    public async Task<IActionResult> GetEventsByLegalService(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "events/services/{id}")] HttpRequest req,
        string id)
    {
        var container = cosmosClient.GetContainer(DatabaseId, ContainerId);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.extendedProps.legalService = @legalServiceId")
            .WithParameter("@legalServiceId", id);

        var response = await QueryExecutor.ExecuteRetrivingQueryAsync<EventApi>(container, query, logger);
        return new OkObjectResult(response);
    }

    [Function("AddEvent")]
    public async Task<IActionResult> AddEvent([HttpTrigger(AuthorizationLevel.Function, "post", Route = "events/add")] HttpRequest req, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(AddEvent));

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var newEvent = JsonConvert.DeserializeObject<EventApi>(requestBody);

        try {
            newEvent.Id = Guid.NewGuid().ToString();

            var response = await QueryExecutor.CreateItemAsync(container, newEvent, newEvent.Id, logger);
            return new OkObjectResult(response);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);

            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateEvent")]
    public async Task<IActionResult> UpdateEvent([HttpTrigger(AuthorizationLevel.Function, "put", Route = "events/update/{id}")] HttpRequest req, FunctionContext context)
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
    public async Task<IActionResult> DeleteEvent([HttpTrigger(AuthorizationLevel.Function, "delete", Route = "events/delete/{id}")] HttpRequest req, FunctionContext context, string id)
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
