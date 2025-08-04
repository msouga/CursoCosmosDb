# **Manual del Alumno: Laboratorio 4 - Sistema de Log de Alto Rendimiento con Cosmos DB**

**Objetivos del Laboratorio:**

Al finalizar este laboratorio, serás capaz de:

1.  Implementar un sistema de ingesta de datos a alta velocidad usando el patrón "Fire-and-Forget".
2.  Entender y elegir una clave de partición correcta para optimizar las escrituras.
3.  Modificar una directiva de indexación para reducir el costo de RU en las escrituras.
4.  Analizar y comparar el costo real (en RUs) de consultas eficientes e ineficientes.
5.  Comprender la diferencia crítica entre el costo total de una consulta y su eficiencia por documento.


## **Requisitos Previos (Checklist)**

Antes de empezar, asegúrate de tener todo lo siguiente instalado y configurado en tu máquina:

1.  **Git:** Para clonar el repositorio del curso.
2.  **.NET SDK:** Versión 8.0 o superior. (Verifica con `dotnet --version`).
3.  **Azure CLI:** La línea de comandos de Azure. (Verifica con `az --version`).
4.  **Suscripción a Azure:** Necesitas acceso a una suscripción de Azure activa.
5.  **Sesión de Azure CLI Activa:** Abre tu terminal y ejecuta `az login` para iniciar sesión.

## **Parte 1: Preparación y Generación de Carga**

En esta parte, clonaremos el código, desplegaremos la infraestructura en Azure y ejecutaremos la aplicación que genera miles de logs por segundo.

### **Paso 1.1: Obtener el Código del Laboratorio**

1.  Abre tu terminal en la carpeta donde guardas tus proyectos.
2.  Clona el repositorio del curso desde GitHub:

    ```bash
    git clone https://github.com/msouga/CursoCosmosDb.git
    ```
3.  Navega a la carpeta de este laboratorio:

    ```bash
    cd CursoCosmosDb/Day4_HighThroughputLogging
    ```

### **Paso 1.2: Aprovisionar los Recursos en Azure**

Vamos a usar un script para crear automáticamente nuestra cuenta de Cosmos DB.

1.  Dale permisos de ejecución al script:

    ```bash
    chmod +x aprovisionar_recursos_logging.sh
    ```

2.  **Importante:** Abre el archivo `aprovisionar_recursos_logging.sh` y edita la variable `LAB_SUFFIX` para que tenga tus iniciales o un apodo único. Este sufijo debe ser el mismo que usaste en laboratorios anteriores.
3.  Ejecuta el script:

    ```bash
    ./aprovisionar_recursos_logging.sh
    ```
    El script creará un Grupo de Recursos (`LabLoggingRG-...`) y una cuenta de Cosmos DB con una base de datos `LoggingDb` y un contenedor `SystemLogs`.

### **Paso 1.3: Configurar la Aplicación de Logging**

Nuestra aplicación necesita saber cómo conectarse a la base de datos que acabamos de crear.

1.  Vamos a obtener las credenciales usando la Azure CLI. Asegúrate de usar el mismo `LAB_SUFFIX` que en el script anterior.

    ```bash
    # Pega aquí el sufijo que usaste
    LAB_SUFFIX="mst12345" 
    
    # Variables de nombre (no necesita edición)
    RG_NAME="LabLoggingRG-$LAB_SUFFIX"
    COSMOS_ACCOUNT_NAME="cosmosloglab$LAB_SUFFIX"

    # Obtener el Endpoint
    az cosmosdb show -n $COSMOS_ACCOUNT_NAME -g $RG_NAME --query "documentEndpoint" -o tsv

    # Obtener la Llave Primaria
    az cosmosdb keys list -n $COSMOS_ACCOUNT_NAME -g $RG_NAME --query "primaryMasterKey" -o tsv
    ```
2.  Copia los dos valores que te ha devuelto la terminal.
3.  Abre el archivo de configuración: `src/HighPerfLogger.App/appsettings.json`.
4.  Pega el Endpoint y la Key en los campos correspondientes, reemplazando los placeholders.

### **Paso 1.4: Analizar el Código y Ejecutar el Logger de Alto Rendimiento**

Antes de ejecutar la aplicación, es crucial entender cómo está construida para lograr su objetivo: registrar miles de logs sin ralentizar el sistema principal. Abre la solución en tu editor de código (como VS Code) y navega al archivo `src/HighPerfLogger.App/Program.cs`.

#### **Análisis del Código: ¿Qué está pasando realmente?**

Al revisar el código, verás varios conceptos clave de programación de alto rendimiento:

1.  **El Patrón "Fire-and-Forget":** En el corazón de la clase `CosmosDbLogger`, la línea `_ = _container.CreateItemAsync(...)` es la más importante. Envía la orden de escritura a Cosmos DB pero **no la espera (`await`)**. Esto es lo que permite que el bucle principal del simulador continúe inmediatamente su trabajo, sin verse frenado por la latencia de la red o de la base de datos.

2.  **Sincronización y Rendimiento (`Interlocked`):**
    *   Dentro del bucle del simulador (`LogSimulator`), para llevar la cuenta de los logs enviados, no usamos un simple `_logCount++`. Usamos `Interlocked.Increment(ref _logCount)`.
    *   **¿Qué es esto?** `Interlocked` es una herramienta de C# para realizar operaciones "atómicas", es decir, operaciones que son seguras de usar en entornos con múltiples hilos (multithreading). Aunque nuestro simulador es simple, esta es la práctica profesional correcta para contadores compartidos.
    *   **El "Trade-off" (La Compensación):** Estas operaciones atómicas son ligeramente más "costosas" para el CPU que un simple incremento. Este pequeño costo es el precio que pagamos por la seguridad y por tener un contador preciso. Si quitáramos esta llamada y usáramos un simple `++`, la aplicación podría correr una fracción más rápido, pero perderíamos la garantía de que el contador es 100% preciso si la aplicación se volviera más compleja.

3.  **El Apagado Controlado (`StopAsync`):** Hemos sobrescrito el método `StopAsync` en nuestro `LogSimulator`. El "Host" de .NET llama a este método automáticamente cuando presionas `Ctrl + C`. Esto nos permite no cerrar la aplicación de forma abrupta, sino imprimir un resumen final de cuántos logs se enviaron, una práctica esencial en aplicaciones de servicio reales.

#### **Ejecución y Verificación**

Ahora que entiendes cómo funciona el código, vamos a verlo en acción.

1.  Asegúrate de que tu terminal esté en el directorio del proyecto del logger:
    ```bash
    cd src/HighPerfLogger.App
    ```
2.  Ejecuta la aplicación:
    ```bash
    dotnet run
    ```
3.  **Observa el resultado:** Verás aparecer puntos (`.`) rápidamente en la consola. Cada punto representa cientos de logs que se están enviando. Ahora sabes que esto es posible gracias al patrón **Fire-and-Forget**.
4.  Déjalo correr por unos 30 segundos y luego detén la aplicación con `Ctrl + C`. Verás el mensaje de resumen final, que es posible gracias al método `StopAsync` y al contador seguro `Interlocked`.
5.  **Verificación Final en Azure:** Ve al Portal de Azure, entra en tu cuenta de Cosmos DB, abre el Explorador de Datos y mira los "Elementos" del contenedor `SystemLogs`. Verás que está lleno de los miles de documentos que acabas de generar, cada uno con un `level` y un `payload` diferentes, demostrando que el sistema de ingesta funciona a la perfección.


## **Parte 2: Optimización del Contenedor (La Directiva de Indexación)**

**El Escenario:** Nuestro campo `payload` contiene datos variados que no necesitamos para búsquedas. Indexarlo en cada escritura consume RUs y aumenta los costos. Vamos a excluirlo.

1.  En el **Portal de Azure**, dentro de tu cuenta de Cosmos DB, ve a **Explorador de Datos**.
2.  Selecciona el contenedor `SystemLogs` y haz clic en la pestaña **"Scale & Settings"**.
3.  Busca la sección **"Indexing Policy"** (Directiva de Indexación).
4.  Reemplaza el JSON existente con el siguiente, que añade una regla para excluir `/payload/?`:

    ```json
    {
        "indexingMode": "consistent",
        "automatic": true,
        "includedPaths": [
            {
                "path": "/*"
            }
        ],
        "excludedPaths": [
            {
                "path": "/\"_etag\"/?"
            },
            {
                "path": "/payload/?"
            }
        ]
    }
    ```
5.  Haz clic en **Guardar**.
6.  **Conclusión Clave:** Has optimizado el contenedor. A partir de ahora, cada **escritura (`CreateItemAsync`)** será más barata porque Cosmos DB no tiene que indexar el campo `payload`. Esto no afecta a las lecturas que no usen ese campo.

## **Parte 3: Análisis de Consultas y Costo Real**

Ahora vamos a usar la segunda aplicación, `CosmosQueryRunner`, para ver de forma tangible el impacto de nuestras decisiones de diseño (clave de partición e indexación).

### **Paso 3.1: Ejecutar el Demostrador de Costos**

1.  En tu terminal, vuelve al directorio `src`: `cd ..`
2.  Navega al directorio del nuevo proyecto:
    ```bash
    cd CosmosQueryRunner
    ```
3.  Ejecuta la aplicación de consulta:
    ```bash
    dotnet run
    ```
    La aplicación se iniciará y te presentará un menú con 4 consultas para probar.

### **Paso 3.2: Analizar los Resultados**

Ejecuta las consultas 1, 3 y 4 y presta mucha atención a la salida, especialmente a las dos últimas líneas: **COSTO TOTAL** y **MÉTRICA DE EFICIENCIA**.

*   **Prueba la Opción 1 (Eficiente):**
    *   **¿Qué hace?** Busca por la clave de partición. Cosmos DB va directamente a los datos.
    *   **Observarás:** Un costo por documento **extremadamente bajo** (ej. `0.0345 RUs`).

*   **Prueba la Opción 3 (Fan-out):**
    *   **¿Qué hace?** Busca por un campo bien indexado (`level`) pero sin dar la clave de partición. Cosmos DB tiene que preguntar a todas las particiones.
    *   **Observarás:** Un costo total más alto, pero el costo por documento seguirá siendo razonable.

*   **Prueba la Opción 4 (Ineficiente):**
    *   **¿Qué hace?** Busca por un campo (`payload.ProductId`) que **explícitamente excluimos del índice**.
    *   **Observarás:** Un costo por documento **enormemente alto** (ej. `1.4250 RUs`).

### **Paso 3.3: La Conclusión Final - La Métrica Definitiva**

Compara la "MÉTRICA DE EFICIENCIA (Costo por Documento)" entre la consulta 1 y la 4. Verás que la consulta eficiente es docenas de veces más barata por cada documento útil que te entrega.

Has demostrado experimentalmente el principio más importante de la optimización en Cosmos DB:

> Una consulta es **eficiente** no por su costo total, sino por el **bajo costo que pagas por cada unidad de información útil que recuperas**. La consulta 4 es "ineficiente" porque te obliga a pagar un precio muy alto en RUs (por el escaneo completo) para obtener muy pocos resultados.

¡Felicidades! Has completado el laboratorio y has dominado conceptos avanzados de rendimiento y optimización en Azure Cosmos DB.


## **Parte 4: Conclusiones y Próximos Pasos**

¡Felicidades! Has completado con éxito un laboratorio que aborda uno de los casos de uso más comunes e importantes para Cosmos DB: la ingesta de datos a muy alta velocidad.

**Hoy has aprendido a:**

*   Implementar el patrón **"Fire-and-Forget"** para no impactar el rendimiento de tu aplicación principal.
*   Tomar una decisión crítica sobre la **clave de partición (`/tenantId`)** para garantizar la escalabilidad de las escrituras.
*   **Optimizar el costo y rendimiento de las escrituras** modificando la política de indexación para excluir campos innecesarios.
*   Medir y analizar el costo real de diferentes tipos de consultas (eficientes vs. ineficientes) usando la **métrica de costo por documento**.

Has demostrado experimentalmente que una consulta es "eficiente" no por su costo total, sino por el bajo costo que pagas por cada unidad de información útil que recuperas.

### **Adelanto del Día 5: Puesta en Producción y Gran Final**

Todo lo que hemos construido hasta ahora ha utilizado cadenas de conexión y llaves primarias, lo cual es perfecto para el desarrollo y el aprendizaje. Sin embargo, **en un entorno de producción, esto es una mala práctica de seguridad.**

Mañana, en nuestra última sesión, daremos el paso final para convertirnos en arquitectos de Cosmos DB listos para la producción. Veremos los tres pilares de una solución empresarial robusta:

1.  **Seguridad Real:** Aprenderemos cómo nuestras aplicaciones se pueden autenticar en Azure de forma segura usando **Identidad Administrada (Managed Identity)**, eliminando por completo la necesidad de manejar secretos en nuestro código o configuración.
2.  **Optimización de Costos:** Discutiremos estrategias como el **autoescalado** y la **capacidad reservada** para asegurarnos de que nuestras soluciones sean lo más económicas posible sin sacrificar el rendimiento.
3.  **Monitoreo y Alertas:** Aprenderemos a vigilar la salud de nuestra base de datos, centrándonos en la métrica más importante: las **peticiones "throttled" (HTTP 429)**, que nos alertan cuando nos estamos quedando sin RUs.

Y para cerrar con broche de oro, pondremos a prueba todo lo que hemos aprendido durante la semana con un **Quiz Final** que cubrirá los conceptos clave desde el modelado y la partición hasta los patrones de implementación que hemos construido. ¡Prepárate para demostrar tus nuevas habilidades
