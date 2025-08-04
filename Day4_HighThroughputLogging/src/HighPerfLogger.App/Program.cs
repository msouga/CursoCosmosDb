using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HighPerfLogger.App;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) => {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var cosmosConfig = context.Configuration.GetSection("CosmosDb");
                var endpoint = cosmosConfig["Endpoint"];
                var key = cosmosConfig["Key"];
                var databaseName = cosmosConfig["DatabaseName"] ?? "LoggingDb";
                var containerName = cosmosConfig["ContainerName"] ?? "SystemLogs";

                if (string.IsNullOrEmpty(endpoint) || endpoint.StartsWith("AQUÍ_VA"))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ERROR: La configuración de Cosmos DB no está completa.");
                    Console.WriteLine("Por favor, edita 'src/HighPerfLogger.App/appsettings.Development.json' y añade tu Endpoint y Key.");
                    Console.ResetColor();
                    Environment.Exit(1);
                }

                services.AddSingleton(sp => 
                    new CosmosClient(endpoint, key, new CosmosClientOptions 
                    { 
                        ApplicationName = "HighPerfLogger.App" 
                    })
                );

                services.AddSingleton<IAppLogger>(sp => 
                {
                    var client = sp.GetRequiredService<CosmosClient>();
                    return new CosmosDbLogger(client, databaseName, containerName);
                });

                services.AddHostedService<LogSimulator>();
            })
            .Build();

        await host.RunAsync();
    }
}

public class LogSimulator : BackgroundService
{
    private readonly IAppLogger _logger;

    public LogSimulator(IAppLogger logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Iniciando simulación de generación de logs...");
        Console.WriteLine("Generando logs a alta velocidad. Presiona Ctrl+C para detener...");

        var tenants = new[] { "CLIENTE-001", "CLIENTE-002", "CLIENTE-003", "CLIENTE-004", "CLIENTE-005" };
        var services = new[] { "Invoicing.API", "Inventory.API", "Auth.API", "Shipping.API" };
        var rand = new Random();
        long logCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.Log(new LogEntry
            {
                tenantId = tenants[rand.Next(tenants.Length)],
                correlationId = Guid.NewGuid().ToString(),
                service = services[rand.Next(services.Length)],
                level = "Information",
                message = $"Operación {Interlocked.Increment(ref logCount)} completada."
            });
            
            if (logCount % 100 == 0) Console.Write(".");
            await Task.Delay(5, stoppingToken);
        }
    }
    
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("\nSimulación detenida.");
        return base.StopAsync(cancellationToken);
    }
}
