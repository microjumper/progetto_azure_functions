using appointment_scheduler.types;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace appointment_scheduler.functions
{
    public class Getter(ILogger<Getter> logger)
    {
        private readonly ILogger<Getter> _logger = logger;

        [Function("Getter")]
        public async Task<IActionResult> GetAll([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
        {
            Container container = CosmosClientManager.Instance.GetContainer("appointment_scheduler_db", "legal_service");

            var query = new QueryDefinition("SELECT * FROM c");

            var response = await container.GetItemQueryIterator<LegalService>(query).ReadNextAsync();
            
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult(response.ToList());
        }
    }
}
