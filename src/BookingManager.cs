using AppointmentScheduler.Types;
using AppointmentScheduler.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AppointmentScheduler.Functions;

public class BookingManager(CosmosClient cosmosClient, DocumentManager documentManager, EventManager eventManager, ILogger<BookingManager> logger)
{
    private const string DatabaseId = "appointment_scheduler_db";
    private const string ContainerId = "appointment";
    private readonly Container container = cosmosClient.GetContainer(DatabaseId, ContainerId);

    [Function("GetCurrentDate")]
    public static IActionResult GetCurrentDate([HttpTrigger(AuthorizationLevel.Function, "get", Route = "date")] HttpRequest req)
    {
        return new OkObjectResult(new { dateISO = DateTime.UtcNow.ToString("o") });
    }

    [Function("GetAppointments")]
    public async Task<IActionResult> GetAppointments([HttpTrigger(AuthorizationLevel.Function, "get", Route = "appointments")] HttpRequest req)
    {
        var query = new QueryDefinition("SELECT * FROM c");
        var response = await QueryExecutor.RetrieveItemsAsync<Appointment>(container, query, logger);
        
        return new OkObjectResult(response);
    }

    [Function("GetAppointmentById")]
    public async Task<IActionResult> GetApGetAppointmentByIdointments(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "appointments/{id}")] HttpRequest req,
        string id)
    {
        try
        {
            var response = await container.ReadItemAsync<Appointment>(id, new PartitionKey(id));
            return new OkObjectResult(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new NotFoundResult();
        }
        catch (CosmosException e)
        {
            logger.LogError($"Error retrieving item: {e}");

            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [Function("Book")]
    public async Task<IActionResult> Book([HttpTrigger(AuthorizationLevel.Function, "post", Route = "appointments/book")] HttpRequest req)
    {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        
        var newAppointment = JsonConvert.DeserializeObject<Appointment>(requestBody);

        try {
            var container = cosmosClient.GetContainer(DatabaseId, ContainerId);
            newAppointment.Id = Guid.NewGuid().ToString();

            var response = await QueryExecutor.CreateItemAsync(container, newAppointment, newAppointment.Id, logger);

            await eventManager.SetEventAsBooked(newAppointment.EventId, newAppointment.Id);

            return new OkObjectResult(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);

            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [Function("Cancel")]
    public async Task<IActionResult> Cancel(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "appointments/cancel/{appointmentId}")] HttpRequest req,
        string appointmentId)
    {
        try
        {
            var container = cosmosClient.GetContainer(DatabaseId, ContainerId);

            var appointment = (await container.ReadItemAsync<Appointment>(appointmentId, new PartitionKey(appointmentId))).Resource;
           
            if(appointment.FileMetadata.Count > 0) 
            {
                await documentManager.RemoveFiles(appointment.FileMetadata);
            }

            var response = await container.DeleteItemAsync<Appointment>(appointmentId, new PartitionKey(appointmentId));

            await eventManager.SetEventAsBookable(appointment.EventId);

            return new OkObjectResult(response.Resource);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);

            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}
