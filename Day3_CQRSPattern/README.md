# Laboratorio del Día 3: Implementación del Patrón CQRS

## Objetivo del Laboratorio
Construir una réplica de lectura optimizada en Azure Cosmos DB para acelerar las búsquedas de un sistema ERP basado en SQL. Al final de este laboratorio, habrás implementado un flujo de datos completo que desacopla las operaciones de escritura de las de lectura.

## Prerrequisitos
-   Una suscripción de Azure activa.
-   [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) instalado y autenticado (`az login`).
-   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) instalado.
-   [Visual Studio Code](https://code.visualstudio.com/) con las extensiones **C# Dev Kit** y **SQL Server (mssql)**.

---

## Instrucciones Paso a Paso

### Paso 1: Aprovisionar los Recursos en Azure

1.  **Edita el script de aprovisionamiento.** Abre el fichero `aprovisionar_recursos.sh`.
2.  **Personaliza tu sufijo.** En la línea 15, cambia el valor de la variable `LAB_SUFFIX` por algo único para ti (ej. tus iniciales y números, todo en minúsculas).
    ```bash
    LAB_SUFFIX="mst12345" # <-- ¡CAMBIA ESTO!
    ```
3.  **Ejecuta el script desde tu terminal.** Esto creará todos los recursos necesarios en Azure. Puede tardar varios minutos.
    ```bash
    ./aprovisionar_recursos.sh
    ```
4.  Al final, el script te mostrará los **nombres únicos** de los recursos creados. ¡Guárdalos!

### Paso 2: Configurar las Conexiones de la Aplicación

1.  **Crea los ficheros de configuración.** Ejecuta el script de ayuda que hemos preparado:
    ```bash
    ./setup_configs.sh
    ```
    Esto creará los ficheros `local.settings.json` y `appsettings.json` a partir de las plantillas.

2.  **Define las variables en tu terminal.** Reemplaza los valores con los nombres que te dio el script de aprovisionamiento al final.
    ```bash
    RG_NAME="LabCqrsRG-tunuevosufijo"
    SQL_SERVER_NAME="sql-cqrs-tunuevosufijo"
    COSMOS_ACCOUNT_NAME="cosmoscqrslabtunuevosufijo"
    STORAGE_ACCOUNT_NAME="stcqrslabtunuevosufijo"
    ```
3.  **Obtén y pega las cadenas de conexión.** Ejecuta los siguientes comandos y pega la salida en los ficheros correspondientes:

    *   **Para `src/CqrsLab.DataGenerator/appsettings.json` Y `src/CqrsLab.SqlToCosmosFunction/local.settings.json`:**

        *   **`SqlConnectionString`:**
            ```bash
            az sql db show-connection-string --client ado.net --name ErpDb --server $SQL_SERVER_NAME
            ```
            *(Recuerda reemplazar `{your_password}` con `LabPassw0rd!`)*

        *   **`StorageConnectionString` / `StorageQueueConnection`:**
            ```bash
            az storage account show-connection-string --name $STORAGE_ACCOUNT_NAME --resource-group $RG_NAME --query connectionString
            ```

    *   **Para `src/CqrsLab.SqlToCosmosFunction/local.settings.json` Y `src/CqrsLab.ReaderApi/appsettings.Development.json`:**

        *   **`CosmosDbEndpoint`:**
            ```bash
            az cosmosdb show --name $COSMOS_ACCOUNT_NAME --resource-group $RG_NAME --query "documentEndpoint"
            ```
        *   **`CosmosDbKey`:**
            ```bash
            az cosmosdb keys list --name $COSMOS_ACCOUNT_NAME --resource-group $RG_NAME --query "primaryMasterKey"
            ```

¡Excelente punto! Has anticipado un problema de usabilidad muy común en VS Code, especialmente para usuarios nuevos en la extensión `mssql`.

El mensaje de error **`mssql: Un editor de SQL debe tener el foco antes de ejecutar este comando`** es la forma que tiene la extensión de decir: "Sé que quieres conectarte, pero no sé a qué fichero o ventana de consulta asociar esta conexión".

Para evitar esta confusión en el `README.md`, debemos ser mucho más explícitos en las instrucciones.

---
### **Paso 3: Preparar la Base de Datos SQL**

1.  **Abre el script SQL:** En el explorador de ficheros de VS Code, navega y haz doble clic en `scripts/01-create-sql-table.sql`. El fichero se abrirá en el editor.

2.  **Inicia la conexión desde el fichero:**
    *   Con el fichero `01-create-sql-table.sql` abierto y activo, abre la Paleta de Comandos (`Cmd+Shift+P` en Mac, `Ctrl+Shift+P` en Windows/Linux).
    *   Escribe y selecciona `MS SQL: Connect`.
    *   Sigue los pasos para crear el perfil de conexión:
        *   **Server Name:** `TU_SQL_SERVER_NAME.database.windows.net`
        *   **Database:** `ErpDb`
        *   **Authentication Type:** `SQL Login`
        *   **User name:** `azureadmin`
        *   **Password:** `LabPassw0rd!` (y elige si deseas guardarla)
        *   **Profile Name:** "Azure SQL Lab"

3.  **Verifica la Conexión:** Una vez conectado, mira la barra de estado en la parte inferior derecha de VS Code. Deberías ver el nombre de tu servidor y la base de datos, confirmando que el fichero actual está asociado a esa conexión.

4.  **Ejecuta el Script:**
    *   Asegúrate de que tu cursor esté en la ventana del editor con el script SQL.
    *   Haz clic en el icono de **Play (un triángulo verde)** en la esquina superior derecha o presiona el atajo `Cmd+Shift+E` (`Ctrl+Shift+E`).

    Deberías ver un mensaje en la ventana de "Resultados" que dice `Tabla Clientes creada exitosamente.`

---

### Paso 4: Restaurar Dependencias y Ejecutar

1.  **Abre el fichero de la solución `CqrsCosmosLab.sln`.** VS Code debería reconocerlo.
2.  **Restaura los paquetes NuGet.** Abre un terminal en la carpeta `Day3_CQRSPattern` y ejecuta:
    ```bash
    dotnet restore CqrsCosmosLab.sln
    ```
3.  **Inicia los servicios** en terminales separados, como se explicó en el laboratorio.

¡Ahora estás listo para ejecutar las pruebas end-to-end!