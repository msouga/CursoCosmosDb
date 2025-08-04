#!/bin/bash

# ===================================================================================
# Script de Aprovisionamiento para el Laboratorio de Logging de Alto Rendimiento
# ===================================================================================
#
# Objetivo: Crear los recursos de Azure necesarios para el laboratorio del Día 4.
#           Reutiliza el sufijo del laboratorio anterior para mantener la consistencia.
#
# Instrucciones para el Alumno:
# 1. Asegúrate de que la variable LAB_SUFFIX es la misma que usaste en el Lab 3.
# 2. Guarda el fichero.
# 3. Dale permisos de ejecución: chmod +x aprovisionar_recursos_logging.sh
# 4. Ejecútalo: ./aprovisionar_recursos_logging.sh
#
# ===================================================================================

# --- PASO 1: CONFIGURACIÓN PERSONALIZADA (VERIFICAR ESTA LÍNEA) ---

# Usa el MISMO sufijo del laboratorio anterior.
# IMPORTANTE: ¡SOLO LETRAS MINÚSCULAS Y NÚMEROS!
LAB_SUFFIX="mst12345"

# --- Variables Globales (No editar a menos que sea necesario) ---
LOCATION="westus2"
# Se crea un NUEVO grupo de recursos para mantener los laboratorios aislados.
RG_NAME="LabLoggingRG-$LAB_SUFFIX"

# --- Generación de Nombres de Recursos (Automático) ---
# Cosmos DB no puede tener guiones y debe ser único globalmente.
COSMOS_ACCOUNT_NAME="cosmosloglab$LAB_SUFFIX"

# --- Inicio del Script de Aprovisionamiento ---
echo "======================================================"
echo "Iniciando Aprovisionamiento para Lab de Logging con el Sufijo: $LAB_SUFFIX"
echo "Grupo de Recursos a crear: $RG_NAME"
echo "======================================================"
echo ""

# --- Creación del Grupo de Recursos ---
echo "Paso 1/3: Creando Grupo de Recursos '$RG_NAME' en '$LOCATION'..."
az group create --name "$RG_NAME" --location "$LOCATION" -o none
echo "Grupo de Recursos creado."
echo "---"

# --- Registro de Proveedores de Recursos ---
echo "Paso 2/3: Verificando el proveedor de recursos 'Microsoft.DocumentDB'..."
PROVIDER="Microsoft.DocumentDB"
if [[ $(az provider show -n "$PROVIDER" -o tsv --query "registrationState") != "Registered" ]]; then
    echo "Registrando $PROVIDER (esto puede tardar unos minutos)..."
    az provider register --namespace "$PROVIDER"
    # Espera en silencio hasta que el registro esté completo
    while [[ $(az provider show -n "$PROVIDER" -o tsv --query "registrationState") != "Registered" ]]; do
        sleep 10
    done
    echo "Proveedor $PROVIDER registrado."
else
    echo "Proveedor $PROVIDER ya está registrado."
fi
echo "Proveedor de Cosmos DB está listo."
echo "---"

# --- Aprovisionamiento de Cosmos DB ---
echo "Paso 3/3: Creando Cuenta de Cosmos DB '$COSMOS_ACCOUNT_NAME'..."
az cosmosdb create --name "$COSMOS_ACCOUNT_NAME" --resource-group "$RG_NAME" --locations "regionName=$LOCATION" -o none

echo "Creando base de datos 'LoggingDb'..."
az cosmosdb sql database create --account-name "$COSMOS_ACCOUNT_NAME" --resource-group "$RG_NAME" --name "LoggingDb" -o none

echo "Creando contenedor 'SystemLogs' con clave de partición '/tenantId'..."
az cosmosdb sql container create --account-name "$COSMOS_ACCOUNT_NAME" --resource-group "$RG_NAME" --database-name "LoggingDb" --name "SystemLogs" --partition-key-path "/tenantId" -o none
echo "Cosmos DB creado y configurado."
echo "---"


# --- Resumen Final ---
echo "========================================================================"
echo "¡APROVISIONAMIENTO COMPLETADO!"
echo ""
echo "Tu nuevo Grupo de Recursos es: $RG_NAME"
echo ""
echo "Nombre de recurso para tu cadena de conexión:"
echo "------------------------------------------------"
echo "Cosmos DB Account Name:   $COSMOS_ACCOUNT_NAME"
echo "------------------------------------------------"
echo "Recuerda que para optimizar costos, debes entrar al portal de Azure,"
echo "navegar al contenedor 'SystemLogs', ir a 'Configuración > Directiva de indexación'"
echo "y excluir la ruta '/payload/?' como se indica en el manual del laboratorio."
echo "========================================================================"