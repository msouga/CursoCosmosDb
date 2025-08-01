using System;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using CqrsLab.Shared;
using Dapper;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CqrsLab.SqlToCosmosFunction
{
    public class SincronizarCliente
    {
        private readonly CosmosClient _cosmosClient;
        
        public SincronizarCliente(CosmosClient cosmosClient)
        {
            _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        }

        [Function("SincronizarCliente")]
        public async Task Run([QueueTrigger("cliente-actualizado", Connection = "StorageQueueConnection")] string clienteIdStr,
            FunctionContext context)
        {
            var logger = context.GetLogger<SincronizarCliente>();
            logger.LogInformation($"[DIAGNÓSTICO] PASO 1: Procesando mensaje: {clienteIdStr}");

            if (!int.TryParse(clienteIdStr, out int clienteId))
            {
                logger.LogError($"El mensaje '{clienteIdStr}' no es un ID de cliente válido.");
                return;
            }

            var sqlConnectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            ClienteCosmos? cliente = null;

            try
            {
                logger.LogInformation("[DIAGNÓSTICO] PASO 2: Intentando conectar a SQL Server...");
                await using (var connection = new SqlConnection(sqlConnectionString))
                {
                    await connection.OpenAsync(); // Intentamos abrir la conexión explícitamente
                    logger.LogInformation("[DIAGNÓSTICO] PASO 3: Conexión a SQL exitosa. Ejecutando consulta...");
                    
                    var sql = "SELECT CAST(ClienteId AS NVARCHAR(10)) as Id, Nombre, RUC, Ciudad, Pais FROM Clientes WHERE ClienteId = @Id";
                    cliente = await connection.QuerySingleOrDefaultAsync<ClienteCosmos>(sql, new { Id = clienteId });

                    logger.LogInformation("[DIAGNÓSTICO] PASO 4: Consulta a SQL completada.");
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex, $"[DIAGNÓSTICO] FALLO EN SQL: Error crítico al conectar o leer desde SQL para cliente {clienteId}.");
                throw; 
            }

            if (cliente == null)
            {
                logger.LogWarning($"Cliente con ID {clienteId} no encontrado en SQL DB. Ignorando.");
                return;
            }
            
            if (string.IsNullOrEmpty(cliente.Pais))
            {
                logger.LogError($"El cliente con ID {cliente.Id} tiene un valor de 'Pais' nulo o vacío. Descartando mensaje.");
                return;
            }

            try
            {
                logger.LogInformation("[DIAGNÓSTICO] PASO 5: Intentando obtener el contenedor de Cosmos DB...");
                var container = _cosmosClient.GetContainer("ReadModelDb", "Clientes");
                logger.LogInformation("[DIAGNÓSTICO] PASO 6: Contenedor obtenido. Intentando escribir en Cosmos DB...");
                
                var response = await container.UpsertItemAsync(cliente, new PartitionKey(cliente.Pais));
                
                logger.LogInformation($"[DIAGNÓSTICO] PASO 7: ÉXITO. Cliente '{cliente.Nombre}' (ID: {cliente.Id}) sincronizado. Costo: {response.RequestCharge} RUs.");
            }
            catch (CosmosException ex)
            {
                logger.LogError(ex, $"[DIAGNÓSTICO] FALLO EN COSMOS DB: Error al escribir el cliente {clienteId}.");
                throw;
            }
        }
    }
}