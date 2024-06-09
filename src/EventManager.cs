using System.Net;
using AppointmentScheduler.Types;
using AppointmentScheduler.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AppointmentScheduler.Functions;

public class EventManager(CosmosClient cosmosClient, ILogger<EventManager> logger)
{
    private const string DatabaseId = "appointment_scheduler_db";
    private const string ContainerId = "event";
    private readonly Container container = cosmosClient.GetContainer(DatabaseId, ContainerId);

    [Function("GetEventById")]
    public async Task<IActionResult?> GetEventById([HttpTrigger(AuthorizationLevel.Function, "get", Route = "events/{id}")] HttpRequest req, string id)
    {
        var eventItem = await GetEventByIdAsync(id);

        if (eventItem == null)
        {
            logger.LogInformation($"Event with id '{id}' not found.");
            
            return new NotFoundResult();
        }

        return new OkObjectResult(eventItem);
    }

    [Function("GetEvents")]
    public async Task<IActionResult> GetEvents([HttpTrigger(AuthorizationLevel.Function, "get", Route = "events")] HttpRequest req)
    {
        var query = new QueryDefinition("SELECT * FROM c");

        var response = await QueryExecutor.RetrieveItemsAsync<EventApi>(container, query, logger);
        return new OkObjectResult(response);
    }

    [Function("GetEventsByLegalService")]
    public async Task<IActionResult> GetEventsByLegalService(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "services/events/{id}")] HttpRequest req, string id)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.extendedProps.legalService = @legalServiceId")
            .WithParameter("@legalServiceId", id);

        var response = await QueryExecutor.RetrieveItemsAsync<EventApi>(container, query, logger);
        return new OkObjectResult(response);
    }

    [Function("AddEvent")]
    public async Task<IActionResult> AddEvent([HttpTrigger(AuthorizationLevel.Function, "post", Route = "events/add")] HttpRequest req)
    {
        using var reader = new StreamReader(req.Body);
        string requestBody = await reader.ReadToEndAsync();

        var newEvent = JsonConvert.DeserializeObject<EventApi>(requestBody);
        newEvent.Id = Guid.NewGuid().ToString();

        newEvent.BackgroundColor = "#4CAF50";
        newEvent.BorderColor = "#4CAF50";

        var response = await QueryExecutor.CreateItemAsync(container, newEvent, newEvent.Id, logger);
        return new OkObjectResult(response);
    }

    [Function("UpdateEvent")]
    public async Task<IActionResult> UpdateEvent([HttpTrigger(AuthorizationLevel.Function, "put", Route = "events/update/{id}")] HttpRequest req)
    {
        using var reader = new StreamReader(req.Body);
        string requestBody = await reader.ReadToEndAsync();

        var updatedEvent = JsonConvert.DeserializeObject<EventApi>(requestBody);

        var response = await QueryExecutor.UpdateItemAsync(container, updatedEvent, updatedEvent.Id, updatedEvent.Id, logger);
        return new OkObjectResult(response);
    }

    [Function("DeleteEvent")]
    public async Task<IActionResult> DeleteEvent([HttpTrigger(AuthorizationLevel.Function, "delete", Route = "events/delete/{id}")] HttpRequest req, string id)
    {
        var deletedEvent = await QueryExecutor.DeleteItemAsync<EventApi>(container, id, id, logger);
        return new OkObjectResult(deletedEvent);
    }

    public async Task<EventApi?> SetEventAsBooked(string id, Appointment appointment)
    {
        var eventApi = await GetEventByIdAsync(id);

        if(eventApi != null)
        {
            eventApi.ExtendedProps?.Add("appointment", appointment);
            eventApi.BackgroundColor = "#F44336";
            eventApi.BorderColor = "#F44336";

            return await container.ReplaceItemAsync(eventApi, eventApi?.Id, new PartitionKey(eventApi?.Id));
        }

        return null;
    }

    public async Task<EventApi?> SetEventAsBookable(string id)
    {
        var eventApi = await GetEventByIdAsync(id);

        if(eventApi != null)
        {
            eventApi.ExtendedProps?.Remove("appointment");
            eventApi.BackgroundColor = "#4CAF50";
            eventApi.BorderColor = "#4CAF50";

            return await container.ReplaceItemAsync(eventApi, eventApi?.Id, new PartitionKey(eventApi?.Id));
        }

        return null;
    }

    private async Task<EventApi?> GetEventByIdAsync(string id)
    {
        try
        {
            return (await container.ReadItemAsync<EventApi>(id, new PartitionKey(id))).Resource;
        }
        catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInformation($"Event with ID '{id}' not found.");

            return null;
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Error retrieving event with ID '{id}'");
            
            throw;
        }
    }
}
