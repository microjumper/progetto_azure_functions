using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using appointment_scheduler.types;
using appointment_scheduler.utils;

namespace appointment_scheduler.functions;

public class BookingManager(CosmosClient cosmosClient, DocumentManager documentManager, ILogger<BookingManager> logger)
{
    private const string DatabaseId = "appointment_scheduler_db";
    private const string ContainerId = "appointment";

    [Function("GetAppointments")]
    public async Task<IActionResult> GetAppointments([HttpTrigger(AuthorizationLevel.Function, "get", Route = "appointments/all")] HttpRequest req)
    {
        try
        {
            var container = cosmosClient.GetContainer(DatabaseId, ContainerId);
            var query = new QueryDefinition("SELECT * FROM c");

            var response = await QueryExecutor.ExecuteRetrivingQueryAsync<Appointment>(container, query, logger);
            return new OkObjectResult(response);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);

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
            return new OkObjectResult(response);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);

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

            return new OkObjectResult(response.Resource);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while removing the appointment.");

            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}
