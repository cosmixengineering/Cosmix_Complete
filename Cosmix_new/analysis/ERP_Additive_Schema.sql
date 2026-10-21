BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [Accounts] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [ParentId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsSystem] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Description] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Accounts_Accounts_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [BillsOfMaterial] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [OutputQuantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Description] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_BillsOfMaterial] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [Boqs] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [BoqNumber] nvarchar(50) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [Currency] nvarchar(3) NOT NULL DEFAULT N'USD',
        [Subtotal] decimal(18,2) NOT NULL DEFAULT 0.0,
        [MarkupPercent] decimal(9,4) NOT NULL DEFAULT 0.0,
        [MarkupAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
        [TotalAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
        [ValidUntil] datetimeoffset NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_Boqs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [CommercialExceptionRecords] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [DocumentType] nvarchar(50) NOT NULL,
        [Reason] nvarchar(4000) NOT NULL,
        [Timestamp] datetimeoffset NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [TenantId] nvarchar(64) NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_CommercialExceptionRecords] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [CustomerComplaints] (
        [Id] uniqueidentifier NOT NULL,
        [ComplaintNumber] nvarchar(50) NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [CustomerId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [EquipmentDetails] nvarchar(1000) NULL,
        [SiteLocation] nvarchar(500) NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [AssignedTechnicianId] uniqueidentifier NULL,
        [ResolutionNotes] nvarchar(4000) NULL,
        [ResolvedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_CustomerComplaints] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [CustomerPurchaseOrders] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerPoNumber] nvarchar(50) NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [BoqId] uniqueidentifier NULL,
        [PoDate] datetimeoffset NOT NULL,
        [PoAmount] decimal(18,4) NOT NULL,
        [Currency] nvarchar(10) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [DocumentUrl] nvarchar(1000) NULL,
        [DeliveryTerms] nvarchar(200) NULL,
        [PaymentTerms] nvarchar(200) NULL,
        [ScopeOfWork] nvarchar(4000) NULL,
        [DiscrepancyNotes] nvarchar(4000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_CustomerPurchaseOrders] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [DeliveryChallans] (
        [Id] uniqueidentifier NOT NULL,
        [ChallanNumber] nvarchar(50) NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [DispatchDate] datetimeoffset NOT NULL,
        [DeliveredDate] datetimeoffset NULL,
        [Status] nvarchar(30) NOT NULL,
        [SiteAddress] nvarchar(500) NULL,
        [TransporterName] nvarchar(150) NULL,
        [VehicleNumber] nvarchar(50) NULL,
        [DriverName] nvarchar(100) NULL,
        [DriverPhone] nvarchar(50) NULL,
        [CustomerOtp] nvarchar(10) NULL,
        [ProofOfDeliveryUrl] nvarchar(1000) NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_DeliveryChallans] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [FieldJobs] (
        [Id] uniqueidentifier NOT NULL,
        [JobNumber] nvarchar(50) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [CustomerId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [TechnicianId] uniqueidentifier NULL,
        [EquipmentDetails] nvarchar(1000) NULL,
        [Type] nvarchar(30) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [SiteAddress] nvarchar(500) NULL,
        [SiteLatitude] float NULL,
        [SiteLongitude] float NULL,
        [ScheduledDate] datetimeoffset NULL,
        [StartedAt] datetimeoffset NULL,
        [CompletedAt] datetimeoffset NULL,
        [CustomerOtp] nvarchar(10) NULL,
        [MaterialUsage] nvarchar(2000) NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_FieldJobs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [GoodsReceiptNotes] (
        [Id] uniqueidentifier NOT NULL,
        [GrnNumber] nvarchar(50) NOT NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [PurchaseOrderId] uniqueidentifier NULL,
        [StoreLocation] nvarchar(200) NULL,
        [ReceivedDate] datetimeoffset NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [VehicleNumber] nvarchar(50) NULL,
        [DeliveryChallanNumber] nvarchar(50) NULL,
        [InspectedBy] nvarchar(100) NULL,
        [Remarks] nvarchar(2000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_GoodsReceiptNotes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [HelpdeskTickets] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [AssignedToId] uniqueidentifier NULL,
        [ClosedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_HelpdeskTickets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [IntakeMessages] (
        [Id] uniqueidentifier NOT NULL,
        [MessageNumber] nvarchar(50) NOT NULL,
        [Channel] nvarchar(30) NOT NULL,
        [SenderName] nvarchar(150) NOT NULL,
        [SenderEmail] nvarchar(150) NULL,
        [SenderPhone] nvarchar(50) NULL,
        [Subject] nvarchar(300) NOT NULL,
        [RawBody] nvarchar(4000) NOT NULL,
        [ReceivedAt] datetimeoffset NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [AssignedDepartmentId] uniqueidentifier NULL,
        [AssignedUserId] uniqueidentifier NULL,
        [ProcessingNotes] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_IntakeMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [JournalEntries] (
        [Id] uniqueidentifier NOT NULL,
        [EntryNumber] nvarchar(30) NOT NULL,
        [EntryDate] date NOT NULL,
        [Reference] nvarchar(100) NULL,
        [Description] nvarchar(2000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [SourceType] nvarchar(50) NULL,
        [SourceId] uniqueidentifier NULL,
        [PostedByUserId] nvarchar(450) NULL,
        [PostedAt] datetimeoffset NULL,
        [VoidedByUserId] nvarchar(450) NULL,
        [VoidedAt] datetimeoffset NULL,
        [VoidReason] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_JournalEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [PaymentRecords] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentNumber] nvarchar(50) NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [SalesInvoiceId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [Method] nvarchar(30) NOT NULL,
        [TransactionReference] nvarchar(100) NULL,
        [PaymentDate] datetimeoffset NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_PaymentRecords] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [ProductionOrders] (
        [Id] uniqueidentifier NOT NULL,
        [OrderNumber] nvarchar(50) NOT NULL,
        [BillOfMaterialId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [PlannedQuantity] decimal(18,4) NOT NULL,
        [ProducedQuantity] decimal(18,4) NOT NULL,
        [ScrapQuantity] decimal(18,4) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [PlannedStartDate] date NULL,
        [PlannedEndDate] date NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [CompletedByUserId] uniqueidentifier NULL,
        [CompletedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_ProductionOrders] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [PurchaseOrders] (
        [Id] uniqueidentifier NOT NULL,
        [PoNumber] nvarchar(50) NOT NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [Status] nvarchar(30) NOT NULL,
        [OrderDate] datetimeoffset NOT NULL,
        [ExpectedDeliveryDate] datetimeoffset NULL,
        [Currency] nvarchar(10) NOT NULL,
        [Subtotal] decimal(18,4) NOT NULL,
        [TaxAmount] decimal(18,4) NOT NULL,
        [TotalAmount] decimal(18,4) NOT NULL,
        [PaymentTerms] nvarchar(200) NULL,
        [ShippingTerms] nvarchar(200) NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [QaInspections] (
        [Id] uniqueidentifier NOT NULL,
        [InspectionNumber] nvarchar(50) NOT NULL,
        [Stage] nvarchar(30) NOT NULL,
        [ReferenceEntityId] uniqueidentifier NULL,
        [ReferenceEntityType] nvarchar(50) NULL,
        [Title] nvarchar(200) NOT NULL,
        [ChecklistSummary] nvarchar(2000) NULL,
        [Result] nvarchar(30) NOT NULL,
        [InspectedBy] nvarchar(100) NULL,
        [InspectedAt] datetimeoffset NOT NULL,
        [DefectNotes] nvarchar(4000) NULL,
        [CorrectiveAction] nvarchar(4000) NULL,
        [EvidenceDocumentUrl] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_QaInspections] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [SalesInvoices] (
        [Id] uniqueidentifier NOT NULL,
        [InvoiceNumber] nvarchar(50) NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [IssueDate] datetimeoffset NOT NULL,
        [DueDate] datetimeoffset NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [Currency] nvarchar(10) NOT NULL,
        [Subtotal] decimal(18,4) NOT NULL,
        [TaxAmount] decimal(18,4) NOT NULL,
        [TotalAmount] decimal(18,4) NOT NULL,
        [PaidAmount] decimal(18,4) NOT NULL,
        [PaymentTerms] nvarchar(200) NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_SalesInvoices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [StockIndents] (
        [Id] uniqueidentifier NOT NULL,
        [IndentNumber] nvarchar(50) NOT NULL,
        [RequestedByUserId] uniqueidentifier NULL,
        [NeededByDate] date NULL,
        [Department] nvarchar(200) NULL,
        [Notes] nvarchar(2000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [ApprovedByUserId] nvarchar(450) NULL,
        [ApprovedAt] datetimeoffset NULL,
        [RejectReason] nvarchar(1000) NULL,
        [ProjectId] nvarchar(100) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_StockIndents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [StockTransfers] (
        [Id] uniqueidentifier NOT NULL,
        [TransferNumber] nvarchar(50) NOT NULL,
        [FromLocation] nvarchar(200) NOT NULL,
        [ToLocation] nvarchar(200) NOT NULL,
        [TransferDate] date NULL,
        [Notes] nvarchar(2000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [ApprovedByUserId] uniqueidentifier NULL,
        [ApprovedAt] datetimeoffset NULL,
        [RejectReason] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_StockTransfers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [Submittals] (
        [Id] uniqueidentifier NOT NULL,
        [SubmittalNumber] nvarchar(50) NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [BoqId] uniqueidentifier NULL,
        [Title] nvarchar(200) NOT NULL,
        [SpecificationSection] nvarchar(100) NULL,
        [RevisionNumber] int NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [DocumentUrl] nvarchar(1000) NULL,
        [SubmittedAt] datetimeoffset NOT NULL,
        [ReviewedAt] datetimeoffset NULL,
        [ReviewedBy] nvarchar(100) NULL,
        [ReviewComments] nvarchar(2000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_Submittals] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [ContactPerson] nvarchar(500) NULL,
        [Email] nvarchar(150) NULL,
        [Phone] nvarchar(50) NULL,
        [TaxId] nvarchar(50) NULL,
        [Address] nvarchar(500) NULL,
        [PaymentTerms] nvarchar(200) NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [UnitConversions] (
        [Id] uniqueidentifier NOT NULL,
        [FromUnit] nvarchar(50) NOT NULL,
        [ToUnit] nvarchar(50) NOT NULL,
        [ConversionFactor] decimal(18,6) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        [TenantId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_UnitConversions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [BillOfMaterialItems] (
        [Id] uniqueidentifier NOT NULL,
        [BillOfMaterialId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        [WastePercent] decimal(5,2) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        CONSTRAINT [PK_BillOfMaterialItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BillOfMaterialItems_BillsOfMaterial_BillOfMaterialId] FOREIGN KEY ([BillOfMaterialId]) REFERENCES [BillsOfMaterial] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [BoqItems] (
        [Id] uniqueidentifier NOT NULL,
        [BoqId] uniqueidentifier NOT NULL,
        [Section] nvarchar(100) NOT NULL,
        [ItemCode] nvarchar(50) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        [UnitRate] decimal(18,2) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [TenantId] nvarchar(64) NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_BoqItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BoqItems_Boqs_BoqId] FOREIGN KEY ([BoqId]) REFERENCES [Boqs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [BoqRevisions] (
        [Id] uniqueidentifier NOT NULL,
        [BoqId] uniqueidentifier NOT NULL,
        [RevisionNumber] int NOT NULL,
        [Reason] nvarchar(500) NULL,
        [PreviousTotal] decimal(18,2) NOT NULL,
        [NewTotal] decimal(18,2) NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        CONSTRAINT [PK_BoqRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BoqRevisions_Boqs_BoqId] FOREIGN KEY ([BoqId]) REFERENCES [Boqs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [DeliveryChallanItems] (
        [Id] uniqueidentifier NOT NULL,
        [DeliveryChallanId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ItemDescription] nvarchar(500) NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [TenantId] nvarchar(64) NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_DeliveryChallanItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeliveryChallanItems_DeliveryChallans_DeliveryChallanId] FOREIGN KEY ([DeliveryChallanId]) REFERENCES [DeliveryChallans] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [GoodsReceiptNoteItems] (
        [Id] uniqueidentifier NOT NULL,
        [GoodsReceiptNoteId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ItemDescription] nvarchar(500) NOT NULL,
        [ReceivedQuantity] decimal(18,4) NOT NULL,
        [AcceptedQuantity] decimal(18,4) NOT NULL,
        [RejectedQuantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        [RejectionReason] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [TenantId] nvarchar(64) NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_GoodsReceiptNoteItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoodsReceiptNoteItems_GoodsReceiptNotes_GoodsReceiptNoteId] FOREIGN KEY ([GoodsReceiptNoteId]) REFERENCES [GoodsReceiptNotes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [HelpdeskTicketComments] (
        [Id] uniqueidentifier NOT NULL,
        [TicketId] uniqueidentifier NOT NULL,
        [Text] nvarchar(4000) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [TenantId] nvarchar(64) NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_HelpdeskTicketComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HelpdeskTicketComments_HelpdeskTickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [HelpdeskTickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [JournalLines] (
        [Id] uniqueidentifier NOT NULL,
        [JournalEntryId] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [Debit] decimal(18,2) NOT NULL DEFAULT 0.0,
        [Credit] decimal(18,2) NOT NULL DEFAULT 0.0,
        [Memo] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        CONSTRAINT [PK_JournalLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JournalLines_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [ProductionOrderItems] (
        [Id] uniqueidentifier NOT NULL,
        [ProductionOrderId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [RequiredQuantity] decimal(18,4) NOT NULL,
        [IssuedQuantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ProductionOrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductionOrderItems_ProductionOrders_ProductionOrderId] FOREIGN KEY ([ProductionOrderId]) REFERENCES [ProductionOrders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [PurchaseOrderItems] (
        [Id] uniqueidentifier NOT NULL,
        [PurchaseOrderId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ItemDescription] nvarchar(500) NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [TotalPrice] decimal(18,4) NOT NULL,
        [ReceivedQuantity] decimal(18,4) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [TenantId] nvarchar(64) NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_PurchaseOrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [SalesInvoiceItems] (
        [Id] uniqueidentifier NOT NULL,
        [SalesInvoiceId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [Subtotal] decimal(18,4) NOT NULL,
        [TaxRate] decimal(18,4) NOT NULL,
        [TaxAmount] decimal(18,4) NOT NULL,
        [TotalAmount] decimal(18,4) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [TenantId] nvarchar(64) NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_SalesInvoiceItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesInvoiceItems_SalesInvoices_SalesInvoiceId] FOREIGN KEY ([SalesInvoiceId]) REFERENCES [SalesInvoices] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [StockIndentItems] (
        [Id] uniqueidentifier NOT NULL,
        [IndentId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(50) NOT NULL,
        [Remarks] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [TenantId] nvarchar(64) NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_StockIndentItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockIndentItems_StockIndents_IndentId] FOREIGN KEY ([IndentId]) REFERENCES [StockIndents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE TABLE [StockTransferItems] (
        [Id] uniqueidentifier NOT NULL,
        [StockTransferId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Unit] nvarchar(50) NOT NULL,
        [Remarks] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [TenantId] nvarchar(64) NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [ModifiedBy] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_StockTransferItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockTransferItems_StockTransfers_StockTransferId] FOREIGN KEY ([StockTransferId]) REFERENCES [StockTransfers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Accounts_IsDeleted] ON [Accounts] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Accounts_ParentId] ON [Accounts] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Accounts_TenantId] ON [Accounts] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Accounts_TenantId_Code] ON [Accounts] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_BillOfMaterialItems_BillOfMaterialId] ON [BillOfMaterialItems] ([BillOfMaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BillOfMaterialItems_BillOfMaterialId_ProductId] ON [BillOfMaterialItems] ([BillOfMaterialId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BillsOfMaterial_Code_TenantId] ON [BillsOfMaterial] ([Code], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_BillsOfMaterial_IsDeleted] ON [BillsOfMaterial] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_BillsOfMaterial_TenantId] ON [BillsOfMaterial] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_BoqItems_BoqId] ON [BoqItems] ([BoqId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_BoqItems_BoqId_SortOrder] ON [BoqItems] ([BoqId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_BoqItems_IsDeleted] ON [BoqItems] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_BoqItems_TenantId] ON [BoqItems] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_BoqRevisions_BoqId] ON [BoqRevisions] ([BoqId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BoqRevisions_BoqId_RevisionNumber] ON [BoqRevisions] ([BoqId], [RevisionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Boqs_IsDeleted] ON [Boqs] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Boqs_TenantId] ON [Boqs] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Boqs_TenantId_BoqNumber] ON [Boqs] ([TenantId], [BoqNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Boqs_TenantId_CreatedAt] ON [Boqs] ([TenantId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Boqs_TenantId_CustomerId] ON [Boqs] ([TenantId], [CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Boqs_TenantId_ProjectId] ON [Boqs] ([TenantId], [ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Boqs_TenantId_Status] ON [Boqs] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_CommercialExceptionRecords_IsDeleted] ON [CommercialExceptionRecords] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_CommercialExceptionRecords_TenantId] ON [CommercialExceptionRecords] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerComplaints_ComplaintNumber_TenantId] ON [CustomerComplaints] ([ComplaintNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_CustomerComplaints_CustomerId] ON [CustomerComplaints] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_CustomerComplaints_IsDeleted] ON [CustomerComplaints] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_CustomerComplaints_Status] ON [CustomerComplaints] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_CustomerComplaints_TenantId] ON [CustomerComplaints] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerPurchaseOrders_CustomerPoNumber_TenantId] ON [CustomerPurchaseOrders] ([CustomerPoNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_CustomerPurchaseOrders_IsDeleted] ON [CustomerPurchaseOrders] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_CustomerPurchaseOrders_TenantId] ON [CustomerPurchaseOrders] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_DeliveryChallanItems_DeliveryChallanId] ON [DeliveryChallanItems] ([DeliveryChallanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_DeliveryChallanItems_IsDeleted] ON [DeliveryChallanItems] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_DeliveryChallanItems_TenantId] ON [DeliveryChallanItems] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DeliveryChallans_ChallanNumber_TenantId] ON [DeliveryChallans] ([ChallanNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_DeliveryChallans_IsDeleted] ON [DeliveryChallans] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_DeliveryChallans_TenantId] ON [DeliveryChallans] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_FieldJobs_IsDeleted] ON [FieldJobs] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FieldJobs_JobNumber_TenantId] ON [FieldJobs] ([JobNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_FieldJobs_Status] ON [FieldJobs] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_FieldJobs_TechnicianId] ON [FieldJobs] ([TechnicianId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_FieldJobs_TenantId] ON [FieldJobs] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_GoodsReceiptNoteItems_GoodsReceiptNoteId] ON [GoodsReceiptNoteItems] ([GoodsReceiptNoteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_GoodsReceiptNoteItems_IsDeleted] ON [GoodsReceiptNoteItems] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_GoodsReceiptNoteItems_TenantId] ON [GoodsReceiptNoteItems] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GoodsReceiptNotes_GrnNumber_TenantId] ON [GoodsReceiptNotes] ([GrnNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_GoodsReceiptNotes_IsDeleted] ON [GoodsReceiptNotes] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_GoodsReceiptNotes_TenantId] ON [GoodsReceiptNotes] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_HelpdeskTicketComments_IsDeleted] ON [HelpdeskTicketComments] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_HelpdeskTicketComments_TenantId] ON [HelpdeskTicketComments] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_HelpdeskTicketComments_TicketId] ON [HelpdeskTicketComments] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_HelpdeskTickets_IsDeleted] ON [HelpdeskTickets] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_HelpdeskTickets_TenantId] ON [HelpdeskTickets] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_IntakeMessages_IsDeleted] ON [IntakeMessages] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IntakeMessages_MessageNumber_TenantId] ON [IntakeMessages] ([MessageNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_IntakeMessages_TenantId] ON [IntakeMessages] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_IsDeleted] ON [JournalEntries] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_TenantId] ON [JournalEntries] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_TenantId_EntryDate] ON [JournalEntries] ([TenantId], [EntryDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JournalEntries_TenantId_EntryNumber] ON [JournalEntries] ([TenantId], [EntryNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_TenantId_Status] ON [JournalEntries] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_JournalLines_AccountId] ON [JournalLines] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_JournalLines_JournalEntryId] ON [JournalLines] ([JournalEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_PaymentRecords_IsDeleted] ON [PaymentRecords] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentRecords_PaymentNumber_TenantId] ON [PaymentRecords] ([PaymentNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_PaymentRecords_TenantId] ON [PaymentRecords] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_ProductionOrderItems_ProductionOrderId] ON [ProductionOrderItems] ([ProductionOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductionOrderItems_ProductionOrderId_ProductId] ON [ProductionOrderItems] ([ProductionOrderId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_ProductionOrders_IsDeleted] ON [ProductionOrders] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductionOrders_OrderNumber_TenantId] ON [ProductionOrders] ([OrderNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_ProductionOrders_Status] ON [ProductionOrders] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_ProductionOrders_TenantId] ON [ProductionOrders] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderItems_IsDeleted] ON [PurchaseOrderItems] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderItems_PurchaseOrderId] ON [PurchaseOrderItems] ([PurchaseOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderItems_TenantId] ON [PurchaseOrderItems] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_IsDeleted] ON [PurchaseOrders] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseOrders_PoNumber_TenantId] ON [PurchaseOrders] ([PoNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_TenantId] ON [PurchaseOrders] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QaInspections_InspectionNumber_TenantId] ON [QaInspections] ([InspectionNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_QaInspections_IsDeleted] ON [QaInspections] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_QaInspections_TenantId] ON [QaInspections] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_SalesInvoiceItems_IsDeleted] ON [SalesInvoiceItems] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_SalesInvoiceItems_SalesInvoiceId] ON [SalesInvoiceItems] ([SalesInvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_SalesInvoiceItems_TenantId] ON [SalesInvoiceItems] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SalesInvoices_InvoiceNumber_TenantId] ON [SalesInvoices] ([InvoiceNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_SalesInvoices_IsDeleted] ON [SalesInvoices] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_SalesInvoices_TenantId] ON [SalesInvoices] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockIndentItems_IndentId] ON [StockIndentItems] ([IndentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockIndentItems_IsDeleted] ON [StockIndentItems] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockIndentItems_TenantId] ON [StockIndentItems] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockIndents_IndentNumber_TenantId] ON [StockIndents] ([IndentNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockIndents_IsDeleted] ON [StockIndents] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockIndents_TenantId] ON [StockIndents] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockIndents_TenantId_Status_CreatedAt] ON [StockIndents] ([TenantId], [Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockTransferItems_IsDeleted] ON [StockTransferItems] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockTransferItems_StockTransferId] ON [StockTransferItems] ([StockTransferId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockTransferItems_TenantId] ON [StockTransferItems] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_IsDeleted] ON [StockTransfers] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_TenantId] ON [StockTransfers] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_TenantId_FromLocation_ToLocation] ON [StockTransfers] ([TenantId], [FromLocation], [ToLocation]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_TenantId_Status_CreatedAt] ON [StockTransfers] ([TenantId], [Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockTransfers_TransferNumber_TenantId] ON [StockTransfers] ([TransferNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Submittals_IsDeleted] ON [Submittals] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Submittals_SubmittalNumber_TenantId] ON [Submittals] ([SubmittalNumber], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Submittals_TenantId] ON [Submittals] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Suppliers_Code_TenantId] ON [Suppliers] ([Code], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Suppliers_IsDeleted] ON [Suppliers] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_Suppliers_TenantId] ON [Suppliers] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_UnitConversions_IsDeleted] ON [UnitConversions] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    CREATE INDEX [IX_UnitConversions_TenantId] ON [UnitConversions] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012095756_AddErpModuleSchemas'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012095756_AddErpModuleSchemas', N'10.0.5');
END;

COMMIT;
GO

