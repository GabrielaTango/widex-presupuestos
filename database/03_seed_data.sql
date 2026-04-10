USE WidexPresupuestos;
GO

-- Usuario admin por defecto
-- Password: Admin123! (el hash se genera desde la app al iniciar)
IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Usuario = 'admin')
BEGIN
    INSERT INTO Usuarios (Nombre, Mail, Usuario, Password, Activo)
    VALUES ('Administrador', 'admin@widex.com', 'admin', 'SEED_FROM_APP', 1);
END
GO
