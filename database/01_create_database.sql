-- Crear base de datos WidexPresupuestos
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'WidexPresupuestos')
BEGIN
    CREATE DATABASE WidexPresupuestos;
END
GO

USE WidexPresupuestos;
GO
