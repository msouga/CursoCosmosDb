using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;
using Azure.Storage.Queues;

namespace CqrsLab.DataGenerator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Iniciando Generador de Datos para el ERP...");

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var sqlConnectionString = config.GetConnectionString("SqlConnectionString");
            var storageConnectionString = config.GetConnectionString("StorageConnectionString");
            const string queueName = "cliente-actualizado";

            if (string.IsNullOrEmpty(sqlConnectionString) || string.IsNullOrEmpty(storageConnectionString))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Las cadenas de conexión 'SqlConnectionString' y 'StorageConnectionString' deben estar configuradas en appsettings.json.");
                Console.ResetColor();
                return;
            }

            // *** ESTA ES LA CORRECCIÓN DEFINITIVA ***
            // Creamos el cliente de la cola instruyéndole explícitamente
            // que codifique todos los mensajes en Base64.
            var queueClient = new QueueClient(storageConnectionString, queueName, new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            });

            await queueClient.CreateIfNotExistsAsync();
            Console.WriteLine($"Conectado a la cola: '{queueName}' (Modo de codificación: Base64 explícito)");
            Console.WriteLine("---");

            while (true)
            {
                Console.WriteLine("Presiona Enter para generar un nuevo cliente o escribe 'salir' para terminar.");
                var input = Console.ReadLine();
                if (input?.ToLower() == "salir")
                {
                    break;
                }

                try
                {
                    var (nombre, ruc, ciudad, pais) = GenerateRandomClient();
                    Console.WriteLine($"Generando cliente: {nombre} en {ciudad}, {pais}");

                    await using var connection = new SqlConnection(sqlConnectionString);
                    
                    var sql = @"
                        INSERT INTO Clientes (Nombre, RUC, Ciudad, Pais) 
                        VALUES (@Nombre, @RUC, @Ciudad, @Pais);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";
                    
                    int newClientId = await connection.QuerySingleAsync<int>(sql, new { Nombre = nombre, RUC = ruc, Ciudad = ciudad, Pais = pais });

                    if (newClientId > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Éxito: Cliente insertado en SQL con ID: {newClientId}");
                        Console.ResetColor();

                        string messageContent = newClientId.ToString();
                        
                        await queueClient.SendMessageAsync(messageContent);
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"Mensaje '{messageContent}' enviado a la cola '{queueName}'.");
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Ocurrió un error: {ex.Message}");
                    Console.ResetColor();
                }
                Console.WriteLine("---");
            }
        }
        
        private static (string Nombre, string Ruc, string Ciudad, string Pais) GenerateRandomClient()
        {
            var random = new Random();
            string[] Nombres = { "Inversiones del Sol SAC", "Comercializadora Andina", "Servicios Logísticos del Pacifico", "Exportadora del Norte", "Tecnología y Sistemas Globales", "Agroindustrias del Valle EIRL", "Constructora e Inmobiliaria Horizonte", "Distribuidora de Alimentos del Sur", "Soluciones Mineras Integrales", "Factoría Textil de los Andes", "Consultores Financieros y Asociados", "Innovaciones Plásticas Industriales", "Transportes y Carga Panamericana", "Grupo Editorial Cóndor", "Desarrollos de Software Latinoamericanos", "Maderas y Enchapados de la Amazonía", "Laboratorios Químicos del Perú", "Proyectos Energéticos Renovables", "Clínica y Centro Médico San Pablo", "Academia de Idiomas El Continental" };
            string[] CiudadesPeru = { "Lima", "Arequipa", "Trujillo", "Chiclayo", "Tacna" };
            string[] CiudadesChile = { "Santiago", "Valparaíso", "Concepción", "Temuco" };

            var pais = random.Next(2) == 0 ? "Peru" : "Chile";
            var ciudad = pais == "Peru" ? CiudadesPeru[random.Next(CiudadesPeru.Length)] : CiudadesChile[random.Next(CiudadesChile.Length)];
            
            return (
                Nombre: $"{Nombres[random.Next(Nombres.Length)]} {random.Next(10, 99)}",
                Ruc: $"{random.Next(10000000, 99999999)}-{random.Next(0, 9)}",
                Ciudad: ciudad,
                Pais: pais
            );
        }
    }
}