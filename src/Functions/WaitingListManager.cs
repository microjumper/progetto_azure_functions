using System.Globalization;
using System.Net;
using AppointmentScheduler.Types;
using AppointmentScheduler.Utils;
using Azure.Communication.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AppointmentScheduler.Functions;

public class WaitingListManager(CosmosClient cosmosClient, EmailClient emailClient, ILogger<WaitingListManager> logger, DocumentManager documentManager, EventManager eventManager)
{
    private CancellationTokenSource? cancellationTokenSource;
    private const int WaitingListSize = 5;
    private const string DatabaseId = "appointment_scheduler_db";
    private const string ContainerId = "waiting_list";
    private readonly Container container = cosmosClient.GetContainer(DatabaseId, ContainerId);

    [Function("GetWaitingLists")]
    public async Task<IActionResult> GetWaitingLists([HttpTrigger(AuthorizationLevel.Function, "get", Route = "waitinglist/all")] HttpRequest req)
    {
        var query = new QueryDefinition("SELECT * FROM c");

        var response = await QueryExecutor.RetrieveItemsAsync<WaitingListEntity>(container, query, logger);
        return new OkObjectResult(response);
    }

    [Function("AddToWaitingList")]
    public async Task<IActionResult> AddToWaitingList(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "waitinglist/add")] HttpRequest req)
    {
        var deserialized = await Deserializer<Appointment>.Deserialize(req.Body);

        var totalRecordsForLegalService = await GetWaitingListCountAsync(deserialized.LegalServiceId);
        if(totalRecordsForLegalService > WaitingListSize)
        {
            return new ConflictObjectResult("The waiting list is full.");
        }

        var entity = new WaitingListEntity
        {
            Id = Guid.NewGuid().ToString(),
            Appointment = deserialized,
            AddedOn = DateTime.UtcNow.ToString("o")
        };

        var response = await QueryExecutor.CreateItemAsync(container, entity, entity.Id, logger);
        return new OkObjectResult(response);
    }

    [Function("RemoveFromWaitingList")]
    public async Task<IActionResult> RemoveFromWaitingList(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "waitinglist/remove/{id}")] HttpRequest req,
        string id)
    {
        var response = await RemoveFromWaitingList(id);

        return new OkObjectResult(response);
    }

    [Function("GetUserWaitingList")]
    public async Task<IActionResult> GetUserWaitingList(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/{id}/waitinglist")] HttpRequest req, string id)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.appointment.user.id = @userId")
            .WithParameter("@userId", id);

        var response = await QueryExecutor.RetrieveItemsAsync<WaitingListEntity>(container, query, logger);
        return new OkObjectResult(response);
    }

    public async Task NotifyWaitingList(string legalServiceId, string legalServiceTitle, string eventId, string eventDate)
    {
        var entity = await GetFirstInWaitingList(legalServiceId);

        if(entity == null) 
        {
            logger.LogInformation("No users found in waiting list.");

            await eventManager.SetEventAsBookable(eventId);
        }
        else
        {
            entity.Appointment.EventDate = eventDate;
            entity.Appointment.EventId = eventId;

            await QueryExecutor.UpdateItemAsync(container, entity, entity.Id, entity.Id, logger);
            
            await SendConfirmationEmail(entity.Appointment.User.Email, eventDate, legalServiceTitle);

            cancellationTokenSource = new();

            try
            {
                await Task.Delay(180000, cancellationTokenSource.Token); // 3 minutes
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Task was cancelled. User confirmed the appointment.");

                await RemoveFromWaitingList(entity.Id);

                logger.LogInformation("Entity remove from waiting list");

                return;
            }

            try // if the user doesn't confirm within the delay time
            {
                //  check if the reservation is still inside the wating list
                var response = await QueryExecutor.RetrieveItemAsync<WaitingListEntity>(container, entity.Id, entity.Id, logger);

                await RemoveFromWaitingList(entity.Id); // is so, remove it

                _ = SendCancellationEmail(entity.Appointment.User.Email);   // infor user of the cancellation
                
                _ = NotifyWaitingList(legalServiceId, legalServiceTitle, eventId, eventDate);   // start over
            }
            catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("User confirmed the appointment.");

                await RemoveFromWaitingList(entity.Id);
            }
        }
    }

    public void StopNotificationTask() => cancellationTokenSource?.Cancel();

    private async Task SendConfirmationEmail(string recipientAddress, string eventDate, string legalServiceTitle)
    {
        await SendEmail(
            recipientAddress,
            "Appuntamento disponibile",
            htmlContent: $@"
            <p>
                Un appuntamento per il servizio <strong>{legalServiceTitle}</strong> è ora disponibile in data <strong>{FormatDate(eventDate)}</strong>.<br>
                Puoi confermare l'appuntamento dal tuo profilo entro il prossimo minuto.
            </p>",
            plainTextContent: ""
        );
    }

    private async Task SendCancellationEmail(string recipientAddress)
    {
        await SendEmail(
            recipientAddress,
            "Rimosso dalla lista di attesa",
            htmlContent: $@"
            <p>
                Con la presente desideriamo comunicarti che non sei più incluso nella lista d'attesa. Ti ringraziamo sinceramente per l'interesse dimostrato nei nostri servizi..
            </p>",
            plainTextContent: ""
        );
    }

    private async Task SendEmail(string recipientAddress, string subject, string htmlContent, string plainTextContent)
    {
        try 
        {
            EmailSendOperation sendOperation = await emailClient.SendAsync(
                Azure.WaitUntil.Completed,
                senderAddress: "DoNotReply@f965f1af-6fb4-43d0-9e24-4b783ef8cfbd.azurecomm.net",
                recipientAddress,
                subject,
                htmlContent,
                plainTextContent
            );

            if (sendOperation.HasCompleted)
            {
                logger.LogInformation("Email sent successfully");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while sending the email.");
        }
    }

    private async Task<int> GetWaitingListCountAsync(string legalServiceId)
    {
    var countQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.appointment.legalServiceId = @legalServiceId")
        .WithParameter("@legalServiceId", legalServiceId);
    var countResponse = await container.GetItemQueryIterator<int>(countQuery).ReadNextAsync();
    return countResponse.FirstOrDefault();
    }

    private async Task<WaitingListEntity?> GetFirstInWaitingList(string legalServiceId)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.appointment.legalServiceId = @legalServiceId ORDER BY c.addedOn ASC")
            .WithParameter("@legalServiceId", legalServiceId);
        var response = await QueryExecutor.RetrieveItemsAsync<WaitingListEntity>(container, query, logger);
        return response.FirstOrDefault();
    }

    private async Task<WaitingListEntity> RemoveFromWaitingList(string userId)
    {
        var enity = await QueryExecutor.RetrieveItemAsync<WaitingListEntity>(container, userId, userId, logger);
        
        if(enity.Appointment.FileMetadata.Count > 0)  // remove attached files 
        {
            await documentManager.RemoveFiles(enity.Appointment.FileMetadata);
        }

        return await QueryExecutor.DeleteItemAsync<WaitingListEntity>(container, userId, userId, logger);
    }

    private string FormatDate(string dateString)
    {        
        // Parse the string into a DateTimeOffset object
        DateTimeOffset dateTimeOffset = DateTimeOffset.Parse(dateString);
        
        // Define Italian culture for formatting
        CultureInfo italianCulture = CultureInfo.GetCultureInfo("it-IT");
        
        // Create a custom format string
        string format = "dddd d MMMM H:mm";
        
        // Format the DateTimeOffset object using the custom format and Italian culture
        return dateTimeOffset.ToString(format, italianCulture);
    }
}
