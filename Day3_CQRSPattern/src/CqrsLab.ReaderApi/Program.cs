using CqrsLab.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton((provider) =>
{
    var endpoint = builder.Configuration["CosmosDb:Endpoint"];
    var key = builder.Configuration["CosmosDb:Key"];
    if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
    {
        throw new InvalidOperationException("La configuración de Cosmos DB no está presente en appsettings.json.");
    }
    return new CosmosClient(endpoint, key, new CosmosClientOptions { ApplicationName = "CqrsReaderApi" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/clientes/buscar", async ([FromQuery] string searchTerm, CosmosClient cosmosClient, IConfiguration config) =>
{
    var databaseName = config["CosmosDb:Database"];
    var containerName = config["CosmosDb:Container"];
    var container = cosmosClient.GetContainer(databaseName, containerName);

    var query = new QueryDefinition("SELECT * FROM c WHERE CONTAINS(c.nombre, @searchTerm, true) OR CONTAINS(c.ruc, @searchTerm, true) OR CONTAINS(c.ciudad, @searchTerm, true)")
        .WithParameter("@searchTerm", searchTerm);

    var results = new List<ClienteCosmos>();
    double totalRequestCharge = 0;

    using (var feed = container.GetItemQueryIterator<ClienteCosmos>(query))
    {
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync();
            totalRequestCharge += response.RequestCharge;
            results.AddRange(response.ToList());
        }
    }
    
    // Devolvemos un objeto anónimo que incluye los resultados y el costo para observabilidad.
    return Results.Ok(new { CostoEnRUs = totalRequestCharge, Resultados = results });
})
.WithName("BuscarClientes")
.WithDescription("Busca clientes en la réplica de lectura de Cosmos DB. El término de búsqueda se aplica a los campos nombre, RUC y ciudad.")
.WithOpenApi();

app.Run();
