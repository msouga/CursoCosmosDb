-- Verificar si la tabla ya existe para evitar errores al ejecutar el script varias veces.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Clientes]') AND type in (N'U'))
BEGIN
    CREATE TABLE Clientes (
        ClienteId INT PRIMARY KEY IDENTITY,
        Nombre NVARCHAR(100) NOT NULL,
        RUC NVARCHAR(20),
        Ciudad NVARCHAR(50),
        Pais NVARCHAR(50),
        FechaModificacion DATETIME2 DEFAULT GETUTCDATE()
    );

    PRINT 'Tabla Clientes creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La tabla Clientes ya existe.';
END
GO
