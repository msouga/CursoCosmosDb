using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

var host = new HostBuilder()
    // *** ESTA ES LA LÍNEA CORREGIDA ***
    .ConfigureFunctionsWebApplication() 
    .ConfigureServices(services =>
    {
        // Inyectar el CosmosClient como Singleton para reutilizar conexiones
        services.AddSingleton((provider) =>
        {
            var endpoint = Environment.GetEnvironmentVariable("CosmosDbEndpoint");
            var key = Environment.GetEnvironmentVariable("CosmosDbKey");
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("Las variables de entorno de Cosmos DB (Endpoint y Key) no están configuradas.");
            }
            return new CosmosClient(endpoint, key, new CosmosClientOptions { ApplicationName = "CqrsSyncFunction" });
        });
    })
    .Build();

host.Run();