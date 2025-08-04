using Microsoft.Azure.Cosmos;

namespace HighPerfLogger.App;

public class CosmosDbLogger : IAppLogger
{
    private readonly Container _container;

    public CosmosDbLogger(CosmosClient cosmosClient, string databaseName, string containerName)
    {
        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    public void Log(LogEntry entry)
    {
        _ = _container.CreateItemAsync(entry, new PartitionKey(entry.tenantId))
            .ContinueWith(t => 
            { 
                if (t.IsFaulted) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[ERROR] Fallo al guardar log en Cosmos DB: {t.Exception?.Flatten().InnerException?.Message}");
                    Console.ResetColor();
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
