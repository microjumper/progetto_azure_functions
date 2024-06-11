using AppointmentScheduler.Types;
using AppointmentScheduler.Utils;
using Azure.Communication.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AppointmentScheduler.Functions;

public class WaitingListManager(CosmosClient cosmosClient, EmailClient emailClient, ILogger<WaitingListManager> logger)
{
    private const string DatabaseId = "appointment_scheduler_db";
    private const string ContainerId = "waiting_list";
    private readonly Container container = cosmosClient.GetContainer(DatabaseId, ContainerId);

    [Function("SubscribeToWaitingList")]
    public async Task<IActionResult> SubscribeToWaitingList(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "waitinglist/subscribe")] HttpRequest req)
    {
        using var reader = new StreamReader(req.Body);
        string requestBody = await reader.ReadToEndAsync();

        var deserialized = JsonConvert.DeserializeObject<WaitingListEntity>(requestBody);

        var entity = new WaitingListEntity
        {
            Id = Guid.NewGuid().ToString(),
            LegalServiceId = deserialized.LegalServiceId,
            User = deserialized.User,
            JoinedAt = DateTime.UtcNow.ToString("o")
        };

        var response = await QueryExecutor.CreateItemAsync(container, entity, entity.Id, logger);
        return new OkObjectResult(response);
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
                    recipientAddress: firstEntity.User.Email,
                    subject: "Appuntamento disponibile",
                    htmlContent: $"<html><h2>{eventDate}</h2><p>Legal Service Title: {legalServiceTitle}</p><p>Event ID: {eventId}</p></html>",
                    plainTextContent: "An appointment slot has become available. Please visit our website to book your appointment."
                );

                if (sendOperation.HasCompleted)
                {
                    logger.LogInformation("Email sent successfully");
                }
                else
                {
                    logger.LogError("Failed to send email");
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

    private async Task<WaitingListEntity> GetFirstInWaitingList(string legalServiceId)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.legalServiceId = @legalServiceId ORDER BY c.CreatedAt ASC")
            .WithParameter("@legalServiceId", legalServiceId);
        
        var response = await QueryExecutor.RetrieveItemsAsync<WaitingListEntity>(container, query, logger);

        return response.First();
    }
}
