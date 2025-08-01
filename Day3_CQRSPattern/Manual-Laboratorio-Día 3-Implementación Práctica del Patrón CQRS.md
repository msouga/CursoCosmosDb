# **Manual del Laboratorio del Día 3: Implementación Práctica del Patrón CQRS**

## Índice del Manual
1.  **Objetivo del Laboratorio**
1.  **Visión General de la Infraestructura en Azure**
2.  **Arquitectura de la Solución y Flujo de Datos**
3.  **Análisis del Código: ¿Cómo Funciona Cada Pieza?**
4.  **Guía de Pruebas End-to-End**
5.  **Conclusión**

## 1. Objetivo del Laboratorio

Bienvenido al primer laboratorio práctico del curso. Hoy vamos a trascender la teoría para construir una solución real a un problema común: **las búsquedas lentas en una base de datos transaccional.**

Al final de esta sesión, habrás implementado el patrón **CQRS (Command Query Responsibility Segregation)**. Habrás creado un sistema donde:

1.  Las **escrituras** (Comandos) se siguen realizando en una base de datos SQL, optimizada para la integridad y las transacciones.
2.  Las **lecturas** (Consultas) se realizan en una réplica optimizada en **Azure Cosmos DB**, diseñada para búsquedas flexibles y ultra rápidas.

## 2. Visión General de la Infraestructura en Azure**

Antes de analizar el código, es fundamental entender sobre qué cimientos estamos construyendo. Para este laboratorio, hemos desplegado un conjunto de servicios PaaS (Plataforma como Servicio) en Azure. Esto significa que Microsoft gestiona la infraestructura subyacente (hardware, redes, sistema operativo), permitiéndonos centrarnos únicamente en nuestra aplicación.

> Nuestro script `aprovisionar_recursos.sh` crea los siguientes componentes dentro de un único Grupo de Recursos (`LabCqrsRG-tunombbre`):


### a) Azure SQL Database
*   **Rol en el Laboratorio:** Nuestra base de datos **OLTP (Procesamiento de Transacciones en Línea)**. Es la "Fuente de la Verdad" para los datos de nuestros clientes.
*   **¿Por qué SQL?** Porque garantiza la integridad de los datos, las relaciones y la consistencia transaccional (ACID), características indispensables para el núcleo de un sistema ERP.
*   **Recursos Creados:**
    *   Un **Servidor SQL Lógico:** Actúa como un punto de administración central para un grupo de bases de datos.
    *   Una **Base de Datos Única (`ErpDb`):** Donde reside nuestra tabla `Clientes`.
    *   **Reglas de Firewall:** Configuramos dos reglas: una para permitir el acceso desde cualquier servicio de Azure (necesario para que nuestra Azure Function pueda conectarse) y otra para permitir el acceso desde tu máquina local (para que puedas administrarla con VS Code).

### b) Azure Cosmos DB
*   **Rol en el Laboratorio:** Nuestra base de datos de **lectura optimizada**. Almacena una copia desnormalizada de los datos de los clientes para búsquedas rápidas.
*   **¿Por qué Cosmos DB?** Por su velocidad de lectura de milisegundos, su indexación automática y su capacidad para manejar datos semi-estructurados (JSON) de forma nativa.
*   **Recursos Creados:**
    *   Una **Cuenta de Cosmos DB:** El recurso principal. La configuramos para usar la **API for NoSQL**.
    *   Una **Base de Datos (`ReadModelDb`):** Un contenedor lógico.
    *   Un **Contenedor (`Clientes`):** Aquí es donde se guardan los documentos. Lo configuramos con `"/pais"` como **clave de partición**, una decisión de diseño clave para la escalabilidad.

### c) Azure Storage Account
*   **Rol en el Laboratorio:** Actúa como el sistema nervioso de nuestra arquitectura de eventos. Es un servicio de almacenamiento versátil.
*   **¿Por qué Storage Account?** Porque proporciona varios servicios en uno, siendo el más importante para nosotros la cola de mensajes.
*   **Recursos Creados:**
    *   Una **Cuenta de Almacenamiento (v2 de uso general):** El recurso principal.
    *   Una **Cola de Almacenamiento (`cliente-actualizado`):** Un búfer FIFO (First-In, First-Out) simple y robusto donde nuestro Generador de Datos deposita los mensajes.

### d) Azure Functions App
*   **Rol en el Laboratorio:** El "cerebro" sin servidor de nuestra operación de sincronización. Es el componente de cómputo que ejecuta nuestro código.
*   **¿Por qué Azure Functions?** Porque es una forma económica y escalable de ejecutar pequeños fragmentos de código en respuesta a eventos (como un nuevo mensaje en una cola) sin tener que gestionar servidores.
*   **Recursos Creados:**
    *   Una **Function App:** El entorno de hospedaje para nuestra función.
    *   Un **Plan de Consumo:** El modelo de facturación. Solo pagamos por los segundos que nuestra función está realmente ejecutándose.
    *   **Application Insights:** (Creado automáticamente) Un potente servicio de telemetría para monitorear la ejecución, el rendimiento y los errores de nuestra función.


## 3. Arquitectura de la Solución y Flujo de Datos

Para lograr este desacoplamiento, no conectaremos los sistemas directamente. Usaremos una arquitectura controlada por eventos, que es resiliente y escalable.

**Componentes Clave y su Rol:**

*   **Generador de Datos (`CqrsLab.DataGenerator`):** Simula nuestra aplicación ERP. Es el único que **escribe** en la base de datos SQL. Tras cada escritura, notifica al resto del sistema poniendo un mensaje en una cola.
*   **Base de Datos SQL:** Nuestra "Fuente de la Verdad" (Source of Truth). Contiene los datos originales y garantiza la integridad.
*   **Azure Storage Queue:** El "mensajero". Actúa como un búfer desacoplado y persistente. Garantiza que los mensajes de actualización no se pierdan, incluso si el sistema de sincronización está caído.
*   **Azure Function (`CqrsLab.SqlToCosmosFunction`):** El "pegamento" o "sincronizador". Es un trabajador asíncrono que se activa cuando llega un nuevo mensaje a la cola. Su única misión es leer el dato completo desde SQL y escribirlo en su formato de lectura optimizado en Cosmos DB.
*   **Azure Cosmos DB:** Nuestra "Réplica de Lectura". Almacena los datos de forma desnormalizada (documentos JSON) e indexa automáticamente cada campo, permitiendo búsquedas muy rápidas.
*   **API de Lectura (`CqrsLab.ReaderApi`):** La cara visible para los clientes que necesitan buscar datos. **Lee exclusivamente de Cosmos DB**, garantizando respuestas rápidas sin sobrecargar la base de datos SQL principal.

## 4. Análisis del Código: ¿Cómo Funciona Cada Pieza?

### A. El Generador de Datos (`CqrsLab.DataGenerator`)

Este programa simula las escrituras en nuestro sistema.

**`Program.cs` - Puntos Clave:**

1.  **Conexión a la Cola (con codificación correcta):**
    
    ```csharp
    var queueClient = new QueueClient(storageConnectionString, queueName, new QueueClientOptions
    {
        MessageEncoding = QueueMessageEncoding.Base64
    });
    ```
    Creamos el cliente de la cola instruyéndole explícitamente que codifique los mensajes en **Base64**. Esto es crucial para que sea compatible con lo que el host de Azure Functions espera.

2.  **Inserción y Obtención del ID:**
    
    ```csharp
    var sql = @"
        INSERT INTO Clientes (Nombre, RUC, Ciudad, Pais) 
        VALUES (@Nombre, @RUC, @Ciudad, @Pais);
        SELECT CAST(SCOPE_IDENTITY() AS INT);";
    
    int newClientId = await connection.QuerySingleAsync<int>(sql, ...);
    ```
    Usamos `SCOPE_IDENTITY()` para que SQL nos devuelva de forma fiable el `ClienteId` que acaba de generar para el nuevo registro.

3.  **Envío del Mensaje:**
    
    ```csharp
    string messageContent = newClientId.ToString();
    await queueClient.SendMessageAsync(messageContent);
    ```
    Ponemos el ID del nuevo cliente (convertido a string) en la cola. Esto es todo lo que el resto del sistema necesita saber para iniciar la sincronización.

### B. La Función de Sincronización (`CqrsLab.SqlToCosmosFunction`)

Este es el corazón de la sincronización.

**`SincronizarCliente.cs` - Puntos Clave:**

1.  **El Disparador (Trigger):**
    
    ```csharp
    [Function("SincronizarCliente")]
    public async Task Run(
        [QueueTrigger("cliente-actualizado", Connection = "StorageQueueConnection")] string clienteIdStr,
        ...)
    ```
    El atributo `[QueueTrigger]` le dice al host de Azure Functions que ejecute este método automáticamente cada vez que aparezca un nuevo mensaje en la cola `cliente-actualizado`. El contenido del mensaje se pasa como el parámetro `clienteIdStr`.

2.  **Lectura desde SQL:**
    
    ```csharp
    var sql = "SELECT CAST(ClienteId AS NVARCHAR(10)) as Id, Nombre, RUC, Ciudad, Pais FROM Clientes WHERE ClienteId = @Id";
    cliente = await connection.QuerySingleOrDefaultAsync<ClienteCosmos>(sql, new { Id = clienteId });
    ```
    Usamos Dapper para leer el registro desde SQL. **La corrección crucial** que hicimos fue `CAST(ClienteId AS NVARCHAR(10)) as Id` para asegurarnos de que el ID se mapee a un `string`, ya que la propiedad `id` de un documento de Cosmos DB debe ser un string.

3.  **Escritura en Cosmos DB:**
    
    ```csharp
    var container = _cosmosClient.GetContainer("ReadModelDb", "Clientes");
    var response = await container.UpsertItemAsync(cliente, new PartitionKey(cliente.Pais));
    ```
    Usamos `UpsertItemAsync`. Esta operación es "idempotente": si un documento con ese `id` y `partitionKey` ya existe, lo actualiza; si no, lo crea. Esto es perfecto para nuestro caso de uso. También proporcionamos la `PartitionKey` (`cliente.Pais`), que es fundamental para que Cosmos DB sepa dónde almacenar el documento.

### C. La API de Lectura (`CqrsLab.ReaderApi`)

Este servicio expone los datos de nuestra réplica de lectura.

**`Program.cs` - Puntos Clave:**

1.  **El Endpoint:**
    
    ```csharp
    app.MapGet("/clientes/buscar", async ([FromQuery] string searchTerm, ...) => { ... });
    ```
    Definimos una ruta `GET` que acepta un parámetro de consulta (`query parameter`) llamado `searchTerm`.

2.  **La Consulta a Cosmos DB:**
    
    ```csharp
    var query = new QueryDefinition(
        "SELECT * FROM c WHERE CONTAINS(c.nombre, @searchTerm, true) OR CONTAINS(c.ruc, @searchTerm, true) OR CONTAINS(c.ciudad, @searchTerm, true) OR CONTAINS(c.pais, @searchTerm, true)")
        .WithParameter("@searchTerm", searchTerm);
    ```
    Construimos una consulta SQL para NoSQL. La función `CONTAINS` es muy potente, ya que realiza una búsqueda de subcadena en los campos especificados. El tercer parámetro `true` la hace insensible a mayúsculas/minúsculas. Gracias a que Cosmos DB indexa todo por defecto, esta consulta es muy eficiente.

## 5. Conclusión

¡Felicidades! Has construido, depurado y ejecutado un sistema distribuido funcional. Has aplicado el patrón CQRS para resolver un problema de rendimiento del mundo real, utilizando las fortalezas de cada base de datos:

*   **SQL Server** para escrituras consistentes y transaccionales.
*   **Azure Cosmos DB** para lecturas flexibles y de alta velocidad.
*   **Azure Functions y Storage Queues** para un acoplamiento débil y una arquitectura resiliente.

Este laboratorio es la base para entender arquitecturas más complejas y escalables en la nube.