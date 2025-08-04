#!/bin/bash

# ===================================================================================
# Script de Generación de Proyecto para el Laboratorio de Logging (V4 - Correcto)
# ===================================================================================
#
# Objetivo: Crear la estructura y código del proyecto C# EN EL DIRECTORIO ACTUAL.
#           Debe ejecutarse DESDE la carpeta del laboratorio (ej. Day4_HighThroughputLogging).
#
# ===================================================================================

# --- Definición de Variables (Rutas relativas al directorio actual) ---
PROJECT_NAME="HighPerfLogger.App"
SRC_DIR="src"
PROJECT_DIR="$SRC_DIR/$PROJECT_NAME"

echo "======================================================"
echo "Creando el proyecto de Logging en el directorio actual"
echo "======================================================"
echo ""

# --- VERIFICACIÓN DE SEGURIDAD ---
if [ -d "$SRC_DIR" ]; then
    echo "❌ ERROR: La carpeta '$SRC_DIR' ya existe en este directorio."
    echo "   Parece que el proyecto ya fue generado. El script se detendrá."
    echo "   Si quieres empezar de nuevo, elimina la carpeta 'src' y vuelve a intentarlo."
    exit 1
fi
# --- FIN DE LA VERIFICACIÓN ---

# --- Creación de Directorios ---
echo "Paso 1/5: Creando la estructura de directorios en './$SRC_DIR'..."
mkdir -p "$PROJECT_DIR"
echo "Estructura creada en '$PROJECT_DIR'."
echo "---"

# --- Creación del Proyecto .NET y Paquetes NuGet ---
echo "Paso 2/5: Creando proyecto de consola .NET y añadiendo paquetes..."
dotnet new console -n "$PROJECT_NAME" -o "$PROJECT_DIR" -f net8.0 > /dev/null
dotnet add "$PROJECT_DIR" package Microsoft.Azure.Cosmos > /dev/null
dotnet add "$PROJECT_DIR" package Microsoft.Extensions.Hosting > /dev/null
dotnet add "$PROJECT_DIR" package Newtonsoft.Json > /dev/null
echo "Proyecto creado y paquetes NuGet añadidos."
echo "---"

# --- Generación de Ficheros de Código C# ---
echo "Paso 3/5: Generando ficheros de código fuente C#..."

# IAppLogger.cs
cat <<EOF > "$PROJECT_DIR/IAppLogger.cs"
namespace HighPerfLogger.App;

public interface IAppLogger
{
    void Log(LogEntry entry);
}
EOF

# LogEntry.cs
cat <<EOF > "$PROJECT_DIR/LogEntry.cs"
using Newtonsoft.Json;

namespace HighPerfLogger.App;

public class LogEntry
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string tenantId { get; set; } // Partition Key
    public DateTime timestamp { get; set; } = DateTime.UtcNow;
    public string correlationId { get; set; } = string.Empty;
    public string service { get; set; } = string.Empty;
    public string user { get; set; } = string.Empty;
    public string level { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
    public object? payload { get; set; }
}
EOF

# CosmosDbLogger.cs
cat <<EOF > "$PROJECT_DIR/CosmosDbLogger.cs"
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
EOF

# Program.cs (El Simulador)
cat <<EOF > "$PROJECT_DIR/Program.cs"
﻿using Microsoft.Azure.Cosmos;
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
EOF

echo "Ficheros C# generados."
echo "---"

# --- Generación de Ficheros de Configuración ---
echo "Paso 4/5: Generando ficheros de configuración..."

# appsettings.json.template
cat <<EOF > "$PROJECT_DIR/appsettings.json.template"
{
  "CosmosDb": {
    "Endpoint": "AQUÍ_VA_EL_ENDPOINT_DE_TU_COSMOS_DB",
    "Key": "AQUÍ_VA_LA_LLAVE_PRIMARIA_DE_TU_COSMOS_DB",
    "DatabaseName": "LoggingDb",
    "ContainerName": "SystemLogs"
  }
}
EOF

cp "$PROJECT_DIR/appsettings.json.template" "$PROJECT_DIR/appsettings.Development.json"

# .gitignore
cat <<EOF > "$PROJECT_DIR/.gitignore"
appsettings.Development.json
appsettings.json
bin/
obj/
*.user
*.suo
.vs/
EOF

echo "Ficheros de configuración y .gitignore creados."
echo "---"

# --- Resumen Final ---
echo "Paso 5/5: ¡Proceso completado!"
echo ""
echo "========================================================================"
echo "✅ ¡ESTRUCTURA DEL PROYECTO LISTA!"
echo ""
echo "Próximos pasos:"
echo "1. Edita el fichero './$PROJECT_DIR/appsettings.Development.json' y"
echo "   reemplaza los placeholders con tus credenciales de Cosmos DB."
echo "2. Abre el proyecto en tu editor y, desde './$PROJECT_DIR', ejecútalo con 'dotnet run'."
echo "========================================================================"