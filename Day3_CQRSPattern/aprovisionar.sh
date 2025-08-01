#!/bin/bash

# ===================================================================================
# Script de Aprovisionamiento para el Laboratorio de Cosmos DB CQRS
# ===================================================================================
#
# Objetivo: Crear todos los recursos de Azure necesarios para el laboratorio del Día 3.
#
# Instrucciones para el Alumno:
# 1. Edita la variable LAB_SUFFIX a continuación. Usa algo único
#    (ej. tus iniciales + algunos números).
# 2. Guarda el fichero.
# 3. Dale permisos de ejecución: chmod +x aprovisionar_recursos.sh
# 4. Ejecútalo: ./aprovisionar_recursos.sh
#
# ===================================================================================

# --- PASO 1: CONFIGURACIÓN PERSONALIZADA (EDITAR ESTA LÍNEA) ---

# Usa tus iniciales o un apodo + números. Debe tener entre 3 y 8 caracteres.
# IMPORTANTE: ¡SOLO LETRAS MINÚSCULAS Y NÚMEROS!
LAB_SUFFIX="mst12345"

# --- Variables Globales (No editar a menos que sea necesario) ---
LOCATION="westus2"
RG_NAME="LabCqrsRG-$LAB_SUFFIX"

SQL_DB_NAME="ErpDb"
SQL_ADMIN_USER="azureadmin"
SQL_ADMIN_PASS="LabPassw0rd!"

# --- Generación de Nombres de Recursos (Automático) ---
# Se asegura de que todas las variables cumplan las reglas de nomenclatura de Azure.

# Los nombres de SQL y Functions pueden tener guiones.
SQL_SERVER_NAME="sql-cqrs-$LAB_SUFFIX"
FUNCTION_APP_NAME="func-cqrs-$LAB_SUFFIX"

# Cosmos DB y Storage Accounts NO pueden tener guiones y deben ser únicos globalmente.
COSMOS_ACCOUNT_NAME="cosmoscqrslab$LAB_SUFFIX"
STORAGE_ACCOUNT_NAME="stcqrslab$LAB_SUFFIX"

# --- Inicio del Script de Aprovisionamiento ---
echo "======================================================"
echo "Iniciando Aprovisionamiento con el Sufijo: $LAB_SUFFIX"
echo "Grupo de Recursos a crear: $RG_NAME"
echo "======================================================"
echo ""

# --- Creación del Grupo de Recursos ---
echo "Paso 1/5: Creando Grupo de Recursos '$RG_NAME' en '$LOCATION'..."
az group create --name "$RG_NAME" --location "$LOCATION" -o none
echo "Grupo de Recursos creado."
echo "---"

# --- Registro de Proveedores de Recursos ---
echo "Paso 2/5: Verificando y registrando los proveedores de recursos..."
PROVIDERS=("Microsoft.Sql" "Microsoft.DocumentDB" "Microsoft.Storage" "Microsoft.Web")
for provider in "${PROVIDERS[@]}"; do
    if [[ $(az provider show -n "$provider" -o tsv --query "registrationState") != "Registered" ]]; then
        echo "Registrando $provider (esto puede tardar unos minutos)..."
        az provider register --namespace "$provider"
        # Espera en silencio hasta que el registro esté completo
        while [[ $(az provider show -n "$provider" -o tsv --query "registrationState") != "Registered" ]]; do
            sleep 10
        done
        echo "Proveedor $provider registrado."
    else
        echo "Proveedor $provider ya está registrado."
    fi
done
echo "Todos los proveedores están listos."
echo "---"

# --- Aprovisionamiento de SQL ---
echo "Paso 3/5: Creando SQL Server '$SQL_SERVER_NAME'..."
az sql server create --name "$SQL_SERVER_NAME" --resource-group "$RG_NAME" --location "$LOCATION" --admin-user "$SQL_ADMIN_USER" --admin-password "$SQL_ADMIN_PASS" -o none
az sql db create --resource-group "$RG_NAME" --server "$SQL_SERVER_NAME" --name "$SQL_DB_NAME" --service-objective S0 -o none
echo "Configurando reglas de firewall para SQL..."
az sql server firewall-rule create --resource-group "$RG_NAME" --server "$SQL_SERVER_NAME" --name "AllowAzureServices" --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 -o none
MY_IP=$(curl -s ifconfig.me)
if [ -n "$MY_IP" ]; then
    az sql server firewall-rule create --resource-group "$RG_NAME" --server "$SQL_SERVER_NAME" --name "AllowMyLocalIP" --start-ip-address "$MY_IP" --end-ip-address "$MY_IP" -o none
    echo "Regla de firewall creada para tu IP local ($MY_IP)."
else
    echo "ADVERTENCIA: No se pudo detectar tu IP local. Añádela manualmente en el portal de Azure."
fi
echo "SQL Server creado y configurado."
echo "---"

# --- Aprovisionamiento de Cosmos DB ---
echo "Paso 4/5: Creando Cuenta de Cosmos DB '$COSMOS_ACCOUNT_NAME'..."
az cosmosdb create --name "$COSMOS_ACCOUNT_NAME" --resource-group "$RG_NAME" --locations "regionName=$LOCATION" -o none
az cosmosdb sql database create --account-name "$COSMOS_ACCOUNT_NAME" --resource-group "$RG_NAME" --name "ReadModelDb" -o none
az cosmosdb sql container create --account-name "$COSMOS_ACCOUNT_NAME" --resource-group "$RG_NAME" --database-name "ReadModelDb" --name "Clientes" --partition-key-path "/pais" -o none
echo "Cosmos DB creado y configurado."
echo "---"

# --- Aprovisionamiento de Storage y Function App ---
echo "Paso 5/5: Creando Storage Account y Function App..."
az storage account create --name "$STORAGE_ACCOUNT_NAME" --location "$LOCATION" --resource-group "$RG_NAME" --sku Standard_LRS -o none
az storage queue create --name "cliente-actualizado" --account-name "$STORAGE_ACCOUNT_NAME" -o none
az functionapp create --resource-group "$RG_NAME" --consumption-plan-location "$LOCATION" --runtime dotnet-isolated --functions-version 4 --name "$FUNCTION_APP_NAME" --storage-account "$STORAGE_ACCOUNT_NAME" -o none
echo "Storage Account y Function App creados."
echo "---"

# --- Resumen Final ---
echo "========================================================================"
echo "¡APROVISIONAMIENTO COMPLETADO!"
echo ""
echo "Tu Grupo de Recursos es: $RG_NAME"
echo ""
echo "Nombres de recursos para tus cadenas de conexión:"
echo "------------------------------------------------"
echo "SQL Server Name:        $SQL_SERVER_NAME"
echo "Cosmos DB Account Name:   $COSMOS_ACCOUNT_NAME"
echo "Storage Account Name:   $STORAGE_ACCOUNT_NAME"
echo "------------------------------------------------"
echo "Ahora, usa la CLI de Azure o el portal para obtener las cadenas de conexión"
echo "y configurar tus ficheros 'local.settings.json' y 'appsettings.Development.json'."
echo "========================================================================"
