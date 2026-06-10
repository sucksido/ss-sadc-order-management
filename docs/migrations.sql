-- =============================================================================
-- SADC OMS — idempotent migration script (SQL Server)
--
-- Equivalent to the output of:
--   dotnet ef migrations script --idempotent -o migrations.sql
--     --project src/SadcOms.Infrastructure --startup-project src/SadcOms.Api
--
-- Applies the three migrations in order, guarding each with the __EFMigrationsHistory
-- table so the script can be run repeatedly and against partially-migrated databases.
-- =============================================================================

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

-- ----------------------------------------------------------------------------
-- 20250601090000_InitialCreate
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250601090000_InitialCreate')
BEGIN
    CREATE TABLE [Customers] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Email] nvarchar(320) NOT NULL,
        [CountryCode] nchar(2) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id])
    );

    CREATE TABLE [Orders] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CurrencyCode] nchar(3) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Orders_Customers_CustomerId] FOREIGN KEY ([CustomerId])
            REFERENCES [Customers] ([Id]) ON DELETE NO ACTION
    );

    CREATE TABLE [OrderLineItems] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [ProductSku] nvarchar(64) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderLineItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderLineItems_Orders_OrderId] FOREIGN KEY ([OrderId])
            REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_Customers_Email] ON [Customers] ([Email]);
    CREATE INDEX [IX_Customers_Name] ON [Customers] ([Name]);
    CREATE INDEX [IX_Orders_CustomerId_Status_CreatedAt] ON [Orders] ([CustomerId], [Status], [CreatedAt]);
    CREATE INDEX [IX_Orders_Status_CreatedAt] ON [Orders] ([Status], [CreatedAt]);
    CREATE INDEX [IX_OrderLineItems_OrderId] ON [OrderLineItems] ([OrderId]);

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250601090000_InitialCreate', N'8.0.6');
END;
GO

-- ----------------------------------------------------------------------------
-- 20250601090500_AddOrderRowVersion
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250601090500_AddOrderRowVersion')
BEGIN
    ALTER TABLE [Orders] ADD [RowVersion] rowversion NOT NULL;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250601090500_AddOrderRowVersion', N'8.0.6');
END;
GO

-- ----------------------------------------------------------------------------
-- 20250601091000_AddOutbox
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250601091000_AddOutbox')
BEGIN
    CREATE TABLE [OutboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [Type] nvarchar(200) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [CorrelationId] nvarchar(100) NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        [ProcessedAt] datetimeoffset NULL,
        [Attempts] int NOT NULL,
        [LastError] nvarchar(2000) NULL,
        CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id])
    );

    CREATE TABLE [IdempotencyRecords] (
        [Key] nvarchar(100) NOT NULL,
        [RequestTarget] nvarchar(200) NOT NULL,
        [StatusCode] int NOT NULL,
        [ResponseBody] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_IdempotencyRecords] PRIMARY KEY ([Key])
    );

    CREATE INDEX [IX_OutboxMessages_Unprocessed] ON [OutboxMessages] ([ProcessedAt], [OccurredAt])
        WHERE [ProcessedAt] IS NULL;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250601091000_AddOutbox', N'8.0.6');
END;
GO
