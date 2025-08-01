#!/bin/bash

# Script de ayuda para preparar los ficheros de configuración locales
# a partir de las plantillas del repositorio.

echo "Preparando ficheros de configuración..."
echo "---"

# Definir las rutas de las plantillas y los destinos
TEMPLATE_FUNCTION="src/CqrsLab.SqlToCosmosFunction/local.settings.json.template"
TARGET_FUNCTION="src/CqrsLab.SqlToCosmosFunction/local.settings.json"

TEMPLATE_API="src/CqrsLab.ReaderApi/appsettings.Development.json.template"
TARGET_API="src/CqrsLab.ReaderApi/appsettings.Development.json"

TEMPLATE_GENERATOR="src/CqrsLab.DataGenerator/appsettings.json.template"
TARGET_GENERATOR="src/CqrsLab.DataGenerator/appsettings.json"

# Función para copiar si no existe
copy_if_not_exists() {
    if [ -f "$2" ]; then
        echo "ADVERTENCIA: El fichero '$2' ya existe. No se sobrescribirá."
    else
        cp "$1" "$2"
        echo "ÉXITO: Creado '$2' a partir de la plantilla."
    fi
}

copy_if_not_exists "$TEMPLATE_FUNCTION" "$TARGET_FUNCTION"
copy_if_not_exists "$TEMPLATE_API" "$TARGET_API"
copy_if_not_exists "$TEMPLATE_GENERATOR" "$TARGET_GENERATOR"

echo "---"
echo "¡Configuración lista! Ahora edita los ficheros .json con tus cadenas de conexión."