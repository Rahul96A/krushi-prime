IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(400) NOT NULL,
        [Mobile] nvarchar(400) NULL,
        [Village] nvarchar(400) NULL,
        [Address] nvarchar(400) NULL,
        [Gstin] nvarchar(400) NULL,
        [OpeningBalance] decimal(18,3) NOT NULL,
        [Balance] decimal(18,3) NOT NULL,
        [CreditLimit] decimal(18,3) NOT NULL,
        [LandAcres] decimal(18,3) NULL,
        [Crops] nvarchar(400) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        [TenantId] int NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [MessageLogs] (
        [Id] int NOT NULL IDENTITY,
        [Channel] nvarchar(400) NOT NULL,
        [To] nvarchar(400) NOT NULL,
        [Purpose] nvarchar(400) NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [Status] nvarchar(400) NOT NULL,
        [ProviderMessageId] nvarchar(400) NULL,
        [Error] nvarchar(400) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        [TenantId] int NOT NULL,
        CONSTRAINT [PK_MessageLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [PartyType] int NOT NULL,
        [PartyId] int NOT NULL,
        [Date] datetime2 NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [Mode] int NOT NULL,
        [Reference] nvarchar(400) NULL,
        [Note] nvarchar(400) NULL,
        [SalesInvoiceId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        [TenantId] int NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(400) NOT NULL,
        [LocalName] nvarchar(400) NULL,
        [Category] int NOT NULL,
        [Company] nvarchar(400) NULL,
        [HsnCode] nvarchar(400) NULL,
        [GstRate] decimal(18,3) NOT NULL,
        [Unit] nvarchar(400) NOT NULL,
        [PackSize] nvarchar(400) NULL,
        [Barcode] nvarchar(400) NULL,
        [SalePrice] decimal(18,3) NOT NULL,
        [Mrp] decimal(18,3) NOT NULL,
        [PurchasePrice] decimal(18,3) NOT NULL,
        [ReorderLevel] decimal(18,3) NOT NULL,
        [TrackExpiry] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        [TenantId] int NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [StockMovements] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [BatchId] int NULL,
        [Date] datetime2 NOT NULL,
        [Type] int NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Reference] nvarchar(400) NULL,
        [Note] nvarchar(400) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        [TenantId] int NOT NULL,
        CONSTRAINT [PK_StockMovements] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(400) NOT NULL,
        [Mobile] nvarchar(400) NULL,
        [Gstin] nvarchar(400) NULL,
        [Address] nvarchar(400) NULL,
        [Balance] decimal(18,3) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        [TenantId] int NOT NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] int NOT NULL IDENTITY,
        [ShopName] nvarchar(400) NOT NULL,
        [OwnerName] nvarchar(400) NOT NULL,
        [Mobile] nvarchar(400) NOT NULL,
        [Email] nvarchar(400) NULL,
        [AddressLine] nvarchar(400) NULL,
        [Village] nvarchar(400) NULL,
        [Taluka] nvarchar(400) NULL,
        [District] nvarchar(400) NULL,
        [State] nvarchar(400) NOT NULL,
        [StateCode] nvarchar(400) NOT NULL,
        [Pincode] nvarchar(400) NULL,
        [Gstin] nvarchar(400) NULL,
        [IsGstRegistered] bit NOT NULL,
        [FertilizerLicenseNo] nvarchar(400) NULL,
        [PesticideLicenseNo] nvarchar(400) NULL,
        [SeedLicenseNo] nvarchar(400) NULL,
        [UpiId] nvarchar(400) NULL,
        [InvoicePrefix] nvarchar(400) NOT NULL,
        [NextInvoiceNo] int NOT NULL,
        [InvoiceFooter] nvarchar(400) NULL,
        [PricesIncludeTax] bit NOT NULL,
        [AllowNegativeStock] bit NOT NULL,
        [Plan] int NOT NULL,
        [SubscriptionStart] datetime2 NOT NULL,
        [SubscriptionEnd] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [SalesInvoices] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceNo] nvarchar(400) NOT NULL,
        [InvoiceDate] datetime2 NOT NULL,
        [CustomerId] int NULL,
        [CustomerName] nvarchar(400) NOT NULL,
        [CustomerMobile] nvarchar(400) NULL,
        [CustomerVillage] nvarchar(400) NULL,
        [CustomerGstin] nvarchar(400) NULL,
        [IsGst] bit NOT NULL,
        [IsInterState] bit NOT NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [Discount] decimal(18,3) NOT NULL,
        [CgstAmount] decimal(18,3) NOT NULL,
        [SgstAmount] decimal(18,3) NOT NULL,
        [IgstAmount] decimal(18,3) NOT NULL,
        [RoundOff] decimal(18,3) NOT NULL,
        [GrandTotal] decimal(18,3) NOT NULL,
        [PaidAmount] decimal(18,3) NOT NULL,
        [BalanceAmount] decimal(18,3) NOT NULL,
        [PaymentMode] int NOT NULL,
        [Status] int NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CancelledAt] datetime2 NULL,
        [CancelReason] nvarchar(400) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        [TenantId] int NOT NULL,
        CONSTRAINT [PK_SalesInvoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesInvoices_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductBatches] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [BatchNo] nvarchar(400) NOT NULL,
        [MfgDate] datetime2 NULL,
        [ExpiryDate] datetime2 NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [PurchasePrice] decimal(18,3) NOT NULL,
        [SalePrice] decimal(18,3) NOT NULL,
        [Mrp] decimal(18,3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        [TenantId] int NOT NULL,
        CONSTRAINT [PK_ProductBatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductBatches_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [PurchaseInvoices] (
        [Id] int NOT NULL IDENTITY,
        [SupplierId] int NOT NULL,
        [BillNo] nvarchar(400) NOT NULL,
        [BillDate] datetime2 NOT NULL,
        [IsInterState] bit NOT NULL,
        [SubTotal] decimal(18,3) NOT NULL,
        [TaxAmount] decimal(18,3) NOT NULL,
        [OtherCharges] decimal(18,3) NOT NULL,
        [GrandTotal] decimal(18,3) NOT NULL,
        [PaidAmount] decimal(18,3) NOT NULL,
        [PaymentMode] int NOT NULL,
        [Notes] nvarchar(400) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        [TenantId] int NOT NULL,
        CONSTRAINT [PK_PurchaseInvoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseInvoices_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NULL,
        [FullName] nvarchar(400) NOT NULL,
        [Username] nvarchar(400) NOT NULL,
        [PasswordHash] nvarchar(400) NOT NULL,
        [Role] int NOT NULL,
        [IsActive] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [SalesInvoiceItems] (
        [Id] int NOT NULL IDENTITY,
        [SalesInvoiceId] int NOT NULL,
        [ProductId] int NOT NULL,
        [BatchId] int NULL,
        [ProductName] nvarchar(400) NOT NULL,
        [HsnCode] nvarchar(400) NULL,
        [BatchNo] nvarchar(400) NULL,
        [ExpiryDate] datetime2 NULL,
        [Unit] nvarchar(400) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Rate] decimal(18,3) NOT NULL,
        [DiscountAmount] decimal(18,3) NOT NULL,
        [TaxableValue] decimal(18,3) NOT NULL,
        [GstRate] decimal(18,3) NOT NULL,
        [Cgst] decimal(18,3) NOT NULL,
        [Sgst] decimal(18,3) NOT NULL,
        [Igst] decimal(18,3) NOT NULL,
        [Total] decimal(18,3) NOT NULL,
        [CostPrice] decimal(18,3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        CONSTRAINT [PK_SalesInvoiceItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesInvoiceItems_SalesInvoices_SalesInvoiceId] FOREIGN KEY ([SalesInvoiceId]) REFERENCES [SalesInvoices] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE TABLE [PurchaseInvoiceItems] (
        [Id] int NOT NULL IDENTITY,
        [PurchaseInvoiceId] int NOT NULL,
        [ProductId] int NOT NULL,
        [BatchId] int NOT NULL,
        [ProductName] nvarchar(400) NOT NULL,
        [BatchNo] nvarchar(400) NOT NULL,
        [MfgDate] datetime2 NULL,
        [ExpiryDate] datetime2 NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [FreeQuantity] decimal(18,3) NOT NULL,
        [Rate] decimal(18,3) NOT NULL,
        [GstRate] decimal(18,3) NOT NULL,
        [TaxAmount] decimal(18,3) NOT NULL,
        [Total] decimal(18,3) NOT NULL,
        [SalePrice] decimal(18,3) NOT NULL,
        [Mrp] decimal(18,3) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        CONSTRAINT [PK_PurchaseInvoiceItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseInvoiceItems_PurchaseInvoices_PurchaseInvoiceId] FOREIGN KEY ([PurchaseInvoiceId]) REFERENCES [PurchaseInvoices] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_TenantId_Mobile] ON [Customers] ([TenantId], [Mobile]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_TenantId_Name] ON [Customers] ([TenantId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_TenantId_PartyType_PartyId] ON [Payments] ([TenantId], [PartyType], [PartyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductBatches_ProductId] ON [ProductBatches] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductBatches_TenantId_ProductId_BatchNo] ON [ProductBatches] ([TenantId], [ProductId], [BatchNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_TenantId_Barcode] ON [Products] ([TenantId], [Barcode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_TenantId_Name] ON [Products] ([TenantId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseInvoiceItems_PurchaseInvoiceId] ON [PurchaseInvoiceItems] ([PurchaseInvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseInvoices_SupplierId] ON [PurchaseInvoices] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseInvoices_TenantId_BillDate] ON [PurchaseInvoices] ([TenantId], [BillDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SalesInvoiceItems_SalesInvoiceId] ON [SalesInvoiceItems] ([SalesInvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SalesInvoices_CustomerId] ON [SalesInvoices] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SalesInvoices_TenantId_InvoiceDate] ON [SalesInvoices] ([TenantId], [InvoiceDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SalesInvoices_TenantId_InvoiceNo] ON [SalesInvoices] ([TenantId], [InvoiceNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockMovements_TenantId_ProductId_Date] ON [StockMovements] ([TenantId], [ProductId], [Date]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Suppliers_TenantId_Name] ON [Suppliers] ([TenantId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tenants_Mobile] ON [Tenants] ([Mobile]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119125012_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261119125012_InitialCreate', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    ALTER TABLE [Tenants] ADD [CashfreeAppId] nvarchar(400) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    ALTER TABLE [Tenants] ADD [CashfreeSecretEnc] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    ALTER TABLE [Tenants] ADD [NttMerchId] nvarchar(400) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    ALTER TABLE [Tenants] ADD [NttPasswordEnc] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    ALTER TABLE [Tenants] ADD [NttProductId] nvarchar(400) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    ALTER TABLE [Tenants] ADD [NttRequestKeyEnc] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    ALTER TABLE [Tenants] ADD [NttResponseKeyEnc] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    ALTER TABLE [Tenants] ADD [PaymentLiveMode] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    ALTER TABLE [Tenants] ADD [PaymentProvider] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    CREATE TABLE [PaymentLinks] (
        [Id] int NOT NULL IDENTITY,
        [PublicId] nvarchar(400) NOT NULL,
        [Purpose] int NOT NULL,
        [Provider] int NOT NULL,
        [LiveMode] bit NOT NULL,
        [CustomerId] int NULL,
        [SalesInvoiceId] int NULL,
        [Plan] int NULL,
        [PlanMonths] int NULL,
        [PayerName] nvarchar(400) NOT NULL,
        [PayerMobile] nvarchar(400) NULL,
        [Description] nvarchar(400) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [AmountPaid] decimal(18,3) NOT NULL,
        [Status] int NOT NULL,
        [ProviderRef] nvarchar(400) NOT NULL,
        [ProviderUrl] nvarchar(1000) NULL,
        [ProviderToken] nvarchar(400) NULL,
        [ProviderTxnId] nvarchar(400) NULL,
        [PaidAt] datetime2 NULL,
        [ExpiresAt] datetime2 NULL,
        [RecordedPaymentId] int NULL,
        [LastMessage] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(400) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(400) NULL,
        [TenantId] int NOT NULL,
        CONSTRAINT [PK_PaymentLinks] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    CREATE INDEX [IX_PaymentLinks_ProviderRef] ON [PaymentLinks] ([ProviderRef]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentLinks_PublicId] ON [PaymentLinks] ([PublicId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    CREATE INDEX [IX_PaymentLinks_TenantId_Status] ON [PaymentLinks] ([TenantId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123062823_OnlinePayments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261123062823_OnlinePayments', N'8.0.11');
END;
GO

COMMIT;
GO

