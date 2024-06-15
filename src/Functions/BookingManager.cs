using AppointmentScheduler.Types;
using AppointmentScheduler.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AppointmentScheduler.Functions;

public class BookingManager(CosmosClient cosmosClient, DocumentManager documentManager, EventManager eventManager, WaitingListManager waitingListManager, ILogger<BookingManager> logger)
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

    [Function("GetAppointmentByUser")]
    public async Task<IActionResult> GetAppointmentByUser([HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/{id}/appointments")] HttpRequest req,
    string id)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.user.id = @id").WithParameter("@id", id);
        var response = await QueryExecutor.RetrieveItemsAsync<Appointment>(container, query, logger);
        
        return new OkObjectResult(response);
    }

    [Function("GetAppointmentById")]
    public async Task<IActionResult> GetApGetAppointmentByIdointments(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "appointments/{id}")] HttpRequest req,
        string id)
    {
        var response = await QueryExecutor.RetrieveItemAsync<Appointment>(container, id, id, logger);
        return new OkObjectResult(response);
    }

    [Function("Book")]
    public async Task<IActionResult> Book([HttpTrigger(AuthorizationLevel.Function, "post", Route = "appointments/book")] HttpRequest req)
    {       
        var appointment = await Deserializer<Appointment>.Deserialize(req.Body);
        appointment.Id = Guid.NewGuid().ToString();

        var response = await QueryExecutor.CreateItemAsync(container, appointment, appointment.Id, logger);

        await eventManager.SetEventAsBooked(appointment.EventId, appointment.Id);

        return new OkObjectResult(response);
    }

    [Function("Cancel")]
    public async Task<IActionResult> Cancel(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "appointments/cancel/{appointmentId}")] HttpRequest req,
        string appointmentId)
    {
        var appointment = await QueryExecutor.RetrieveItemAsync<Appointment>(container, appointmentId, appointmentId, logger);
        
        if(appointment.FileMetadata.Count > 0)  // remove attached files 
        {
            await documentManager.RemoveFiles(appointment.FileMetadata);
        }

        var response = await QueryExecutor.DeleteItemAsync<Appointment>(container, appointmentId, appointmentId, logger);

        // fire and forget
        _ = waitingListManager.NotifyWaitingList(appointment.LegalServiceId, appointment.LegalServiceTitle, appointment.EventId, appointment.EventDate);

        return new OkObjectResult(response);
    }
}
