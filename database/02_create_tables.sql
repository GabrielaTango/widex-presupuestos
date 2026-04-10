USE WidexPresupuestos;
GO

-- Tabla de Usuarios
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Usuarios')
BEGIN
    CREATE TABLE Usuarios (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Nombre NVARCHAR(100) NOT NULL,
        Mail NVARCHAR(150) NOT NULL,
        Usuario NVARCHAR(50) NOT NULL,
        Password NVARCHAR(256) NOT NULL,
        Activo BIT NOT NULL DEFAULT 1,
        FechaCreacion DATETIME2 NOT NULL DEFAULT GETDATE(),
        FechaModificacion DATETIME2 NULL
    );

    CREATE UNIQUE INDEX IX_Usuarios_Mail ON Usuarios(Mail);
    CREATE UNIQUE INDEX IX_Usuarios_Usuario ON Usuarios(Usuario);
END
GO
