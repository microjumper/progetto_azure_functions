using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using appointment_scheduler.types;

namespace appointment_scheduler.functions;

public class BookingManager(CosmosClient cosmosClient)
{
    private const string DatabaseId = "appointment_scheduler_db";
    private const string ContainerId = "appointment";
    private readonly Container container = cosmosClient.GetContainer(DatabaseId, ContainerId);

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

    [Function("Book")]
    public async Task<IActionResult> Book(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "appointments/book")]
        HttpRequest req,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(Book));

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        
        var newAppointment = JsonConvert.DeserializeObject<Appointment>(requestBody);

        try {
            newAppointment.Id = Guid.NewGuid().ToString();
            var response = await container.CreateItemAsync(newAppointment, new PartitionKey(newAppointment.Id));

            return new OkObjectResult(response.Resource);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);

            return new StatusCodeResult(500);
        }
    }

    [Function("Cancel")]
    public static async Task<IActionResult> Cancel(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "appointments/cancel/{appointmentId}")] HttpRequest req,
        FunctionContext context,
        string appointmentId)
    {
        var logger = context.GetLogger(nameof(Cancel));

        try
        {
            return new OkResult();
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while removing the appointment.");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}
