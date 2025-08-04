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
            // ... (el resto de la configuración del host no cambia) ...
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
                    new CosmosClient(endpoint, key, new CosmosClientOptions { ApplicationName = "HighPerfLogger.App" })
                );

                services.AddSingleton<IAppLogger>(sp => 
                {
                    var client = sp.GetRequiredService<CosmosClient>();
                    return new CosmosDbLogger(client, databaseName, containerName);
                });

                services.AddHostedService<LogSimulator>();
            })
            .Build();

        // --- CAMBIO 1: Bloque try...finally para un mensaje final ---
        try
        {
            await host.RunAsync();
        }
        finally
        {
            Console.WriteLine("\nLa aplicación ha terminado. Presiona cualquier tecla para cerrar.");
            // Console.ReadKey(); // Descomenta si quieres que la ventana espere
        }
    }
}

public class LogSimulator : BackgroundService
{
    private readonly IAppLogger _logger;
    // --- CAMBIO 2: Mover el contador a un campo de la clase ---
    private long _logCount = 0;

    public LogSimulator(IAppLogger logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Iniciando simulación de generación de logs...");
        Console.WriteLine("Generando logs a alta velocidad. Presiona Ctrl+C para detener...");

        var tenants = new[] { "CLIENTE-001", "CLIENTE-002", "CLIENTE-003", "CLIENTE-004", "CLIENTE-005" };
        var services = new[] { "Invoicing.API", "Inventory.API", "Auth.API", "Shipping.API" };
        var logLevels = new[] { "Information", "Warning", "Critical", "Verbose" };
        
        var rand = new Random();

        // El bucle se detendrá limpiamente cuando stoppingToken sea cancelado por CTRL+C
        while (!stoppingToken.IsCancellationRequested)
        {
            var entry = new LogEntry
            {
                tenantId = tenants[rand.Next(tenants.Length)],
                correlationId = Guid.NewGuid().ToString(),
                service = services[rand.Next(services.Length)],
                level = logLevels[rand.Next(logLevels.Length)],
                // --- CAMBIO 3: Usar Interlocked.Increment para seguridad entre hilos ---
                message = $"Operación {Interlocked.Read(ref _logCount) + 1} completada.",
                payload = new { ProductId = rand.Next(1000, 2000), Quantity = rand.Next(1, 100) }
            };

            _logger.Log(entry);
            Interlocked.Increment(ref _logCount);
            
            if (_logCount % 100 == 0) Console.Write(".");
            
            try
            {
                await Task.Delay(50, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Es normal que esto ocurra al presionar CTRL+C, lo ignoramos.
                break;
            }
        }
    }
    
    // --- CAMBIO 4: Implementar StopAsync para el resumen final ---
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n\n=============================================");
        Console.WriteLine("Señal de apagado recibida. Finalizando...");
        Console.WriteLine($"Total de logs enviados durante la sesión: {_logCount}");
        Console.WriteLine("=============================================");
        Console.ResetColor();
        return base.StopAsync(cancellationToken);
    }
}