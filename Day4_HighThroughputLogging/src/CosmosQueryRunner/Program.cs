using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

// ===================================================================
// PARTE 1: EL ARRANQUE DE LA APLICACIÓN
// ===================================================================

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var cosmosConfig = context.Configuration.GetSection("CosmosDb");
        services.AddSingleton(sp => new CosmosClient(cosmosConfig["Endpoint"], cosmosConfig["Key"]));
        services.AddTransient<QueryRunner>();
    })
    .Build();

var queryRunner = host.Services.GetRequiredService<QueryRunner>();
await queryRunner.RunAsync();


// ===================================================================
// PARTE 2: LA LÓGICA DE LA APLICACIÓN
// ===================================================================

public class QueryRunner
{
    private readonly Container _container;

    public QueryRunner(CosmosClient cosmosClient, IConfiguration config)
    {
        var dbName = config.GetSection("CosmosDb")["DatabaseName"] ?? "LoggingDb";
        var containerName = config.GetSection("CosmosDb")["ContainerName"] ?? "SystemLogs";
        _container = cosmosClient.GetContainer(dbName, containerName);
    }

    // ===================================================================
    // ESTE ES EL MÉTODO QUE SE BORRÓ Y QUE AHORA RESTAURAMOS
    // ===================================================================
    public async Task RunAsync()
    {
        Console.WriteLine("=============================================");
        Console.WriteLine("   Azure Cosmos DB - Demostrador de Costo (RU)");
        Console.WriteLine("=============================================");

        while (true)
        {
            Console.WriteLine("\nElige una consulta para ejecutar:");
            Console.WriteLine("  1. Consulta EFICIENTE (por clave de partición única)");
            Console.WriteLine("  2. Consulta MÁS COSTOSA (varias claves de partición)");
            Console.WriteLine("  3. Consulta MUY COSTOSA ('fan-out', sin clave de partición)");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  4. Consulta INEFICIENTE (por campo NO indexado en 'payload')");
            Console.ResetColor();
            Console.WriteLine("  5. Salir");

            Console.Write("Opción: ");
            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await RunQueryAsync(
                        "Consulta Eficiente (Single Partition)",
                        new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @tenantId")
                            .WithParameter("@tenantId", "CLIENTE-001")
                    );
                    break;
                case "2":
                    await RunQueryAsync(
                        "Consulta Costosa (Multi Partition / IN)",
                        new QueryDefinition("SELECT * FROM c WHERE c.tenantId IN (@t1, @t2, @t3)")
                            .WithParameter("@t1", "CLIENTE-002")
                            .WithParameter("@t2", "CLIENTE-003")
                            .WithParameter("@t3", "CLIENTE-004")
                    );
                    break;
                case "3":
                    await RunQueryAsync(
                        "Consulta MUY Costosa (Cross-Partition / Fan-out)",
                        new QueryDefinition("SELECT * FROM c WHERE c.level = @level")
                            .WithParameter("@level", "Critical")
                    );
                    break;
                case "4":
                    await RunQueryAsync(
                        "Consulta Ineficiente (Campo no indexado)",
                        new QueryDefinition("SELECT * FROM c WHERE c.payload.ProductId = @productId")
                            .WithParameter("@productId", 1226)
                    );
                    break;
                case "5":
                    Console.WriteLine("Saliendo...");
                    return;
                default:
                    Console.WriteLine("Opción no válida. Inténtalo de nuevo.");
                    break;
            }
        }
    }

    // Este es el método auxiliar que muestra los resultados
    private async Task RunQueryAsync(string queryName, QueryDefinition query)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n--- Ejecutando: {queryName} ---");
        Console.ResetColor();

        var stopwatch = Stopwatch.StartNew();
        double totalRequestCharge = 0;
        int documentCount = 0;
        int pageCount = 0;

        using var iterator = _container.GetItemQueryIterator<dynamic>(query);
        while (iterator.HasMoreResults)
        {
            pageCount++;
            var response = await iterator.ReadNextAsync();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [Página {pageCount}] Documentos: {response.Count}, Costo de Página: {response.RequestCharge:F2} RUs");
            Console.ResetColor();

            totalRequestCharge += response.RequestCharge;
            documentCount += response.Count;
        }
        stopwatch.Stop();
        
        double costPerDocument = (documentCount > 0) ? (totalRequestCharge / documentCount) : 0;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  Documentos encontrados: {documentCount}");
        Console.WriteLine($"  Tiempo total: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  COSTO TOTAL: {totalRequestCharge:F2} RUs");
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"  MÉTRICA DE EFICIENCIA (Costo por Documento): {costPerDocument:F4} RUs");
        Console.ResetColor();
    }
}