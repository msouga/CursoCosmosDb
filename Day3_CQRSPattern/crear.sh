#!/bin/bash

# Script para crear la estructura del proyecto del laboratorio del Día 3
# VERSIÓN FINAL Y VERIFICADA: Corregido el comando 'dotnet new func'.

# --- Inicio del Script ---
echo "Iniciando la creación de la estructura del proyecto para el Laboratorio del Día 3..."

# --- Paso 1: Instalar Plantillas de Azure Functions (si es necesario) ---
if ! dotnet new list | grep -q "func"; then
    echo "Plantillas de Azure Functions no encontradas. Instalando..."
    dotnet new install Microsoft.Azure.Functions.Worker.ProjectTemplates
    if [ $? -ne 0 ]; then
        echo "Error crítico: No se pudieron instalar las plantillas de Azure Functions. Abortando."
        exit 1
    fi
    echo "Plantillas de Azure Functions instaladas correctamente."
else
    echo "Las plantillas de Azure Functions ya están instaladas."
fi
echo "---"

# --- Paso 2: Crear la Solución ---
echo "Creando la solución CqrsCosmosLab.sln..."
dotnet new sln --name CqrsCosmosLab
if [ $? -ne 0 ]; then echo "Fallo al crear la solución."; exit 1; fi

# --- Paso 3: Crear Directorios ---
echo "Creando directorios 'src' y 'scripts'..."
mkdir -p src
mkdir -p scripts

# --- Paso 4: Crear los Proyectos dentro de 'src' ---
echo "Creando proyectos..."
dotnet new classlib -o src/CqrsLab.Shared -f net8.0
if [ $? -ne 0 ]; then echo "Fallo al crear CqrsLab.Shared"; exit 1; fi

# *** LÍNEA CORREGIDA FINAL: Se elimina el parámetro -f net8.0, ya que no es válido para 'dotnet new func'. ***
dotnet new func -o src/CqrsLab.SqlToCosmosFunction -n CqrsLab.SqlToCosmosFunction
if [ $? -ne 0 ]; then echo "Fallo al crear CqrsLab.SqlToCosmosFunction"; exit 1; fi

dotnet new webapi -o src/CqrsLab.ReaderApi -f net8.0
if [ $? -ne 0 ]; then echo "Fallo al crear CqrsLab.ReaderApi"; exit 1; fi

echo "Proyectos creados exitosamente."
echo "---"

# --- Paso 5: Añadir Proyectos a la Solución ---
echo "Añadiendo proyectos a la solución..."
dotnet sln add src/CqrsLab.Shared/CqrsLab.Shared.csproj
dotnet sln add src/CqrsLab.SqlToCosmosFunction/CqrsLab.SqlToCosmosFunction.csproj
dotnet sln add src/CqrsLab.ReaderApi/CqrsLab.ReaderApi.csproj
echo "---"

# --- Paso 6: Añadir Referencias entre Proyectos ---
echo "Añadiendo referencias de proyectos..."
dotnet add src/CqrsLab.SqlToCosmosFunction/CqrsLab.SqlToCosmosFunction.csproj reference src/CqrsLab.Shared/CqrsLab.Shared.csproj
dotnet add src/CqrsLab.ReaderApi/CqrsLab.ReaderApi.csproj reference src/CqrsLab.Shared/CqrsLab.Shared.csproj
echo "---"

echo "¡Estructura del proyecto creada exitosamente!"