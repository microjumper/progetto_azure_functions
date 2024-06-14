using AppointmentScheduler.Types;
using AppointmentScheduler.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AppointmentScheduler.Functions;

public class LegalServiceManager(CosmosClient cosmosClient, ILogger<LegalServiceManager> logger)
{
    private const string DatabaseId = "appointment_scheduler_db";
    private const string ContainerId = "legal_service";
    private readonly Container container = cosmosClient.GetContainer(DatabaseId, ContainerId);

    [Function("GetLegalServices")]
    public async Task<IActionResult> GetLegalServices([HttpTrigger(AuthorizationLevel.Function, "get", Route = "legalServices")] HttpRequest req)
    {
        var query = new QueryDefinition("SELECT * FROM c");
        var response = await QueryExecutor.RetrieveItemsAsync<LegalService>(container, query, logger);
        return new OkObjectResult(response);
    }

    [Function("AddLegalService")]
    public async Task<IActionResult> AddEvent([HttpTrigger(AuthorizationLevel.Function, "post", Route = "legalServices/add")] HttpRequest req)
    {
        using var reader = new StreamReader(req.Body);
        string requestBody = await reader.ReadToEndAsync();

        var newService = JsonConvert.DeserializeObject<LegalService>(requestBody);
        newService.Id = Guid.NewGuid().ToString();

        var response = await QueryExecutor.CreateItemAsync(container, newService, newService.Id, logger);
        return new OkObjectResult(response);
    }

    [Function("RemoveLegalService")]
    public async Task<IActionResult> RemoveLegalService(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "legalServices/delete/{id}")] HttpRequest req,
        string id)
    {
        var deletedService = await QueryExecutor.DeleteItemAsync<LegalService>(container, id, id, logger);
        return new OkObjectResult(deletedService);
    }
}
