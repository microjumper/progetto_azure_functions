using System.Globalization;
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
    private const int WaitingListSize = 5;
    private const string DatabaseId = "appointment_scheduler_db";
    private const string ContainerId = "waiting_list";
    private readonly Container container = cosmosClient.GetContainer(DatabaseId, ContainerId);

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
        var enity = await QueryExecutor.RetrieveItemAsync<WaitingListEntity>(container, id, id, logger);
        
        if(enity.Appointment.FileMetadata.Count > 0)  // remove attached files 
        {
            await documentManager.RemoveFiles(enity.Appointment.FileMetadata);
        }

        var response = await QueryExecutor.DeleteItemAsync<WaitCallback>(container, id, id, logger);

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
            await eventManager.SetEventAsBookable(eventId);
        }
        else
        {
            await SendConfirmationEmail(entity.Appointment.User.Email, eventDate, legalServiceTitle);
            // wait for confirmation
            // if confirm, add appointment to db, remove entity
            // else, remove entity, call this function
        }
    }

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

    public async Task SendEmailToFirstInWaitingList(string legalServiceId, string legalServiceTitle, string eventId, string eventDate)
    {
        WaitingListEntity firstEntity = await GetFirstInWaitingList(legalServiceId);

        if (firstEntity != null)
        {
            try 
            {
                EmailSendOperation sendOperation = await emailClient.SendAsync(
                    Azure.WaitUntil.Completed,
                    senderAddress: "DoNotReply@f965f1af-6fb4-43d0-9e24-4b783ef8cfbd.azurecomm.net",
                    recipientAddress: firstEntity.Appointment.User.Email,
                    subject: "Appuntamento disponibile",
                    htmlContent: $"<html><h2>{eventDate}</h2><p>Legal Service Title: {legalServiceTitle}</p><p>Event ID: {eventId}</p></html>",
                    plainTextContent: "An appointment slot has become available. Please visit our website to book your appointment."
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
        else
        {
            logger.LogWarning("No valid email address found  in the waiting list.");
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

    private string FormatDate(string dateString)
    {        
        // Parse the string into a DateTimeOffset object
        DateTimeOffset dateTimeOffset = DateTimeOffset.Parse(dateString);
        
        // Define Italian culture for formatting
        CultureInfo italianCulture = CultureInfo.GetCultureInfo("it-IT");
        
        // Create a custom format string
        string format = "dddd d MMMM H:mm";
        
        // Format the DateTimeOffset object using the custom format and Italian culture
        string formattedDateTime = dateTimeOffset.ToString(format, italianCulture);
        
        return formattedDateTime;
    }
}
