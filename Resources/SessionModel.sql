-- Crear tabla de sesiones persistentes
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SessionData')
BEGIN
    CREATE TABLE [dbo].[SessionData] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [KeyName] NVARCHAR(255) NOT NULL,
        [Value] NVARCHAR(MAX) NULL,
        [idetify] NVARCHAR(255) NOT NULL,
        [created] DATETIME NOT NULL DEFAULT GETUTCDATE(),
        [ExpireTime] DATETIME NOT NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0
    );
    
    -- Índices para mejorar performance
    CREATE NONCLUSTERED INDEX [IX_SessionData_KeyName_Idetify] 
    ON [dbo].[SessionData] ([KeyName], [idetify])
    WHERE [IsDeleted] = 0;
    
    CREATE NONCLUSTERED INDEX [IX_SessionData_Idetify] 
    ON [dbo].[SessionData] ([idetify])
    WHERE [IsDeleted] = 0;
    
    CREATE NONCLUSTERED INDEX [IX_SessionData_ExpireTime] 
    ON [dbo].[SessionData] ([ExpireTime])
    WHERE [IsDeleted] = 0;
END