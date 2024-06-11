using AppointmentScheduler.Types;
using AppointmentScheduler.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AppointmentScheduler.Functions;

public class WaitingListManager(CosmosClient cosmosClient, ILogger<WaitingListManager> logger)
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

    public async Task SendEmailToFirstInWaitingList(string legalServiceId)
    {
        await GetFirstInWaitingList(legalServiceId);
    }

    private async Task<WaitingListEntity> GetFirstInWaitingList(string legalServiceId)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.legalServiceId = @legalServiceId ORDER BY c.CreatedAt ASC")
            .WithParameter("@legalServiceId", legalServiceId);
        
        var response = await QueryExecutor.RetrieveItemsAsync<WaitingListEntity>(container, query, logger);

        return response.First();
    }
}
