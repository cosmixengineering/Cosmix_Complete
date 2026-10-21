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
IF SCHEMA_ID(N'hr') IS NULL EXEC(N'CREATE SCHEMA [hr];');

CREATE TABLE [hr].[Departments] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_Departments_Code] ON [hr].[Departments] ([Code]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261005075444_InitialFoundation', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
IF SCHEMA_ID(N'attendance') IS NULL EXEC(N'CREATE SCHEMA [attendance];');

CREATE SEQUENCE [hr].[EmployeeNumbers] START WITH 1 INCREMENT BY 1 NO CYCLE;

CREATE TABLE [hr].[Branches] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [City] nvarchar(100) NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Branches] PRIMARY KEY ([Id])
);

CREATE TABLE [hr].[Designations] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Designations] PRIMARY KEY ([Id])
);

CREATE TABLE [hr].[Employees] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeNumber] bigint NOT NULL DEFAULT (NEXT VALUE FOR [hr].[EmployeeNumbers]),
    [Status] int NOT NULL,
    [FullLegalName] nvarchar(200) NULL,
    [FatherOrSpouseName] nvarchar(200) NULL,
    [IdentityDocumentType] int NULL,
    [IdentityDocumentNumber] nvarchar(50) NULL,
    [NormalizedIdentityDocumentNumber] nvarchar(50) NULL,
    [IdentityDocumentExpiresOn] date NULL,
    [DateOfBirth] date NULL,
    [Gender] nvarchar(40) NULL,
    [MaritalStatus] nvarchar(40) NULL,
    [PersonalPhone] nvarchar(30) NULL,
    [CompanyPhone] nvarchar(30) NULL,
    [SimIssuedOn] date NULL,
    [SimStatus] nvarchar(40) NULL,
    [SimCreditTier] nvarchar(50) NULL,
    [PersonalEmail] nvarchar(320) NULL,
    [OfficialEmail] nvarchar(320) NULL,
    [NormalizedOfficialEmail] nvarchar(320) NULL,
    [JoinedOn] date NULL,
    [ProbationEndsOn] date NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedByUserId] nvarchar(200) NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Employees_IdentityPair] CHECK (([IdentityDocumentNumber] IS NULL AND [NormalizedIdentityDocumentNumber] IS NULL) OR ([IdentityDocumentNumber] IS NOT NULL AND [NormalizedIdentityDocumentNumber] IS NOT NULL)),
    CONSTRAINT [CK_Employees_IdentityType] CHECK ([IdentityDocumentType] IS NULL OR [IdentityDocumentType] IN (1, 2)),
    CONSTRAINT [CK_Employees_Number] CHECK ([EmployeeNumber] > 0),
    CONSTRAINT [CK_Employees_OfficialEmailPair] CHECK (([OfficialEmail] IS NULL AND [NormalizedOfficialEmail] IS NULL) OR ([OfficialEmail] IS NOT NULL AND [NormalizedOfficialEmail] IS NOT NULL)),
    CONSTRAINT [CK_Employees_ProbationDates] CHECK ([JoinedOn] IS NULL OR [ProbationEndsOn] IS NULL OR [ProbationEndsOn] >= [JoinedOn]),
    CONSTRAINT [CK_Employees_Status] CHECK ([Status] BETWEEN 0 AND 6)
);

CREATE TABLE [hr].[EmploymentTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmploymentTypes] PRIMARY KEY ([Id])
);

CREATE TABLE [attendance].[Shifts] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id])
);

CREATE TABLE [hr].[StaffCategories] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_StaffCategories] PRIMARY KEY ([Id])
);

CREATE TABLE [hr].[EmployeeAddresses] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [AddressType] int NOT NULL,
    [AddressLine1] nvarchar(250) NULL,
    [AddressLine2] nvarchar(250) NULL,
    [City] nvarchar(100) NULL,
    [Province] nvarchar(100) NULL,
    [PostalCode] nvarchar(20) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeAddresses] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmployeeAddresses_Type] CHECK ([AddressType] IN (1, 2)),
    CONSTRAINT [FK_EmployeeAddresses_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [DocumentType] int NOT NULL,
    [StorageKey] nvarchar(400) NOT NULL,
    [OriginalFileName] nvarchar(255) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [Sha256] varchar(64) NOT NULL,
    [UploadedByUserId] nvarchar(200) NOT NULL,
    [UploadedAt] datetimeoffset NOT NULL,
    [IssuedOn] date NULL,
    [ExpiresOn] date NULL,
    [ScanStatus] int NOT NULL,
    [VerificationStatus] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmployeeDocuments_Dates] CHECK ([IssuedOn] IS NULL OR [ExpiresOn] IS NULL OR [ExpiresOn] >= [IssuedOn]),
    CONSTRAINT [CK_EmployeeDocuments_ScanStatus] CHECK ([ScanStatus] BETWEEN 0 AND 3),
    CONSTRAINT [CK_EmployeeDocuments_Sha256] CHECK (LEN([Sha256]) = 64 AND [Sha256] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_EmployeeDocuments_Size] CHECK ([SizeBytes] > 0),
    CONSTRAINT [CK_EmployeeDocuments_Type] CHECK ([DocumentType] BETWEEN 1 AND 10),
    CONSTRAINT [CK_EmployeeDocuments_VerificationStatus] CHECK ([VerificationStatus] BETWEEN 0 AND 2),
    CONSTRAINT [FK_EmployeeDocuments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeEmergencyContacts] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NULL,
    [Relationship] nvarchar(100) NULL,
    [Phone] nvarchar(30) NULL,
    [AlternatePhone] nvarchar(30) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeEmergencyContacts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeEmergencyContacts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeStatusChanges] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [PreviousStatus] int NOT NULL,
    [NewStatus] int NOT NULL,
    [EffectiveAt] datetimeoffset NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [ChangedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeStatusChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmployeeStatusChanges_EffectiveUtc] CHECK (DATEPART(TZOFFSET, [EffectiveAt]) = 0),
    CONSTRAINT [CK_EmployeeStatusChanges_Status] CHECK ([PreviousStatus] BETWEEN 0 AND 6 AND [NewStatus] BETWEEN 0 AND 6 AND [PreviousStatus] <> [NewStatus]),
    CONSTRAINT [FK_EmployeeStatusChanges_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[Sites] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [Address] nvarchar(500) NULL,
    [SupervisorEmployeeId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Sites] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Sites_Employees_SupervisorEmployeeId] FOREIGN KEY ([SupervisorEmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ShiftVersions] (
    [Id] uniqueidentifier NOT NULL,
    [ShiftId] uniqueidentifier NOT NULL,
    [VersionNumber] int NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [StartTime] time(7) NOT NULL,
    [EndTime] time(7) NOT NULL,
    [EndDayOffset] int NOT NULL,
    [ScheduledMinutes] int NOT NULL,
    [PaidBreakMinutes] int NOT NULL,
    [UnpaidBreakMinutes] int NOT NULL,
    [ApplicableWeekdays] int NOT NULL,
    CONSTRAINT [PK_ShiftVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ShiftVersions_ApplicableWeekdays] CHECK ([ApplicableWeekdays] BETWEEN 1 AND 127),
    CONSTRAINT [CK_ShiftVersions_Duration] CHECK ((DATEDIFF_BIG(nanosecond, [StartTime], [EndTime]) + CAST([EndDayOffset] AS bigint) * 86400000000000) > 0 AND (DATEDIFF_BIG(nanosecond, [StartTime], [EndTime]) + CAST([EndDayOffset] AS bigint) * 86400000000000) <= 86400000000000),
    CONSTRAINT [CK_ShiftVersions_EndDayOffset] CHECK ([EndDayOffset] IN (0, 1)),
    CONSTRAINT [CK_ShiftVersions_Minutes] CHECK ([ScheduledMinutes] > 0 AND [ScheduledMinutes] <= (DATEDIFF_BIG(nanosecond, [StartTime], [EndTime]) + CAST([EndDayOffset] AS bigint) * 86400000000000) / 60000000000.0 AND [PaidBreakMinutes] >= 0 AND [UnpaidBreakMinutes] >= 0 AND CAST([PaidBreakMinutes] AS bigint) + [UnpaidBreakMinutes] <= (DATEDIFF_BIG(nanosecond, [StartTime], [EndTime]) + CAST([EndDayOffset] AS bigint) * 86400000000000) / 60000000000.0),
    CONSTRAINT [CK_ShiftVersions_VersionNumber] CHECK ([VersionNumber] > 0),
    CONSTRAINT [FK_ShiftVersions_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [attendance].[Shifts] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [DepartmentId] uniqueidentifier NOT NULL,
    [DesignationId] uniqueidentifier NOT NULL,
    [StaffCategoryId] uniqueidentifier NOT NULL,
    [EmploymentTypeId] uniqueidentifier NOT NULL,
    [BranchId] uniqueidentifier NOT NULL,
    [SiteId] uniqueidentifier NULL,
    [ReportingManagerId] uniqueidentifier NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_EmployeeAssignments_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]),
    CONSTRAINT [CK_EmployeeAssignments_Dates] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [CK_EmployeeAssignments_Manager] CHECK ([ReportingManagerId] IS NULL OR [ReportingManagerId] <> [EmployeeId]),
    CONSTRAINT [FK_EmployeeAssignments_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeAssignments_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [hr].[Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeAssignments_Designations_DesignationId] FOREIGN KEY ([DesignationId]) REFERENCES [hr].[Designations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeAssignments_Employees_ReportingManagerId] FOREIGN KEY ([ReportingManagerId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeAssignments_EmploymentTypes_EmploymentTypeId] FOREIGN KEY ([EmploymentTypeId]) REFERENCES [hr].[EmploymentTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeAssignments_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeAssignments_StaffCategories_StaffCategoryId] FOREIGN KEY ([StaffCategoryId]) REFERENCES [hr].[StaffCategories] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[EmployeeShiftAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ShiftId] uniqueidentifier NOT NULL,
    [SiteId] uniqueidentifier NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeShiftAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmployeeShiftAssignments_EffectiveDates] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [FK_EmployeeShiftAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeShiftAssignments_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [attendance].[Shifts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeShiftAssignments_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[SiteGeofenceVersions] (
    [Id] uniqueidentifier NOT NULL,
    [SiteId] uniqueidentifier NOT NULL,
    [VersionNumber] int NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [Latitude] decimal(10,7) NOT NULL,
    [Longitude] decimal(10,7) NOT NULL,
    [RadiusMeters] decimal(10,2) NOT NULL,
    [MaximumAccuracyMeters] decimal(10,2) NOT NULL,
    CONSTRAINT [PK_SiteGeofenceVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_SiteGeofenceVersions_SiteId_Id] UNIQUE ([SiteId], [Id]),
    CONSTRAINT [CK_SiteGeofenceVersions_Coordinates] CHECK ([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180),
    CONSTRAINT [CK_SiteGeofenceVersions_Distances] CHECK ([RadiusMeters] > 0 AND [MaximumAccuracyMeters] > 0),
    CONSTRAINT [CK_SiteGeofenceVersions_VersionNumber] CHECK ([VersionNumber] > 0),
    CONSTRAINT [FK_SiteGeofenceVersions_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[RawPunchEvents] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Action] int NOT NULL,
    [RegisteredDeviceId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [IdempotencyKey] nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [RequestSha256] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ReceivedAtUtc] datetimeoffset(7) NOT NULL,
    [DeviceCapturedAtUtc] datetimeoffset(7) NULL,
    [IsOffline] bit NOT NULL,
    [Latitude] decimal(10,7) NULL,
    [Longitude] decimal(10,7) NULL,
    [AccuracyMeters] decimal(10,2) NULL,
    [SiteId] uniqueidentifier NULL,
    [GeofenceVersionId] uniqueidentifier NULL,
    [EmployeeAssignmentId] uniqueidentifier NULL,
    [ShiftVersionId] uniqueidentifier NULL,
    [VerificationAttemptReference] nvarchar(200) NULL,
    [CorrelationId] nvarchar(100) NULL,
    CONSTRAINT [PK_RawPunchEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_RawPunchEvents_Accuracy] CHECK ([AccuracyMeters] IS NULL OR [AccuracyMeters] >= 0),
    CONSTRAINT [CK_RawPunchEvents_Action] CHECK ([Action] IN (1, 2)),
    CONSTRAINT [CK_RawPunchEvents_Coordinates] CHECK (([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)),
    CONSTRAINT [CK_RawPunchEvents_DeviceCapturedUtc] CHECK ([DeviceCapturedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeviceCapturedAtUtc]) = 0),
    CONSTRAINT [CK_RawPunchEvents_GeofenceSite] CHECK ([GeofenceVersionId] IS NULL OR [SiteId] IS NOT NULL),
    CONSTRAINT [CK_RawPunchEvents_ReceivedUtc] CHECK (DATEPART(TZOFFSET, [ReceivedAtUtc]) = 0),
    CONSTRAINT [CK_RawPunchEvents_RequestSha256] CHECK (LEN([RequestSha256]) = 64 AND [RequestSha256] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [FK_RawPunchEvents_EmployeeAssignments_EmployeeId_EmployeeAssignmentId] FOREIGN KEY ([EmployeeId], [EmployeeAssignmentId]) REFERENCES [hr].[EmployeeAssignments] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RawPunchEvents_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RawPunchEvents_ShiftVersions_ShiftVersionId] FOREIGN KEY ([ShiftVersionId]) REFERENCES [attendance].[ShiftVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RawPunchEvents_SiteGeofenceVersions_SiteId_GeofenceVersionId] FOREIGN KEY ([SiteId], [GeofenceVersionId]) REFERENCES [attendance].[SiteGeofenceVersions] ([SiteId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RawPunchEvents_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_Branches_Code] ON [hr].[Branches] ([Code]);

CREATE UNIQUE INDEX [IX_Designations_Code] ON [hr].[Designations] ([Code]);

CREATE UNIQUE INDEX [IX_EmployeeAddresses_EmployeeId_AddressType] ON [hr].[EmployeeAddresses] ([EmployeeId], [AddressType]);

CREATE INDEX [IX_EmployeeAssignments_BranchId] ON [hr].[EmployeeAssignments] ([BranchId]);

CREATE INDEX [IX_EmployeeAssignments_DepartmentId_EffectiveFrom_EffectiveTo] ON [hr].[EmployeeAssignments] ([DepartmentId], [EffectiveFrom], [EffectiveTo]);

CREATE INDEX [IX_EmployeeAssignments_DesignationId] ON [hr].[EmployeeAssignments] ([DesignationId]);

CREATE UNIQUE INDEX [IX_EmployeeAssignments_EmployeeId_EffectiveFrom] ON [hr].[EmployeeAssignments] ([EmployeeId], [EffectiveFrom]);

CREATE INDEX [IX_EmployeeAssignments_EmploymentTypeId] ON [hr].[EmployeeAssignments] ([EmploymentTypeId]);

CREATE INDEX [IX_EmployeeAssignments_ReportingManagerId_EffectiveFrom_EffectiveTo] ON [hr].[EmployeeAssignments] ([ReportingManagerId], [EffectiveFrom], [EffectiveTo]);

CREATE INDEX [IX_EmployeeAssignments_SiteId_EffectiveFrom_EffectiveTo] ON [hr].[EmployeeAssignments] ([SiteId], [EffectiveFrom], [EffectiveTo]);

CREATE INDEX [IX_EmployeeAssignments_StaffCategoryId] ON [hr].[EmployeeAssignments] ([StaffCategoryId]);

CREATE INDEX [IX_EmployeeDocuments_EmployeeId_DocumentType] ON [hr].[EmployeeDocuments] ([EmployeeId], [DocumentType]);

CREATE INDEX [IX_EmployeeDocuments_ExpiresOn] ON [hr].[EmployeeDocuments] ([ExpiresOn]);

CREATE UNIQUE INDEX [IX_EmployeeDocuments_StorageKey] ON [hr].[EmployeeDocuments] ([StorageKey]);

CREATE UNIQUE INDEX [IX_EmployeeEmergencyContacts_EmployeeId] ON [hr].[EmployeeEmergencyContacts] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_Employees_EmployeeNumber] ON [hr].[Employees] ([EmployeeNumber]);

CREATE UNIQUE INDEX [IX_Employees_IdentityDocumentType_NormalizedIdentityDocumentNumber] ON [hr].[Employees] ([IdentityDocumentType], [NormalizedIdentityDocumentNumber]) WHERE [IdentityDocumentType] IS NOT NULL AND [NormalizedIdentityDocumentNumber] IS NOT NULL;

CREATE UNIQUE INDEX [IX_Employees_NormalizedOfficialEmail] ON [hr].[Employees] ([NormalizedOfficialEmail]) WHERE [NormalizedOfficialEmail] IS NOT NULL;

CREATE INDEX [IX_Employees_Status_FullLegalName_Id] ON [hr].[Employees] ([Status], [FullLegalName], [Id]);

CREATE INDEX [IX_EmployeeShiftAssignments_EmployeeId_EffectiveFrom] ON [attendance].[EmployeeShiftAssignments] ([EmployeeId], [EffectiveFrom]);

CREATE INDEX [IX_EmployeeShiftAssignments_ShiftId] ON [attendance].[EmployeeShiftAssignments] ([ShiftId]);

CREATE INDEX [IX_EmployeeShiftAssignments_SiteId] ON [attendance].[EmployeeShiftAssignments] ([SiteId]);

CREATE INDEX [IX_EmployeeStatusChanges_EmployeeId_EffectiveAt_RecordedAt] ON [hr].[EmployeeStatusChanges] ([EmployeeId], [EffectiveAt], [RecordedAt]);

CREATE UNIQUE INDEX [IX_EmploymentTypes_Code] ON [hr].[EmploymentTypes] ([Code]);

CREATE INDEX [IX_RawPunchEvents_EmployeeId_EmployeeAssignmentId] ON [attendance].[RawPunchEvents] ([EmployeeId], [EmployeeAssignmentId]);

CREATE UNIQUE INDEX [IX_RawPunchEvents_EmployeeId_IdempotencyKey] ON [attendance].[RawPunchEvents] ([EmployeeId], [IdempotencyKey]);

CREATE INDEX [IX_RawPunchEvents_EmployeeId_ReceivedAtUtc] ON [attendance].[RawPunchEvents] ([EmployeeId], [ReceivedAtUtc]);

CREATE INDEX [IX_RawPunchEvents_ShiftVersionId] ON [attendance].[RawPunchEvents] ([ShiftVersionId]);

CREATE INDEX [IX_RawPunchEvents_SiteId_GeofenceVersionId] ON [attendance].[RawPunchEvents] ([SiteId], [GeofenceVersionId]);

CREATE UNIQUE INDEX [IX_Shifts_Code] ON [attendance].[Shifts] ([Code]);

CREATE UNIQUE INDEX [IX_ShiftVersions_ShiftId_EffectiveFrom] ON [attendance].[ShiftVersions] ([ShiftId], [EffectiveFrom]);

CREATE UNIQUE INDEX [IX_ShiftVersions_ShiftId_VersionNumber] ON [attendance].[ShiftVersions] ([ShiftId], [VersionNumber]);

CREATE UNIQUE INDEX [IX_SiteGeofenceVersions_SiteId_EffectiveFrom] ON [attendance].[SiteGeofenceVersions] ([SiteId], [EffectiveFrom]);

CREATE UNIQUE INDEX [IX_SiteGeofenceVersions_SiteId_VersionNumber] ON [attendance].[SiteGeofenceVersions] ([SiteId], [VersionNumber]);

CREATE UNIQUE INDEX [IX_Sites_Code] ON [attendance].[Sites] ([Code]);

CREATE INDEX [IX_Sites_SupervisorEmployeeId] ON [attendance].[Sites] ([SupervisorEmployeeId]);

CREATE UNIQUE INDEX [IX_StaffCategories_Code] ON [hr].[StaffCategories] ([Code]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261005113807_AddInitialHrAttendanceModels', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM [hr].[Employees] WHERE [Status] <> 0)
    THROW 51000, 'Migration stopped: existing non-Draft employees require a reviewed onboarding-history conversion.', 1;

IF EXISTS (SELECT 1 FROM [hr].[EmployeeDocuments] WHERE [ScanStatus] <> 0 OR [VerificationStatus] <> 0)
    THROW 51001, 'Migration stopped: existing processed documents require a reviewed scan/verification-evidence conversion.', 1;

IF SCHEMA_ID(N'auth') IS NULL EXEC(N'CREATE SCHEMA [auth];');

IF SCHEMA_ID(N'workflow') IS NULL EXEC(N'CREATE SCHEMA [workflow];');

IF SCHEMA_ID(N'audit') IS NULL EXEC(N'CREATE SCHEMA [audit];');

IF SCHEMA_ID(N'communication') IS NULL EXEC(N'CREATE SCHEMA [communication];');

IF SCHEMA_ID(N'jobs') IS NULL EXEC(N'CREATE SCHEMA [jobs];');

IF SCHEMA_ID(N'reports') IS NULL EXEC(N'CREATE SCHEMA [reports];');

IF SCHEMA_ID(N'files') IS NULL EXEC(N'CREATE SCHEMA [files];');

ALTER TABLE [attendance].[Sites] ADD [City] nvarchar(100) NULL;

ALTER TABLE [attendance].[Sites] ADD [CityId] uniqueidentifier NULL;

ALTER TABLE [attendance].[Sites] ADD [ClientName] nvarchar(200) NULL;

ALTER TABLE [attendance].[Sites] ADD [EndsOn] date NULL;

ALTER TABLE [attendance].[Sites] ADD [ProjectId] uniqueidentifier NULL;

ALTER TABLE [attendance].[Sites] ADD [StartsOn] date NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [EvidenceFileId] uniqueidentifier NULL;

ALTER TABLE [hr].[Employees] ADD [ApprovedOnboardingDecisionId] uniqueidentifier NULL;

ALTER TABLE [hr].[Employees] ADD [DraftRevision] int NOT NULL DEFAULT 1;

ALTER TABLE [hr].[Employees] ADD [OnboardingStatus] int NOT NULL DEFAULT 0;

ALTER TABLE [hr].[Employees] ADD [SubmittedVersion] int NOT NULL DEFAULT 0;

ALTER TABLE [hr].[EmployeeDocuments] ADD [ScannedAt] datetimeoffset NULL;

ALTER TABLE [hr].[EmployeeDocuments] ADD [ScannerVersion] nvarchar(150) NULL;

ALTER TABLE [hr].[EmployeeDocuments] ADD [SignatureVerified] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [hr].[EmployeeDocuments] ADD [VerificationReason] nvarchar(1000) NULL;

ALTER TABLE [hr].[EmployeeDocuments] ADD [VerifiedAt] datetimeoffset NULL;

ALTER TABLE [hr].[EmployeeDocuments] ADD [VerifiedByUserId] nvarchar(200) NULL;

ALTER TABLE [hr].[EmployeeAddresses] ADD [CityId] uniqueidentifier NULL;

ALTER TABLE [hr].[Branches] ADD [CityId] uniqueidentifier NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [AK_RawPunchEvents_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]);

ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [AK_RawPunchEvents_EmployeeId_RegisteredDeviceId_IdempotencyKey_Id] UNIQUE ([EmployeeId], [RegisteredDeviceId], [IdempotencyKey], [Id]);

ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [AK_EmployeeDocuments_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]);

ALTER TABLE [hr].[EmployeeAssignments] ADD CONSTRAINT [AK_EmployeeAssignments_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]);

CREATE TABLE [workflow].[ApprovalDelegations] (
    [Id] uniqueidentifier NOT NULL,
    [DelegatorUserId] nvarchar(200) NOT NULL,
    [DelegateUserId] nvarchar(200) NOT NULL,
    [RequestType] nvarchar(100) NOT NULL,
    [StartsAt] datetimeoffset NOT NULL,
    [EndsAt] datetimeoffset NOT NULL,
    [AuthorizedByUserId] nvarchar(200) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [RevokedAt] datetimeoffset NULL,
    [RevokedByUserId] nvarchar(200) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ApprovalDelegations] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ApprovalDelegations_Dates] CHECK ([EndsAt] > [StartsAt]),
    CONSTRAINT [CK_ApprovalDelegations_Revocation] CHECK (([RevokedAt] IS NULL AND [RevokedByUserId] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedByUserId] IS NOT NULL)),
    CONSTRAINT [CK_ApprovalDelegations_Users] CHECK ([DelegatorUserId] <> [DelegateUserId])
);

CREATE TABLE [workflow].[ApprovalRouteRules] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Version] int NOT NULL,
    [RequestType] nvarchar(100) NOT NULL,
    [ApproverPermission] nvarchar(150) NOT NULL,
    [Stage] int NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [ApprovedByUserId] nvarchar(200) NOT NULL,
    [StaffCategoryId] uniqueidentifier NULL,
    [DepartmentId] uniqueidentifier NULL,
    [SiteId] uniqueidentifier NULL,
    [MinimumAmount] decimal(18,2) NULL,
    [MaximumAmount] decimal(18,2) NULL,
    [Currency] varchar(3) NULL,
    [MinimumDurationMinutes] int NULL,
    [MaximumDurationMinutes] int NULL,
    [RiskCode] nvarchar(50) NULL,
    CONSTRAINT [PK_ApprovalRouteRules] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ApprovalRouteRules_Amount] CHECK (([MinimumAmount] IS NULL OR [MinimumAmount] >= 0) AND ([MaximumAmount] IS NULL OR [MaximumAmount] >= 0) AND ([MinimumAmount] IS NULL OR [MaximumAmount] IS NULL OR [MaximumAmount] >= [MinimumAmount]) AND (([MinimumAmount] IS NULL AND [MaximumAmount] IS NULL) OR ([Currency] IS NOT NULL AND LEN([Currency]) = 3))),
    CONSTRAINT [CK_ApprovalRouteRules_Dates] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [CK_ApprovalRouteRules_Duration] CHECK (([MinimumDurationMinutes] IS NULL OR [MinimumDurationMinutes] >= 0) AND ([MaximumDurationMinutes] IS NULL OR [MaximumDurationMinutes] >= 0) AND ([MinimumDurationMinutes] IS NULL OR [MaximumDurationMinutes] IS NULL OR [MaximumDurationMinutes] >= [MinimumDurationMinutes])),
    CONSTRAINT [CK_ApprovalRouteRules_Version] CHECK ([Version] > 0 AND [Stage] > 0),
    CONSTRAINT [FK_ApprovalRouteRules_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [hr].[Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ApprovalRouteRules_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ApprovalRouteRules_StaffCategories_StaffCategoryId] FOREIGN KEY ([StaffCategoryId]) REFERENCES [hr].[StaffCategories] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[AttendancePeriodLocks] (
    [Id] uniqueidentifier NOT NULL,
    [PeriodStart] date NOT NULL,
    [PeriodEndExclusive] date NOT NULL,
    [IsLocked] bit NOT NULL,
    [Revision] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_AttendancePeriodLocks] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_AttendancePeriodLocks_Dates] CHECK ([PeriodEndExclusive] > [PeriodStart]),
    CONSTRAINT [CK_AttendancePeriodLocks_Revision] CHECK ([Revision] >= 0)
);

CREATE TABLE [audit].[AuditEvents] (
    [Id] uniqueidentifier NOT NULL,
    [EventType] nvarchar(100) NOT NULL,
    [ActorUserId] nvarchar(200) NOT NULL,
    [ActorRoleCode] nvarchar(100) NOT NULL,
    [TargetType] nvarchar(100) NOT NULL,
    [TargetId] nvarchar(200) NOT NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    [CorrelationId] nvarchar(100) NOT NULL,
    [EmployeeScopeId] uniqueidentifier NULL,
    [ProtectedChangesReference] nvarchar(500) NULL,
    [IpAddress] nvarchar(45) NULL,
    [DeviceReference] nvarchar(200) NULL,
    CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AuditEvents_Employees_EmployeeScopeId] FOREIGN KEY ([EmployeeScopeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[Cities] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Province] nvarchar(100) NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Cities] PRIMARY KEY ([Id])
);

CREATE TABLE [hr].[EmployeeAssetCustodies] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [AssetReference] nvarchar(100) NOT NULL,
    [IssuedAt] datetimeoffset NOT NULL,
    [IssueCondition] nvarchar(1000) NOT NULL,
    [IssueEvidencePrivateReference] nvarchar(500) NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [ReturnedAt] datetimeoffset NULL,
    [ReturnCondition] nvarchar(1000) NULL,
    [ReturnEvidencePrivateReference] nvarchar(500) NULL,
    [ReturnedToUserId] nvarchar(200) NULL,
    CONSTRAINT [PK_EmployeeAssetCustodies] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmployeeAssetCustodies_1] CHECK ([ReturnedAt] IS NULL OR [ReturnedAt] >= [IssuedAt]),
    CONSTRAINT [CK_EmployeeAssetCustodies_2] CHECK (([ReturnedAt] IS NULL AND [ReturnCondition] IS NULL AND [ReturnedToUserId] IS NULL AND [ReturnEvidencePrivateReference] IS NULL) OR ([ReturnedAt] IS NOT NULL AND [ReturnCondition] IS NOT NULL AND [ReturnedToUserId] IS NOT NULL)),
    CONSTRAINT [FK_EmployeeAssetCustodies_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeBankDetails] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [SupersedesEmployeeBankDetailsId] uniqueidentifier NULL,
    [PayloadSchemaVersion] int NOT NULL,
    [BankNameEncryptedReference] nvarchar(500) NOT NULL,
    [AccountTitleEncryptedReference] nvarchar(500) NOT NULL,
    [AccountNumberEncryptedReference] nvarchar(500) NOT NULL,
    [IbanEncryptedReference] nvarchar(500) NULL,
    [EncryptionKeyReference] nvarchar(200) NOT NULL,
    [MaskedAccountIdentifier] nvarchar(40) NOT NULL,
    [IsVerified] bit NOT NULL,
    [VerifiedByUserId] nvarchar(200) NULL,
    [VerifiedAt] datetimeoffset NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeBankDetails] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_EmployeeBankDetails_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]),
    CONSTRAINT [CK_EmployeeBankDetails_1] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [CK_EmployeeBankDetails_2] CHECK (([IsVerified] = 0 AND [VerifiedByUserId] IS NULL AND [VerifiedAt] IS NULL) OR ([IsVerified] = 1 AND [VerifiedByUserId] IS NOT NULL AND [VerifiedAt] IS NOT NULL)),
    CONSTRAINT [CK_EmployeeBankDetails_3] CHECK ([PayloadSchemaVersion] > 0),
    CONSTRAINT [CK_EmployeeBankDetails_History] CHECK (([Revision] = 1 AND [SupersedesEmployeeBankDetailsId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeBankDetailsId] IS NOT NULL)),
    CONSTRAINT [CK_EmployeeBankDetails_Predecessor] CHECK ([SupersedesEmployeeBankDetailsId] IS NULL OR [SupersedesEmployeeBankDetailsId] <> [Id]),
    CONSTRAINT [CK_EmployeeBankDetails_Revision] CHECK ([Revision] > 0),
    CONSTRAINT [FK_EmployeeBankDetails_EmployeeBankDetails_SupersedesEmployeeBankDetailsId_EmployeeId] FOREIGN KEY ([SupersedesEmployeeBankDetailsId], [EmployeeId]) REFERENCES [hr].[EmployeeBankDetails] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeBankDetails_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeCertifications] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [CertificateType] nvarchar(100) NOT NULL,
    [CertificateNumber] nvarchar(100) NOT NULL,
    [Issuer] nvarchar(200) NOT NULL,
    [IssuedOn] date NOT NULL,
    [ExpiresOn] date NULL,
    [EmployeeDocumentId] uniqueidentifier NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeCertifications] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmployeeCertifications_1] CHECK ([ExpiresOn] IS NULL OR [ExpiresOn] >= [IssuedOn]),
    CONSTRAINT [FK_EmployeeCertifications_EmployeeDocuments_EmployeeDocumentId_EmployeeId] FOREIGN KEY ([EmployeeDocumentId], [EmployeeId]) REFERENCES [hr].[EmployeeDocuments] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeCertifications_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeCommuteDistances] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [SupersedesEmployeeCommuteDistanceId] uniqueidentifier NULL,
    [UsualHomeOfficeKm] decimal(12,3) NOT NULL,
    [RoutePrivateReference] nvarchar(500) NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeCommuteDistances] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_EmployeeCommuteDistances_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]),
    CONSTRAINT [CK_EmployeeCommuteDistances_1] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [CK_EmployeeCommuteDistances_2] CHECK ([UsualHomeOfficeKm] >= 0),
    CONSTRAINT [CK_EmployeeCommuteDistances_History] CHECK (([Revision] = 1 AND [SupersedesEmployeeCommuteDistanceId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeCommuteDistanceId] IS NOT NULL)),
    CONSTRAINT [CK_EmployeeCommuteDistances_Predecessor] CHECK ([SupersedesEmployeeCommuteDistanceId] IS NULL OR [SupersedesEmployeeCommuteDistanceId] <> [Id]),
    CONSTRAINT [CK_EmployeeCommuteDistances_Revision] CHECK ([Revision] > 0),
    CONSTRAINT [FK_EmployeeCommuteDistances_EmployeeCommuteDistances_SupersedesEmployeeCommuteDistanceId_EmployeeId] FOREIGN KEY ([SupersedesEmployeeCommuteDistanceId], [EmployeeId]) REFERENCES [hr].[EmployeeCommuteDistances] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeCommuteDistances_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeMedicalRecords] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EncryptedPayloadReference] nvarchar(500) NOT NULL,
    [EncryptionKeyReference] nvarchar(200) NOT NULL,
    [EffectiveOn] date NOT NULL,
    [EmployeeDocumentId] uniqueidentifier NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeMedicalRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeMedicalRecords_EmployeeDocuments_EmployeeDocumentId_EmployeeId] FOREIGN KEY ([EmployeeDocumentId], [EmployeeId]) REFERENCES [hr].[EmployeeDocuments] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeMedicalRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeSimAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [SupersedesEmployeeSimAssignmentId] uniqueidentifier NULL,
    [PhoneNumber] nvarchar(30) NOT NULL,
    [SimReference] nvarchar(100) NOT NULL,
    [AllowedCreditTierCode] nvarchar(40) NOT NULL,
    [Status] int NOT NULL,
    [IssuedOn] date NOT NULL,
    [ReturnedOn] date NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeSimAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_EmployeeSimAssignments_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]),
    CONSTRAINT [CK_EmployeeSimAssignments_1] CHECK ([ReturnedOn] IS NULL OR [ReturnedOn] >= [IssuedOn]),
    CONSTRAINT [CK_EmployeeSimAssignments_2] CHECK ([Status] BETWEEN 1 AND 3),
    CONSTRAINT [CK_EmployeeSimAssignments_3] CHECK (([Status] = 3 AND [ReturnedOn] IS NOT NULL) OR ([Status] <> 3 AND [ReturnedOn] IS NULL)),
    CONSTRAINT [CK_EmployeeSimAssignments_History] CHECK (([Revision] = 1 AND [SupersedesEmployeeSimAssignmentId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeSimAssignmentId] IS NOT NULL)),
    CONSTRAINT [CK_EmployeeSimAssignments_Predecessor] CHECK ([SupersedesEmployeeSimAssignmentId] IS NULL OR [SupersedesEmployeeSimAssignmentId] <> [Id]),
    CONSTRAINT [CK_EmployeeSimAssignments_Revision] CHECK ([Revision] > 0),
    CONSTRAINT [FK_EmployeeSimAssignments_EmployeeSimAssignments_SupersedesEmployeeSimAssignmentId_EmployeeId] FOREIGN KEY ([SupersedesEmployeeSimAssignmentId], [EmployeeId]) REFERENCES [hr].[EmployeeSimAssignments] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeSimAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeSubmissions] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Version] int NOT NULL,
    [DraftRevision] int NOT NULL,
    [SubmittedByUserId] uniqueidentifier NOT NULL,
    [SubmittedAt] datetimeoffset NOT NULL,
    [SnapshotStorageKey] nvarchar(500) NOT NULL,
    [SnapshotObjectVersion] nvarchar(200) NOT NULL,
    [SnapshotHash] nvarchar(128) NOT NULL,
    [EncryptionKeyReference] nvarchar(200) NOT NULL,
    [SnapshotSchemaVersion] int NOT NULL,
    CONSTRAINT [PK_EmployeeSubmissions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_EmployeeSubmissions_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]),
    CONSTRAINT [CK_EmployeeSubmissions_Versions] CHECK ([Version] > 0 AND [DraftRevision] > 0 AND [SnapshotSchemaVersion] > 0),
    CONSTRAINT [FK_EmployeeSubmissions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[FaceEnrollments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ReplacesEnrollmentId] uniqueidentifier NULL,
    [EncryptedTemplateReference] nvarchar(500) NOT NULL,
    [EncryptionKeyReference] nvarchar(200) NOT NULL,
    [Provider] nvarchar(100) NOT NULL,
    [ProviderVersion] nvarchar(100) NOT NULL,
    [EnrolledBy] nvarchar(200) NOT NULL,
    [DeviceReference] nvarchar(200) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [CorrelationId] nvarchar(100) NOT NULL,
    [EnrolledAtUtc] datetimeoffset(7) NOT NULL,
    [IsActive] bit NOT NULL,
    [RevokedAtUtc] datetimeoffset(7) NULL,
    [RevokedBy] nvarchar(200) NULL,
    [RevocationReason] nvarchar(1000) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_FaceEnrollments] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_FaceEnrollments_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]),
    CONSTRAINT [CK_FaceEnrollments_EnrolledAtUtc_Utc] CHECK ([EnrolledAtUtc] IS NULL OR DATEPART(TZOFFSET, [EnrolledAtUtc]) = 0),
    CONSTRAINT [CK_FaceEnrollments_Revocation] CHECK (([IsActive] = 1 AND [RevokedAtUtc] IS NULL AND [RevokedBy] IS NULL AND [RevocationReason] IS NULL) OR ([IsActive] = 0 AND [RevokedAtUtc] IS NOT NULL AND [RevokedAtUtc] >= [EnrolledAtUtc] AND [RevokedBy] IS NOT NULL AND [RevocationReason] IS NOT NULL)),
    CONSTRAINT [CK_FaceEnrollments_RevokedAtUtc_Utc] CHECK ([RevokedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RevokedAtUtc]) = 0),
    CONSTRAINT [FK_FaceEnrollments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FaceEnrollments_FaceEnrollments_EmployeeId_ReplacesEnrollmentId] FOREIGN KEY ([EmployeeId], [ReplacesEnrollmentId]) REFERENCES [attendance].[FaceEnrollments] ([EmployeeId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[HolidayCalendars] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(40) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [GovernmentNotificationReference] nvarchar(500) NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_HolidayCalendars] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_HolidayCalendars_1] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom])
);

CREATE TABLE [workflow].[IdempotencyReceipts] (
    [Id] uniqueidentifier NOT NULL,
    [ActorUserId] nvarchar(200) NOT NULL,
    [Operation] nvarchar(100) NOT NULL,
    [Key] nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [RequestSha256] varchar(64) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [CompletedAt] datetimeoffset NULL,
    [ResultCode] nvarchar(100) NULL,
    [ProtectedResultReference] nvarchar(500) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_IdempotencyReceipts] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_IdempotencyReceipts_Expiry] CHECK ([ExpiresAt] > [CreatedAt]),
    CONSTRAINT [CK_IdempotencyReceipts_Hash] CHECK (LEN([RequestSha256]) = 64 AND [RequestSha256] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_IdempotencyReceipts_Result] CHECK (([CompletedAt] IS NULL AND [ResultCode] IS NULL AND [ProtectedResultReference] IS NULL) OR ([CompletedAt] IS NOT NULL AND [ResultCode] IS NOT NULL AND [ProtectedResultReference] IS NOT NULL AND [CompletedAt] >= [CreatedAt] AND [CompletedAt] < [ExpiresAt]))
);

CREATE TABLE [hr].[LeaveTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(40) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [RequiresEvidence] bit NOT NULL,
    [RequiresAdvanceApproval] bit NOT NULL,
    [ReducesRegularBalance] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_LeaveTypes] PRIMARY KEY ([Id])
);

CREATE TABLE [communication].[Notifications] (
    [Id] uniqueidentifier NOT NULL,
    [RecipientUserId] nvarchar(200) NOT NULL,
    [Channel] int NOT NULL,
    [TemplateCode] nvarchar(100) NOT NULL,
    [ProtectedPayloadReference] nvarchar(500) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CorrelationId] nvarchar(100) NOT NULL,
    [DeliveredAt] datetimeoffset NULL,
    [ReadAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Notifications_Channel] CHECK ([Channel] BETWEEN 1 AND 4),
    CONSTRAINT [CK_Notifications_Times] CHECK (([DeliveredAt] IS NULL OR [DeliveredAt] >= [CreatedAt]) AND ([ReadAt] IS NULL OR [ReadAt] >= [CreatedAt]))
);

CREATE TABLE [attendance].[OperationalZones] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [SiteId] uniqueidentifier NOT NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_OperationalZones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OperationalZones_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [jobs].[OutboxMessages] (
    [Id] uniqueidentifier NOT NULL,
    [MessageType] nvarchar(100) NOT NULL,
    [EncryptedPayloadReference] nvarchar(500) NOT NULL,
    [CorrelationId] nvarchar(100) NOT NULL,
    [DeduplicationKey] nvarchar(150) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [AvailableAt] datetimeoffset NOT NULL,
    [State] int NOT NULL,
    [Attempts] int NOT NULL,
    [LeaseToken] uniqueidentifier NULL,
    [LeaseExpiresAt] datetimeoffset NULL,
    [CompletedAt] datetimeoffset NULL,
    [LastFailureCode] nvarchar(100) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_OutboxMessages_Completed] CHECK (([State] = 2 AND [CompletedAt] IS NOT NULL AND [CompletedAt] >= [CreatedAt]) OR ([State] <> 2 AND [CompletedAt] IS NULL)),
    CONSTRAINT [CK_OutboxMessages_Lease] CHECK (([State] = 1 AND [LeaseToken] IS NOT NULL AND [LeaseExpiresAt] IS NOT NULL) OR ([State] <> 1 AND [LeaseToken] IS NULL AND [LeaseExpiresAt] IS NULL)),
    CONSTRAINT [CK_OutboxMessages_State] CHECK ([State] BETWEEN 0 AND 3 AND [Attempts] >= 0)
);

CREATE TABLE [auth].[Permissions] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(150) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [IsSensitive] bit NOT NULL,
    CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
);

CREATE TABLE [hr].[Policies] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(40) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Category] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Policies] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Policies_1] CHECK ([Category] BETWEEN 1 AND 5)
);

CREATE TABLE [hr].[PreviousEmployments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EmployerName] nvarchar(200) NOT NULL,
    [RoleTitle] nvarchar(150) NOT NULL,
    [StartedOn] date NOT NULL,
    [EndedOn] date NULL,
    [LeavingReason] nvarchar(1000) NULL,
    [LastSalaryEncryptedReference] nvarchar(500) NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_PreviousEmployments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PreviousEmployments_1] CHECK ([EndedOn] IS NULL OR [EndedOn] >= [StartedOn]),
    CONSTRAINT [FK_PreviousEmployments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[Projects] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Projects] PRIMARY KEY ([Id])
);

CREATE TABLE [auth].[Roles] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedByUserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);

CREATE TABLE [files].[StoredFiles] (
    [Id] uniqueidentifier NOT NULL,
    [StorageKey] nvarchar(400) NOT NULL,
    [OriginalName] nvarchar(255) NOT NULL,
    [Extension] nvarchar(20) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [Sha256] varchar(64) NOT NULL,
    [UploadedByUserId] nvarchar(200) NOT NULL,
    [UploadedAt] datetimeoffset NOT NULL,
    [EncryptionKeyReference] nvarchar(300) NULL,
    [RetainUntil] datetimeoffset NULL,
    [SignatureVerified] bit NOT NULL,
    [ScanState] int NOT NULL,
    [ScannerVersion] nvarchar(150) NULL,
    [ScannedAt] datetimeoffset NULL,
    [DeletedAt] datetimeoffset NULL,
    [DeletedByUserId] nvarchar(200) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_StoredFiles] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_StoredFiles_Deleted] CHECK (([DeletedAt] IS NULL AND [DeletedByUserId] IS NULL) OR ([DeletedAt] IS NOT NULL AND [DeletedByUserId] IS NOT NULL AND [DeletedAt] >= [UploadedAt])),
    CONSTRAINT [CK_StoredFiles_Hash] CHECK (LEN([Sha256]) = 64 AND [Sha256] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_StoredFiles_Retention] CHECK ([RetainUntil] IS NULL OR [RetainUntil] > [UploadedAt]),
    CONSTRAINT [CK_StoredFiles_Scan] CHECK ([ScanState] BETWEEN 0 AND 3 AND ([ScanState] <> 1 OR [SignatureVerified] = 1)),
    CONSTRAINT [CK_StoredFiles_Size] CHECK ([SizeBytes] > 0)
);

CREATE TABLE [auth].[Teams] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Teams] PRIMARY KEY ([Id])
);

CREATE TABLE [hr].[Vehicles] (
    [Id] uniqueidentifier NOT NULL,
    [RegistrationNumber] nvarchar(40) NOT NULL,
    [VehicleType] nvarchar(60) NOT NULL,
    [MakeModel] nvarchar(150) NOT NULL,
    [AssetReference] nvarchar(100) NULL,
    [RowVersion] rowversion NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Vehicles] PRIMARY KEY ([Id])
);

CREATE TABLE [attendance].[ZoneTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [DisplayColor] nvarchar(30) NOT NULL,
    [EntryRule] int NOT NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ZoneTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ZoneTypes_EntryRule] CHECK ([EntryRule] BETWEEN 1 AND 3)
);

CREATE TABLE [workflow].[ApprovalRequests] (
    [Id] uniqueidentifier NOT NULL,
    [RequestType] nvarchar(100) NOT NULL,
    [SubjectId] uniqueidentifier NOT NULL,
    [RequesterUserId] nvarchar(200) NOT NULL,
    [SnapshotPrivateReference] nvarchar(500) NOT NULL,
    [Stage] int NOT NULL,
    [RouteRuleId] uniqueidentifier NULL,
    [RequestedAt] datetimeoffset NOT NULL,
    [State] int NOT NULL,
    [ClosedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ApprovalRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ApprovalRequests_Closed] CHECK (([State] = 0 AND [ClosedAt] IS NULL) OR ([State] <> 0 AND [ClosedAt] IS NOT NULL AND [ClosedAt] >= [RequestedAt])),
    CONSTRAINT [CK_ApprovalRequests_State] CHECK ([State] BETWEEN 0 AND 4 AND [Stage] > 0),
    CONSTRAINT [FK_ApprovalRequests_ApprovalRouteRules_RouteRuleId] FOREIGN KEY ([RouteRuleId]) REFERENCES [workflow].[ApprovalRouteRules] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[Holidays] (
    [Id] uniqueidentifier NOT NULL,
    [HolidayCalendarId] uniqueidentifier NOT NULL,
    [HolidayDate] date NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [NotificationPrivateReference] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_Holidays] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Holidays_HolidayCalendars_HolidayCalendarId] FOREIGN KEY ([HolidayCalendarId]) REFERENCES [hr].[HolidayCalendars] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[ProjectMembers] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [AssignedByUserId] uniqueidentifier NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ProjectMembers] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ProjectMembers_Dates] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [FK_ProjectMembers_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ProjectMembers_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [auth].[Projects] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeOnboardingDecisions] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [SubmissionId] uniqueidentifier NOT NULL,
    [Decision] int NOT NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [ActorRoleId] uniqueidentifier NOT NULL,
    [Reason] nvarchar(2000) NULL,
    [DecidedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeOnboardingDecisions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_EmployeeOnboardingDecisions_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]),
    CONSTRAINT [CK_EmployeeOnboardingDecisions_Kind] CHECK ([Decision] IN (1,2,3)),
    CONSTRAINT [CK_EmployeeOnboardingDecisions_Reason] CHECK ([Decision] = 1 OR ([Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0)),
    CONSTRAINT [FK_EmployeeOnboardingDecisions_EmployeeSubmissions_EmployeeId_SubmissionId] FOREIGN KEY ([EmployeeId], [SubmissionId]) REFERENCES [hr].[EmployeeSubmissions] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeOnboardingDecisions_Roles_ActorRoleId] FOREIGN KEY ([ActorRoleId]) REFERENCES [auth].[Roles] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[RolePermissions] (
    [Id] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [PermissionId] uniqueidentifier NOT NULL,
    [GrantedByUserId] uniqueidentifier NOT NULL,
    [GrantedAt] datetimeoffset NOT NULL,
    [RevokedByUserId] uniqueidentifier NULL,
    [RevokedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_RolePermissions_Revocation] CHECK (([RevokedAt] IS NULL AND [RevokedByUserId] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedAt] >= [GrantedAt] AND [RevokedByUserId] IS NOT NULL)),
    CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [auth].[Permissions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [auth].[Roles] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [reports].[ReportExportJobs] (
    [Id] uniqueidentifier NOT NULL,
    [RequestedByUserId] nvarchar(200) NOT NULL,
    [ReportType] nvarchar(100) NOT NULL,
    [Format] int NOT NULL,
    [ProtectedFilterScopeReference] nvarchar(500) NOT NULL,
    [RequestedAt] datetimeoffset NOT NULL,
    [PeriodFrom] date NULL,
    [PeriodTo] date NULL,
    [State] int NOT NULL,
    [StoredFileId] uniqueidentifier NULL,
    [GeneratedAt] datetimeoffset NULL,
    [DownloadExpiresAt] datetimeoffset NULL,
    [SourceLockReference] nvarchar(500) NULL,
    [FailureCode] nvarchar(100) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ReportExportJobs] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ReportExportJobs_Completed] CHECK ([State] <> 2 OR ([StoredFileId] IS NOT NULL AND [GeneratedAt] IS NOT NULL AND [DownloadExpiresAt] IS NOT NULL AND [GeneratedAt] >= [RequestedAt] AND [DownloadExpiresAt] > [GeneratedAt])),
    CONSTRAINT [CK_ReportExportJobs_Period] CHECK (([PeriodFrom] IS NULL AND [PeriodTo] IS NULL) OR ([PeriodFrom] IS NOT NULL AND [PeriodTo] IS NOT NULL AND [PeriodTo] >= [PeriodFrom])),
    CONSTRAINT [CK_ReportExportJobs_State] CHECK ([State] BETWEEN 0 AND 3 AND [Format] BETWEEN 1 AND 3),
    CONSTRAINT [FK_ReportExportJobs_StoredFiles_StoredFileId] FOREIGN KEY ([StoredFileId]) REFERENCES [files].[StoredFiles] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[TeamMembers] (
    [Id] uniqueidentifier NOT NULL,
    [TeamId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [AssignedByUserId] uniqueidentifier NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_TeamMembers] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_TeamMembers_Dates] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [FK_TeamMembers_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TeamMembers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [auth].[Teams] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[InsurancePolicies] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NULL,
    [VehicleId] uniqueidentifier NULL,
    [ProviderName] nvarchar(200) NOT NULL,
    [PolicyNumber] nvarchar(100) NOT NULL,
    [CoverageStartDate] date NOT NULL,
    [ExpiryDate] date NOT NULL,
    [StoredFileId] uniqueidentifier NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [SupersedesPolicyId] uniqueidentifier NULL,
    CONSTRAINT [PK_InsurancePolicies] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_InsurancePolicies_CoverageDates] CHECK ([ExpiryDate] >= [CoverageStartDate]),
    CONSTRAINT [CK_InsurancePolicies_Owner] CHECK (([EmployeeId] IS NOT NULL AND [EmployeeId] <> '00000000-0000-0000-0000-000000000000' AND [VehicleId] IS NULL) OR ([VehicleId] IS NOT NULL AND [VehicleId] <> '00000000-0000-0000-0000-000000000000' AND [EmployeeId] IS NULL)),
    CONSTRAINT [CK_InsurancePolicies_Predecessor] CHECK ([SupersedesPolicyId] IS NULL OR [SupersedesPolicyId] <> [Id]),
    CONSTRAINT [CK_InsurancePolicies_RequiredText] CHECK (LEN(LTRIM(RTRIM([ProviderName]))) > 0 AND LEN(LTRIM(RTRIM([PolicyNumber]))) > 0 AND LEN(LTRIM(RTRIM([RecordedByUserId]))) > 0),
    CONSTRAINT [FK_InsurancePolicies_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InsurancePolicies_InsurancePolicies_SupersedesPolicyId] FOREIGN KEY ([SupersedesPolicyId]) REFERENCES [hr].[InsurancePolicies] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InsurancePolicies_StoredFiles_StoredFileId] FOREIGN KEY ([StoredFileId]) REFERENCES [files].[StoredFiles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InsurancePolicies_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [hr].[Vehicles] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[VehicleCustodies] (
    [Id] uniqueidentifier NOT NULL,
    [VehicleId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [IssuedAt] datetimeoffset NOT NULL,
    [IssueOdometerKm] decimal(12,3) NOT NULL,
    [IssueConditionPrivateReference] nvarchar(500) NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [ReturnedAt] datetimeoffset NULL,
    [ReturnOdometerKm] decimal(12,3) NULL,
    [ReturnConditionPrivateReference] nvarchar(500) NULL,
    [ReturnedToUserId] nvarchar(200) NULL,
    CONSTRAINT [PK_VehicleCustodies] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_VehicleCustodies_1] CHECK ([IssueOdometerKm] >= 0),
    CONSTRAINT [CK_VehicleCustodies_2] CHECK ([ReturnedAt] IS NULL OR ([ReturnedAt] >= [IssuedAt] AND [ReturnOdometerKm] >= [IssueOdometerKm])),
    CONSTRAINT [CK_VehicleCustodies_3] CHECK (([ReturnedAt] IS NULL AND [ReturnOdometerKm] IS NULL AND [ReturnConditionPrivateReference] IS NULL AND [ReturnedToUserId] IS NULL) OR ([ReturnedAt] IS NOT NULL AND [ReturnOdometerKm] IS NOT NULL AND [ReturnConditionPrivateReference] IS NOT NULL AND [ReturnedToUserId] IS NOT NULL)),
    CONSTRAINT [FK_VehicleCustodies_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VehicleCustodies_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [hr].[Vehicles] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[AdvanceLoanRequests] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [RequestedAmount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    [RequestedOn] date NOT NULL,
    [RequestedByUserId] nvarchar(200) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [SubmittedAt] datetimeoffset NULL,
    CONSTRAINT [PK_AdvanceLoanRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_AdvanceLoanRequests_Id_Currency] UNIQUE ([Id], [Currency]),
    CONSTRAINT [CK_AdvanceLoanRequests_1] CHECK ([RequestedAmount] > 0),
    CONSTRAINT [CK_AdvanceLoanRequests_2] CHECK ([Kind] BETWEEN 1 AND 2),
    CONSTRAINT [CK_AdvanceLoanRequests_3] CHECK (([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)),
    CONSTRAINT [CK_AdvanceLoanRequests_4] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [FK_AdvanceLoanRequests_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AdvanceLoanRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [workflow].[ApprovalDecisions] (
    [Id] uniqueidentifier NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [Decision] int NOT NULL,
    [ActorUserId] nvarchar(200) NOT NULL,
    [ActorRoleCode] nvarchar(100) NOT NULL,
    [Reason] nvarchar(2000) NULL,
    [DecidedAt] datetimeoffset NOT NULL,
    [ProtectedChangeReference] nvarchar(500) NULL,
    CONSTRAINT [PK_ApprovalDecisions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ApprovalDecisions_Decision] CHECK ([Decision] IN (1,2,3)),
    CONSTRAINT [CK_ApprovalDecisions_Reason] CHECK ([Decision] = 1 OR ([Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0)),
    CONSTRAINT [FK_ApprovalDecisions_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[AttendancePeriodLockEvents] (
    [Id] uniqueidentifier NOT NULL,
    [AttendancePeriodLockId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [IsLocked] bit NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [Actor] nvarchar(200) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    [AtUtc] datetimeoffset(7) NOT NULL,
    CONSTRAINT [PK_AttendancePeriodLockEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_AttendancePeriodLockEvents_AtUtc_Utc] CHECK ([AtUtc] IS NULL OR DATEPART(TZOFFSET, [AtUtc]) = 0),
    CONSTRAINT [CK_AttendancePeriodLockEvents_Revision] CHECK ([Revision] > 0),
    CONSTRAINT [FK_AttendancePeriodLockEvents_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendancePeriodLockEvents_AttendancePeriodLocks_AttendancePeriodLockId] FOREIGN KEY ([AttendancePeriodLockId]) REFERENCES [attendance].[AttendancePeriodLocks] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[CompensationRecords] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [SupersedesCompensationRecordId] uniqueidentifier NULL,
    [BaseAmount] decimal(18,2) NOT NULL,
    [GrossAmount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [WageBasis] int NOT NULL,
    [PaymentMethod] int NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CompensationRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_CompensationRecords_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]),
    CONSTRAINT [CK_CompensationRecords_1] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [CK_CompensationRecords_2] CHECK ([BaseAmount] >= 0 AND [GrossAmount] >= [BaseAmount]),
    CONSTRAINT [CK_CompensationRecords_3] CHECK ([WageBasis] BETWEEN 1 AND 3),
    CONSTRAINT [CK_CompensationRecords_4] CHECK ([PaymentMethod] BETWEEN 1 AND 4),
    CONSTRAINT [CK_CompensationRecords_5] CHECK ([Revision] > 0),
    CONSTRAINT [CK_CompensationRecords_6] CHECK ([SupersedesCompensationRecordId] IS NULL OR [SupersedesCompensationRecordId] <> [Id]),
    CONSTRAINT [CK_CompensationRecords_7] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [FK_CompensationRecords_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CompensationRecords_CompensationRecords_SupersedesCompensationRecordId_EmployeeId] FOREIGN KEY ([SupersedesCompensationRecordId], [EmployeeId]) REFERENCES [hr].[CompensationRecords] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CompensationRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[FaceVerificationExceptions] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [AlternativeMethodCode] nvarchar(100) NOT NULL,
    [PrivateEvidenceReference] nvarchar(500) NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NOT NULL,
    [RequestedBy] nvarchar(200) NOT NULL,
    [RequestedAtUtc] datetimeoffset(7) NOT NULL,
    CONSTRAINT [PK_FaceVerificationExceptions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_FaceVerificationExceptions_Dates] CHECK ([EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [CK_FaceVerificationExceptions_RequestedAtUtc_Utc] CHECK ([RequestedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedAtUtc]) = 0),
    CONSTRAINT [FK_FaceVerificationExceptions_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FaceVerificationExceptions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[PayrollHandoffBatches] (
    [Id] uniqueidentifier NOT NULL,
    [BatchReference] nvarchar(100) NOT NULL,
    [PeriodStart] date NOT NULL,
    [PeriodEnd] date NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [PreparedByUserId] nvarchar(200) NOT NULL,
    [PreparedAt] datetimeoffset NOT NULL,
    [LinesRevision] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [LockedAt] datetimeoffset NULL,
    [LockedByUserId] nvarchar(200) NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    CONSTRAINT [PK_PayrollHandoffBatches] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_PayrollHandoffBatches_Id_Currency] UNIQUE ([Id], [Currency]),
    CONSTRAINT [CK_PayrollHandoffBatches_1] CHECK ([PeriodEnd] >= [PeriodStart]),
    CONSTRAINT [CK_PayrollHandoffBatches_2] CHECK (([LockedAt] IS NULL AND [LockedByUserId] IS NULL AND [ApprovalRequestId] IS NULL) OR ([LockedAt] IS NOT NULL AND [LockedAt] >= [PreparedAt] AND [LockedByUserId] IS NOT NULL AND [ApprovalRequestId] IS NOT NULL)),
    CONSTRAINT [CK_PayrollHandoffBatches_3] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [CK_PayrollHandoffBatches_Revision] CHECK ([LinesRevision] >= 0),
    CONSTRAINT [FK_PayrollHandoffBatches_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[PolicyVersions] (
    [Id] uniqueidentifier NOT NULL,
    [PolicyId] uniqueidentifier NOT NULL,
    [VersionNumber] int NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [RulesSchemaVersion] nvarchar(60) NOT NULL,
    [RulesJson] nvarchar(max) NOT NULL,
    [PolicyDocumentPrivateReference] nvarchar(500) NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [PublishedByUserId] nvarchar(200) NOT NULL,
    [PublishedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PolicyVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PolicyVersions_1] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [CK_PolicyVersions_2] CHECK ([VersionNumber] > 0),
    CONSTRAINT [CK_PolicyVersions_3] CHECK (ISJSON([RulesJson]) = 1),
    CONSTRAINT [FK_PolicyVersions_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PolicyVersions_Policies_PolicyId] FOREIGN KEY ([PolicyId]) REFERENCES [hr].[Policies] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ShiftPopulationAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [ShiftId] uniqueidentifier NOT NULL,
    [PopulationKind] int NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [TeamId] uniqueidentifier NULL,
    [SiteId] uniqueidentifier NULL,
    [StaffCategoryId] uniqueidentifier NULL,
    [DepartmentId] uniqueidentifier NULL,
    CONSTRAINT [PK_ShiftPopulationAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ShiftPopulationAssignments_Dates] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [CK_ShiftPopulationAssignments_Population] CHECK (([PopulationKind] = 1 AND [TeamId] IS NOT NULL AND [SiteId] IS NULL AND [StaffCategoryId] IS NULL AND [DepartmentId] IS NULL) OR ([PopulationKind] = 2 AND [TeamId] IS NULL AND [SiteId] IS NOT NULL AND [StaffCategoryId] IS NULL AND [DepartmentId] IS NULL) OR ([PopulationKind] = 3 AND [TeamId] IS NULL AND [SiteId] IS NULL AND [StaffCategoryId] IS NOT NULL AND [DepartmentId] IS NULL) OR ([PopulationKind] = 4 AND [TeamId] IS NULL AND [SiteId] IS NULL AND [StaffCategoryId] IS NULL AND [DepartmentId] IS NOT NULL)),
    CONSTRAINT [FK_ShiftPopulationAssignments_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ShiftPopulationAssignments_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [hr].[Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ShiftPopulationAssignments_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [attendance].[Shifts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ShiftPopulationAssignments_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ShiftPopulationAssignments_StaffCategories_StaffCategoryId] FOREIGN KEY ([StaffCategoryId]) REFERENCES [hr].[StaffCategories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ShiftPopulationAssignments_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [auth].[Teams] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[TemporaryShiftChanges] (
    [Id] uniqueidentifier NOT NULL,
    [SiteId] uniqueidentifier NOT NULL,
    [AttendanceDate] date NOT NULL,
    [OriginalShiftVersionId] uniqueidentifier NOT NULL,
    [ReplacementShiftVersionId] uniqueidentifier NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [ApprovedBy] nvarchar(200) NOT NULL,
    [ApprovedAtUtc] datetimeoffset(7) NOT NULL,
    [WrittenEvidenceReference] nvarchar(500) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    CONSTRAINT [PK_TemporaryShiftChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_TemporaryShiftChanges_ApprovedAtUtc_Utc] CHECK ([ApprovedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ApprovedAtUtc]) = 0),
    CONSTRAINT [CK_TemporaryShiftChanges_Versions] CHECK ([OriginalShiftVersionId] <> [ReplacementShiftVersionId]),
    CONSTRAINT [FK_TemporaryShiftChanges_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TemporaryShiftChanges_ShiftVersions_OriginalShiftVersionId] FOREIGN KEY ([OriginalShiftVersionId]) REFERENCES [attendance].[ShiftVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TemporaryShiftChanges_ShiftVersions_ReplacementShiftVersionId] FOREIGN KEY ([ReplacementShiftVersionId]) REFERENCES [attendance].[ShiftVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TemporaryShiftChanges_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[VehicleTrips] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [VehicleId] uniqueidentifier NOT NULL,
    [Purpose] int NOT NULL,
    [StartedAt] datetimeoffset NOT NULL,
    [EndedAt] datetimeoffset NOT NULL,
    [OriginLabel] nvarchar(200) NOT NULL,
    [DestinationLabel] nvarchar(200) NOT NULL,
    [OdometerStartKm] decimal(12,3) NOT NULL,
    [OdometerEndKm] decimal(12,3) NOT NULL,
    [MapDistanceKm] decimal(12,3) NOT NULL,
    [RouteEvidencePrivateReference] nvarchar(500) NOT NULL,
    [WorkReference] nvarchar(150) NOT NULL,
    [RequestedByUserId] nvarchar(200) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [SubmittedAt] datetimeoffset NULL,
    CONSTRAINT [PK_VehicleTrips] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_VehicleTrips_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]),
    CONSTRAINT [AK_VehicleTrips_Id_EmployeeId_VehicleId] UNIQUE ([Id], [EmployeeId], [VehicleId]),
    CONSTRAINT [CK_VehicleTrips_1] CHECK ([EndedAt] >= [StartedAt]),
    CONSTRAINT [CK_VehicleTrips_2] CHECK ([OdometerEndKm] >= [OdometerStartKm] AND [OdometerStartKm] >= 0 AND [MapDistanceKm] >= 0),
    CONSTRAINT [CK_VehicleTrips_3] CHECK ([Purpose] BETWEEN 1 AND 5),
    CONSTRAINT [CK_VehicleTrips_4] CHECK (([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)),
    CONSTRAINT [FK_VehicleTrips_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VehicleTrips_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VehicleTrips_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [hr].[Vehicles] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ZoneVersions] (
    [Id] uniqueidentifier NOT NULL,
    [OperationalZoneId] uniqueidentifier NOT NULL,
    [VersionNumber] int NOT NULL,
    [ZoneTypeId] uniqueidentifier NOT NULL,
    [EntryRule] int NOT NULL,
    [BoundaryKind] int NOT NULL,
    [EffectiveFromUtc] datetimeoffset(7) NOT NULL,
    [EffectiveToUtc] datetimeoffset(7) NOT NULL,
    [EmployeeInstruction] nvarchar(2000) NOT NULL,
    [ReasonCode] nvarchar(100) NOT NULL,
    [PrivateRestrictionReasonReference] nvarchar(500) NULL,
    [ApprovedBy] nvarchar(200) NOT NULL,
    [ApprovedAtUtc] datetimeoffset(7) NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [Latitude] decimal(10,7) NULL,
    [Longitude] decimal(10,7) NULL,
    [RadiusMeters] decimal(10,2) NULL,
    [PolygonGeoJson] nvarchar(max) NULL,
    CONSTRAINT [PK_ZoneVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ZoneVersions_ApprovedAtUtc_Utc] CHECK ([ApprovedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ApprovedAtUtc]) = 0),
    CONSTRAINT [CK_ZoneVersions_Boundary] CHECK (([BoundaryKind] = 1 AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [RadiusMeters] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND [RadiusMeters] > 0 AND [PolygonGeoJson] IS NULL) OR ([BoundaryKind] = 2 AND [Latitude] IS NULL AND [Longitude] IS NULL AND [RadiusMeters] IS NULL AND [PolygonGeoJson] IS NOT NULL AND ISJSON([PolygonGeoJson]) = 1)),
    CONSTRAINT [CK_ZoneVersions_EffectiveFromUtc_Utc] CHECK ([EffectiveFromUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveFromUtc]) = 0),
    CONSTRAINT [CK_ZoneVersions_EffectiveToUtc_Utc] CHECK ([EffectiveToUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveToUtc]) = 0),
    CONSTRAINT [CK_ZoneVersions_EntryRule] CHECK ([EntryRule] BETWEEN 1 AND 3),
    CONSTRAINT [CK_ZoneVersions_Version] CHECK ([VersionNumber] > 0),
    CONSTRAINT [CK_ZoneVersions_Window] CHECK ([EffectiveToUtc] > [EffectiveFromUtc]),
    CONSTRAINT [FK_ZoneVersions_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneVersions_OperationalZones_OperationalZoneId] FOREIGN KEY ([OperationalZoneId]) REFERENCES [attendance].[OperationalZones] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneVersions_ZoneTypes_ZoneTypeId] FOREIGN KEY ([ZoneTypeId]) REFERENCES [attendance].[ZoneTypes] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[InsuranceExpiryReminders] (
    [Id] uniqueidentifier NOT NULL,
    [InsurancePolicyId] uniqueidentifier NOT NULL,
    [RecipientUserId] nvarchar(200) NOT NULL,
    [Channel] int NOT NULL,
    [RemindAt] datetimeoffset NOT NULL,
    [CreatedByUserId] nvarchar(200) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [NotificationId] uniqueidentifier NULL,
    [CancelledAt] datetimeoffset NULL,
    [CancelledByUserId] nvarchar(200) NULL,
    [CancellationReason] nvarchar(1000) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_InsuranceExpiryReminders] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_InsuranceExpiryReminders_Channel] CHECK ([Channel] BETWEEN 1 AND 4),
    CONSTRAINT [CK_InsuranceExpiryReminders_RequiredText] CHECK (LEN(LTRIM(RTRIM([RecipientUserId]))) > 0 AND LEN(LTRIM(RTRIM([CreatedByUserId]))) > 0),
    CONSTRAINT [CK_InsuranceExpiryReminders_State] CHECK (([CancelledAt] IS NULL AND [CancelledByUserId] IS NULL AND [CancellationReason] IS NULL) OR ([CancelledAt] IS NOT NULL AND [CancelledByUserId] IS NOT NULL AND [CancellationReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CancelledByUserId]))) > 0 AND LEN(LTRIM(RTRIM([CancellationReason]))) > 0 AND [NotificationId] IS NULL)),
    CONSTRAINT [CK_InsuranceExpiryReminders_Times] CHECK ([RemindAt] >= [CreatedAt] AND ([CancelledAt] IS NULL OR [CancelledAt] >= [CreatedAt])),
    CONSTRAINT [FK_InsuranceExpiryReminders_InsurancePolicies_InsurancePolicyId] FOREIGN KEY ([InsurancePolicyId]) REFERENCES [hr].[InsurancePolicies] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InsuranceExpiryReminders_Notifications_NotificationId] FOREIGN KEY ([NotificationId]) REFERENCES [communication].[Notifications] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[VehicleIncidents] (
    [Id] uniqueidentifier NOT NULL,
    [VehicleCustodyId] uniqueidentifier NOT NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    [IncidentEvidencePrivateReference] nvarchar(500) NOT NULL,
    [ThirdPartyDetailsEncryptedReference] nvarchar(500) NULL,
    [InsuranceClaimReference] nvarchar(200) NULL,
    [ProposedEmployeeLiability] decimal(18,2) NULL,
    [Currency] nvarchar(3) NULL,
    [RequestedByUserId] nvarchar(200) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [SubmittedAt] datetimeoffset NULL,
    CONSTRAINT [PK_VehicleIncidents] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_VehicleIncidents_1] CHECK (([ProposedEmployeeLiability] IS NULL AND [Currency] IS NULL) OR ([ProposedEmployeeLiability] IS NOT NULL AND [ProposedEmployeeLiability] >= 0 AND [Currency] IS NOT NULL)),
    CONSTRAINT [CK_VehicleIncidents_2] CHECK (([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)),
    CONSTRAINT [CK_VehicleIncidents_3] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [FK_VehicleIncidents_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VehicleIncidents_VehicleCustodies_VehicleCustodyId] FOREIGN KEY ([VehicleCustodyId]) REFERENCES [hr].[VehicleCustodies] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[AdvanceLoanAgreements] (
    [Id] uniqueidentifier NOT NULL,
    [AdvanceLoanRequestId] uniqueidentifier NOT NULL,
    [ApprovedPrincipal] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [EffectiveOn] date NOT NULL,
    [ApprovedTermsPrivateReference] nvarchar(500) NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AdvanceLoanAgreements] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_AdvanceLoanAgreements_Id_Currency] UNIQUE ([Id], [Currency]),
    CONSTRAINT [CK_AdvanceLoanAgreements_1] CHECK ([ApprovedPrincipal] > 0),
    CONSTRAINT [CK_AdvanceLoanAgreements_2] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [FK_AdvanceLoanAgreements_AdvanceLoanRequests_AdvanceLoanRequestId_Currency] FOREIGN KEY ([AdvanceLoanRequestId], [Currency]) REFERENCES [hr].[AdvanceLoanRequests] ([Id], [Currency]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AdvanceLoanAgreements_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[PayrollHandoffProcessingRecords] (
    [Id] uniqueidentifier NOT NULL,
    [PayrollHandoffBatchId] uniqueidentifier NOT NULL,
    [ExternalProcessingReference] nvarchar(150) NOT NULL,
    [ProtectedResultReference] nvarchar(500) NOT NULL,
    [ProcessedByUserId] nvarchar(200) NOT NULL,
    [ProcessedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PayrollHandoffProcessingRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollHandoffProcessingRecords_PayrollHandoffBatches_PayrollHandoffBatchId] FOREIGN KEY ([PayrollHandoffBatchId]) REFERENCES [hr].[PayrollHandoffBatches] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[DailyAttendanceSummaries] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [AttendanceDate] date NOT NULL,
    [Revision] int NOT NULL,
    [Status] int NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [ShiftVersionId] uniqueidentifier NOT NULL,
    [CalculationVersion] nvarchar(100) NOT NULL,
    [InputFingerprint] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ScheduledMinutes] int NOT NULL,
    [WorkedMinutes] int NOT NULL,
    [PayableMinutes] int NOT NULL,
    [LateMinutes] int NOT NULL,
    [OvertimeMinutes] int NOT NULL,
    [ExceptionCount] int NOT NULL,
    [CalculatedAtUtc] datetimeoffset(7) NOT NULL,
    [SupersedesSummaryId] uniqueidentifier NULL,
    CONSTRAINT [PK_DailyAttendanceSummaries] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_DailyAttendanceSummaries_EmployeeId_AttendanceDate_Id] UNIQUE ([EmployeeId], [AttendanceDate], [Id]),
    CONSTRAINT [AK_DailyAttendanceSummaries_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]),
    CONSTRAINT [CK_DailyAttendanceSummaries_CalculatedAtUtc_Utc] CHECK ([CalculatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CalculatedAtUtc]) = 0),
    CONSTRAINT [CK_DailyAttendanceSummaries_Fingerprint] CHECK (LEN([InputFingerprint]) = 64 AND [InputFingerprint] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_DailyAttendanceSummaries_Minutes] CHECK ([ScheduledMinutes] >= 0 AND [WorkedMinutes] >= 0 AND [PayableMinutes] >= 0 AND [LateMinutes] >= 0 AND [OvertimeMinutes] >= 0 AND [ExceptionCount] >= 0),
    CONSTRAINT [CK_DailyAttendanceSummaries_Revision] CHECK ([Revision] > 0),
    CONSTRAINT [CK_DailyAttendanceSummaries_Status] CHECK ([Status] BETWEEN 1 AND 9),
    CONSTRAINT [FK_DailyAttendanceSummaries_DailyAttendanceSummaries_EmployeeId_AttendanceDate_SupersedesSummaryId] FOREIGN KEY ([EmployeeId], [AttendanceDate], [SupersedesSummaryId]) REFERENCES [attendance].[DailyAttendanceSummaries] ([EmployeeId], [AttendanceDate], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_DailyAttendanceSummaries_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_DailyAttendanceSummaries_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_DailyAttendanceSummaries_ShiftVersions_ShiftVersionId] FOREIGN KEY ([ShiftVersionId]) REFERENCES [attendance].[ShiftVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[LeaveEncashmentRequests] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [LeaveYearStart] date NOT NULL,
    [LeaveYearEnd] date NOT NULL,
    [RequestedMinutes] int NOT NULL,
    [AssessedKpiPercent] decimal(9,4) NOT NULL,
    [ProposedAmount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [CalculationPrivateReference] nvarchar(500) NOT NULL,
    [RequestedByUserId] nvarchar(200) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [SubmittedAt] datetimeoffset NULL,
    CONSTRAINT [PK_LeaveEncashmentRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_LeaveEncashmentRequests_1] CHECK ([LeaveYearEnd] > [LeaveYearStart]),
    CONSTRAINT [CK_LeaveEncashmentRequests_2] CHECK ([RequestedMinutes] > 0),
    CONSTRAINT [CK_LeaveEncashmentRequests_3] CHECK ([AssessedKpiPercent] BETWEEN 0 AND 100),
    CONSTRAINT [CK_LeaveEncashmentRequests_4] CHECK ([ProposedAmount] >= 0),
    CONSTRAINT [CK_LeaveEncashmentRequests_5] CHECK (([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)),
    CONSTRAINT [CK_LeaveEncashmentRequests_6] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [FK_LeaveEncashmentRequests_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveEncashmentRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveEncashmentRequests_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[LeaveRequests] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [LeaveTypeId] uniqueidentifier NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NOT NULL,
    [RequestedMinutes] int NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    [ContactDuringLeave] nvarchar(200) NULL,
    [RelieverEmployeeId] uniqueidentifier NULL,
    [HandoverNotes] nvarchar(2000) NULL,
    [EvidencePrivateReference] nvarchar(500) NULL,
    [PayTreatment] int NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [RequestedByUserId] nvarchar(200) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [SubmittedAt] datetimeoffset NULL,
    CONSTRAINT [PK_LeaveRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_LeaveRequests_Id_EmployeeId_LeaveTypeId] UNIQUE ([Id], [EmployeeId], [LeaveTypeId]),
    CONSTRAINT [CK_LeaveRequests_1] CHECK ([EndDate] >= [StartDate]),
    CONSTRAINT [CK_LeaveRequests_2] CHECK ([RequestedMinutes] > 0),
    CONSTRAINT [CK_LeaveRequests_3] CHECK ([PayTreatment] BETWEEN 0 AND 3),
    CONSTRAINT [CK_LeaveRequests_4] CHECK ([RelieverEmployeeId] IS NULL OR [RelieverEmployeeId] <> [EmployeeId]),
    CONSTRAINT [CK_LeaveRequests_5] CHECK (([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)),
    CONSTRAINT [FK_LeaveRequests_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveRequests_Employees_RelieverEmployeeId] FOREIGN KEY ([RelieverEmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveRequests_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId]) REFERENCES [hr].[LeaveTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveRequests_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[PayrollHandoffLines] (
    [Id] uniqueidentifier NOT NULL,
    [PayrollHandoffBatchId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Component] int NOT NULL,
    [SourceType] nvarchar(100) NOT NULL,
    [SourceId] uniqueidentifier NOT NULL,
    [SourceVersion] int NOT NULL,
    [AttendancePeriodLockEventId] uniqueidentifier NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [PolicyVersionId] uniqueidentifier NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [CalculationPrivateReference] nvarchar(500) NOT NULL,
    [TraceId] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_PayrollHandoffLines] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PayrollHandoffLines_1] CHECK ([Component] BETWEEN 1 AND 8),
    CONSTRAINT [CK_PayrollHandoffLines_2] CHECK ([SourceVersion] > 0),
    CONSTRAINT [CK_PayrollHandoffLines_3] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [FK_PayrollHandoffLines_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PayrollHandoffLines_AttendancePeriodLockEvents_AttendancePeriodLockEventId] FOREIGN KEY ([AttendancePeriodLockEventId]) REFERENCES [attendance].[AttendancePeriodLockEvents] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PayrollHandoffLines_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PayrollHandoffLines_PayrollHandoffBatches_PayrollHandoffBatchId_Currency] FOREIGN KEY ([PayrollHandoffBatchId], [Currency]) REFERENCES [hr].[PayrollHandoffBatches] ([Id], [Currency]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PayrollHandoffLines_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[PolicyAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NULL,
    [EmployeeId] uniqueidentifier NULL,
    [DepartmentId] uniqueidentifier NULL,
    [StaffCategoryId] uniqueidentifier NULL,
    [SiteId] uniqueidentifier NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [InstructionPrivateReference] nvarchar(500) NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PolicyAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PolicyAssignments_1] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [CK_PolicyAssignments_2] CHECK ((CASE WHEN [RoleId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [EmployeeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [DepartmentId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [StaffCategoryId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [SiteId] IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [FK_PolicyAssignments_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PolicyAssignments_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [hr].[Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PolicyAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PolicyAssignments_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PolicyAssignments_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [auth].[Roles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PolicyAssignments_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PolicyAssignments_StaffCategories_StaffCategoryId] FOREIGN KEY ([StaffCategoryId]) REFERENCES [hr].[StaffCategories] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[PolicyImpactAssessments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [PeriodStart] date NOT NULL,
    [PeriodEnd] date NOT NULL,
    [AffectedMinutes] int NOT NULL,
    [ProposedMoneyImpact] decimal(18,2) NULL,
    [Currency] nvarchar(3) NULL,
    [ProposedKpiPercent] decimal(9,4) NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [CalculationVersion] nvarchar(80) NOT NULL,
    [ProtectedCalculationReference] nvarchar(500) NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PolicyImpactAssessments] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_PolicyImpactAssessments_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]),
    CONSTRAINT [CK_PolicyImpactAssessments_1] CHECK ([PeriodEnd] >= [PeriodStart]),
    CONSTRAINT [CK_PolicyImpactAssessments_2] CHECK ([AffectedMinutes] >= 0),
    CONSTRAINT [CK_PolicyImpactAssessments_3] CHECK ([Kind] BETWEEN 1 AND 6),
    CONSTRAINT [CK_PolicyImpactAssessments_4] CHECK ([ProposedKpiPercent] IS NULL OR [ProposedKpiPercent] BETWEEN 0 AND 100),
    CONSTRAINT [CK_PolicyImpactAssessments_5] CHECK (([ProposedMoneyImpact] IS NULL AND [Currency] IS NULL) OR ([ProposedMoneyImpact] IS NOT NULL AND [ProposedMoneyImpact] >= 0 AND [Currency] IS NOT NULL)),
    CONSTRAINT [CK_PolicyImpactAssessments_6] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [FK_PolicyImpactAssessments_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PolicyImpactAssessments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PolicyImpactAssessments_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[VehicleServiceRecords] (
    [Id] uniqueidentifier NOT NULL,
    [VehicleId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ServiceKind] int NOT NULL,
    [ServiceDate] date NOT NULL,
    [OdometerKm] decimal(12,3) NOT NULL,
    [ApprovedOutstationKm] decimal(12,3) NOT NULL,
    [AuthorizedWorkshopReference] nvarchar(200) NOT NULL,
    [EvidencePrivateReference] nvarchar(500) NOT NULL,
    [ClaimedAmount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [EligibilityCalculationPrivateReference] nvarchar(500) NOT NULL,
    [RequestedByUserId] nvarchar(200) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [SubmittedAt] datetimeoffset NULL,
    CONSTRAINT [PK_VehicleServiceRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_VehicleServiceRecords_1] CHECK ([OdometerKm] >= 0 AND [ApprovedOutstationKm] >= 0 AND [ClaimedAmount] >= 0),
    CONSTRAINT [CK_VehicleServiceRecords_2] CHECK ([ServiceKind] BETWEEN 1 AND 3),
    CONSTRAINT [CK_VehicleServiceRecords_3] CHECK (([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)),
    CONSTRAINT [CK_VehicleServiceRecords_4] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [FK_VehicleServiceRecords_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VehicleServiceRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VehicleServiceRecords_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VehicleServiceRecords_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [hr].[Vehicles] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[WaitingTimeEvidences] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [DelayedByEmployeeId] uniqueidentifier NOT NULL,
    [SiteId] uniqueidentifier NULL,
    [WaitingStartedAt] datetimeoffset NOT NULL,
    [WaitingEndedAt] datetimeoffset NOT NULL,
    [VerifiedWaitingMinutes] int NOT NULL,
    [WorkPairReference] nvarchar(150) NOT NULL,
    [EvidencePrivateReference] nvarchar(500) NOT NULL,
    [VerifiedByUserId] nvarchar(200) NOT NULL,
    [VerifiedAt] datetimeoffset NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WaitingTimeEvidences] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_WaitingTimeEvidences_1] CHECK ([WaitingEndedAt] > [WaitingStartedAt]),
    CONSTRAINT [CK_WaitingTimeEvidences_2] CHECK ([VerifiedWaitingMinutes] >= 0 AND [VerifiedWaitingMinutes] <= DATEDIFF(MINUTE, [WaitingStartedAt], [WaitingEndedAt])),
    CONSTRAINT [CK_WaitingTimeEvidences_3] CHECK ([EmployeeId] <> [DelayedByEmployeeId]),
    CONSTRAINT [FK_WaitingTimeEvidences_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WaitingTimeEvidences_Employees_DelayedByEmployeeId] FOREIGN KEY ([DelayedByEmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WaitingTimeEvidences_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WaitingTimeEvidences_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WaitingTimeEvidences_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[TemporaryShiftChangeEmployees] (
    [Id] uniqueidentifier NOT NULL,
    [TemporaryShiftChangeId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TemporaryShiftChangeEmployees] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TemporaryShiftChangeEmployees_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TemporaryShiftChangeEmployees_TemporaryShiftChanges_TemporaryShiftChangeId] FOREIGN KEY ([TemporaryShiftChangeId]) REFERENCES [attendance].[TemporaryShiftChanges] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[FuelClaims] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [VehicleTripId] uniqueidentifier NOT NULL,
    [EmployeeCommuteDistanceId] uniqueidentifier NULL,
    [EligibleDistanceKm] decimal(12,3) NOT NULL,
    [ExcludedCommuteKm] decimal(12,3) NOT NULL,
    [FuelLiters] decimal(12,3) NOT NULL,
    [ClaimedAmount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [CalculationPrivateReference] nvarchar(500) NOT NULL,
    [ReceiptPrivateReference] nvarchar(500) NULL,
    [RequestedByUserId] nvarchar(200) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [SubmittedAt] datetimeoffset NULL,
    CONSTRAINT [PK_FuelClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_FuelClaims_1] CHECK ([EligibleDistanceKm] >= 0 AND [ExcludedCommuteKm] >= 0 AND [FuelLiters] >= 0 AND [ClaimedAmount] >= 0),
    CONSTRAINT [CK_FuelClaims_2] CHECK (([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)),
    CONSTRAINT [CK_FuelClaims_3] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [FK_FuelClaims_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FuelClaims_EmployeeCommuteDistances_EmployeeCommuteDistanceId_EmployeeId] FOREIGN KEY ([EmployeeCommuteDistanceId], [EmployeeId]) REFERENCES [hr].[EmployeeCommuteDistances] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FuelClaims_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FuelClaims_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FuelClaims_VehicleTrips_VehicleTripId_EmployeeId] FOREIGN KEY ([VehicleTripId], [EmployeeId]) REFERENCES [hr].[VehicleTrips] ([Id], [EmployeeId]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[VehicleMileageEntries] (
    [Id] uniqueidentifier NOT NULL,
    [VehicleId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [VehicleTripId] uniqueidentifier NOT NULL,
    [BusinessDate] date NOT NULL,
    [ApprovedBusinessKm] decimal(12,3) NOT NULL,
    [ExcludedCommuteKm] decimal(12,3) NOT NULL,
    [OutstationKm] decimal(12,3) NOT NULL,
    [MonthlyAdjustmentKm] decimal(12,3) NOT NULL,
    [CalculationPrivateReference] nvarchar(500) NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [IdempotencyKey] nvarchar(100) NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_VehicleMileageEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_VehicleMileageEntries_1] CHECK ([ApprovedBusinessKm] >= 0 AND [ExcludedCommuteKm] >= 0 AND [OutstationKm] >= 0 AND [OutstationKm] <= [ApprovedBusinessKm]),
    CONSTRAINT [FK_VehicleMileageEntries_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VehicleMileageEntries_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VehicleMileageEntries_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VehicleMileageEntries_VehicleTrips_VehicleTripId_EmployeeId_VehicleId] FOREIGN KEY ([VehicleTripId], [EmployeeId], [VehicleId]) REFERENCES [hr].[VehicleTrips] ([Id], [EmployeeId], [VehicleId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VehicleMileageEntries_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [hr].[Vehicles] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ZoneAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [ZoneVersionId] uniqueidentifier NOT NULL,
    [PopulationKind] int NOT NULL,
    [EmployeeId] uniqueidentifier NULL,
    [RoleId] uniqueidentifier NULL,
    [TeamId] uniqueidentifier NULL,
    [SiteId] uniqueidentifier NULL,
    [IsEmployeeOverride] bit NOT NULL,
    [EffectiveFromUtc] datetimeoffset(7) NOT NULL,
    [EffectiveToUtc] datetimeoffset(7) NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [ApprovedBy] nvarchar(200) NOT NULL,
    [ApprovedAtUtc] datetimeoffset(7) NOT NULL,
    CONSTRAINT [PK_ZoneAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_ZoneAssignments_ZoneVersionId_Id] UNIQUE ([ZoneVersionId], [Id]),
    CONSTRAINT [CK_ZoneAssignments_ApprovedAtUtc_Utc] CHECK ([ApprovedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ApprovedAtUtc]) = 0),
    CONSTRAINT [CK_ZoneAssignments_EffectiveFromUtc_Utc] CHECK ([EffectiveFromUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveFromUtc]) = 0),
    CONSTRAINT [CK_ZoneAssignments_EffectiveToUtc_Utc] CHECK ([EffectiveToUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveToUtc]) = 0),
    CONSTRAINT [CK_ZoneAssignments_Override] CHECK ([IsEmployeeOverride] = 0 OR [PopulationKind] = 1),
    CONSTRAINT [CK_ZoneAssignments_Population] CHECK (([PopulationKind] = 1 AND [EmployeeId] IS NOT NULL AND [RoleId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL) OR ([PopulationKind] = 2 AND [EmployeeId] IS NULL AND [RoleId] IS NOT NULL AND [TeamId] IS NULL AND [SiteId] IS NULL) OR ([PopulationKind] = 3 AND [EmployeeId] IS NULL AND [RoleId] IS NULL AND [TeamId] IS NOT NULL AND [SiteId] IS NULL) OR ([PopulationKind] = 4 AND [EmployeeId] IS NULL AND [RoleId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NOT NULL)),
    CONSTRAINT [CK_ZoneAssignments_Window] CHECK ([EffectiveToUtc] > [EffectiveFromUtc]),
    CONSTRAINT [FK_ZoneAssignments_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneAssignments_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [auth].[Roles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneAssignments_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneAssignments_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [auth].[Teams] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneAssignments_ZoneVersions_ZoneVersionId] FOREIGN KEY ([ZoneVersionId]) REFERENCES [attendance].[ZoneVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ZoneExceptionRequests] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ZoneVersionId] uniqueidentifier NOT NULL,
    [StartsAtUtc] datetimeoffset(7) NOT NULL,
    [EndsAtUtc] datetimeoffset(7) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    [RequestedBy] nvarchar(200) NOT NULL,
    [RequestedAtUtc] datetimeoffset(7) NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [TaskReference] nvarchar(200) NULL,
    CONSTRAINT [PK_ZoneExceptionRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ZoneExceptionRequests_EndsAtUtc_Utc] CHECK ([EndsAtUtc] IS NULL OR DATEPART(TZOFFSET, [EndsAtUtc]) = 0),
    CONSTRAINT [CK_ZoneExceptionRequests_RequestedAtUtc_Utc] CHECK ([RequestedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedAtUtc]) = 0),
    CONSTRAINT [CK_ZoneExceptionRequests_StartsAtUtc_Utc] CHECK ([StartsAtUtc] IS NULL OR DATEPART(TZOFFSET, [StartsAtUtc]) = 0),
    CONSTRAINT [CK_ZoneExceptionRequests_Window] CHECK ([EndsAtUtc] > [StartsAtUtc]),
    CONSTRAINT [FK_ZoneExceptionRequests_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneExceptionRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneExceptionRequests_ZoneVersions_ZoneVersionId] FOREIGN KEY ([ZoneVersionId]) REFERENCES [attendance].[ZoneVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[RepaymentScheduleEntries] (
    [Id] uniqueidentifier NOT NULL,
    [AdvanceLoanAgreementId] uniqueidentifier NOT NULL,
    [Sequence] int NOT NULL,
    [DueDate] date NOT NULL,
    [PrincipalAmount] decimal(18,2) NOT NULL,
    [OtherApprovedAmount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    CONSTRAINT [PK_RepaymentScheduleEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_RepaymentScheduleEntries_Id_AdvanceLoanAgreementId_Currency] UNIQUE ([Id], [AdvanceLoanAgreementId], [Currency]),
    CONSTRAINT [CK_RepaymentScheduleEntries_1] CHECK ([Sequence] > 0),
    CONSTRAINT [CK_RepaymentScheduleEntries_2] CHECK ([PrincipalAmount] >= 0 AND [OtherApprovedAmount] >= 0 AND ([PrincipalAmount] + [OtherApprovedAmount]) > 0),
    CONSTRAINT [CK_RepaymentScheduleEntries_3] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [FK_RepaymentScheduleEntries_AdvanceLoanAgreements_AdvanceLoanAgreementId_Currency] FOREIGN KEY ([AdvanceLoanAgreementId], [Currency]) REFERENCES [hr].[AdvanceLoanAgreements] ([Id], [Currency]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[AttendanceCalculationLines] (
    [Id] uniqueidentifier NOT NULL,
    [DailyAttendanceSummaryId] uniqueidentifier NOT NULL,
    [Sequence] int NOT NULL,
    [RuleCode] nvarchar(100) NOT NULL,
    [Explanation] nvarchar(2000) NOT NULL,
    [InputQuantity] decimal(18,4) NOT NULL,
    [ResultQuantity] decimal(18,4) NOT NULL,
    [UnitCode] nvarchar(30) NOT NULL,
    [MonetaryAmount] decimal(18,2) NULL,
    [Currency] varchar(3) NULL,
    CONSTRAINT [PK_AttendanceCalculationLines] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_AttendanceCalculationLines_Currency] CHECK (([MonetaryAmount] IS NULL AND [Currency] IS NULL) OR ([MonetaryAmount] IS NOT NULL AND [Currency] IS NOT NULL AND LEN([Currency]) = 3)),
    CONSTRAINT [CK_AttendanceCalculationLines_Sequence] CHECK ([Sequence] > 0),
    CONSTRAINT [FK_AttendanceCalculationLines_DailyAttendanceSummaries_DailyAttendanceSummaryId] FOREIGN KEY ([DailyAttendanceSummaryId]) REFERENCES [attendance].[DailyAttendanceSummaries] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[AttendanceCorrections] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [AttendanceDate] date NOT NULL,
    [Kind] int NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [RequestedBy] nvarchar(200) NOT NULL,
    [RequestedAtUtc] datetimeoffset(7) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    [PrivateEvidenceReference] nvarchar(500) NOT NULL,
    [OriginalTimeUtc] datetimeoffset(7) NULL,
    [RequestedTimeUtc] datetimeoffset(7) NULL,
    [RequestedEndUtc] datetimeoffset(7) NULL,
    [OriginalStatus] int NULL,
    [RequestedStatus] int NULL,
    [RawPunchEventId] uniqueidentifier NULL,
    [OriginalSummaryId] uniqueidentifier NULL,
    [AdjustmentPeriodLockId] uniqueidentifier NULL,
    CONSTRAINT [PK_AttendanceCorrections] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_AttendanceCorrections_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]),
    CONSTRAINT [CK_AttendanceCorrections_Kind] CHECK ([Kind] BETWEEN 1 AND 4),
    CONSTRAINT [CK_AttendanceCorrections_OriginalTimeUtc_Utc] CHECK ([OriginalTimeUtc] IS NULL OR DATEPART(TZOFFSET, [OriginalTimeUtc]) = 0),
    CONSTRAINT [CK_AttendanceCorrections_RequestedAtUtc_Utc] CHECK ([RequestedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedAtUtc]) = 0),
    CONSTRAINT [CK_AttendanceCorrections_RequestedEndUtc_Utc] CHECK ([RequestedEndUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedEndUtc]) = 0),
    CONSTRAINT [CK_AttendanceCorrections_RequestedTimeUtc_Utc] CHECK ([RequestedTimeUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedTimeUtc]) = 0),
    CONSTRAINT [CK_AttendanceCorrections_Status] CHECK (([OriginalStatus] IS NULL OR [OriginalStatus] BETWEEN 1 AND 9) AND ([RequestedStatus] IS NULL OR [RequestedStatus] BETWEEN 1 AND 9)),
    CONSTRAINT [CK_AttendanceCorrections_TypedChange] CHECK (([Kind] = 3 AND [RequestedStatus] IS NOT NULL AND [RequestedTimeUtc] IS NULL AND [RequestedEndUtc] IS NULL) OR ([Kind] IN (1,2) AND [RequestedTimeUtc] IS NOT NULL AND [RequestedEndUtc] IS NULL AND [RequestedStatus] IS NULL) OR ([Kind] = 4 AND [RequestedTimeUtc] IS NOT NULL AND [RequestedEndUtc] IS NOT NULL AND [RequestedEndUtc] > [RequestedTimeUtc] AND [RequestedStatus] IS NULL)),
    CONSTRAINT [FK_AttendanceCorrections_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceCorrections_AttendancePeriodLocks_AdjustmentPeriodLockId] FOREIGN KEY ([AdjustmentPeriodLockId]) REFERENCES [attendance].[AttendancePeriodLocks] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceCorrections_DailyAttendanceSummaries_EmployeeId_AttendanceDate_OriginalSummaryId] FOREIGN KEY ([EmployeeId], [AttendanceDate], [OriginalSummaryId]) REFERENCES [attendance].[DailyAttendanceSummaries] ([EmployeeId], [AttendanceDate], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceCorrections_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceCorrections_RawPunchEvents_EmployeeId_RawPunchEventId] FOREIGN KEY ([EmployeeId], [RawPunchEventId]) REFERENCES [attendance].[RawPunchEvents] ([EmployeeId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[AttendancePeriodSummaries] (
    [Id] uniqueidentifier NOT NULL,
    [AttendancePeriodLockEventId] uniqueidentifier NOT NULL,
    [DailyAttendanceSummaryId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [AttendanceDate] date NOT NULL,
    CONSTRAINT [PK_AttendancePeriodSummaries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AttendancePeriodSummaries_AttendancePeriodLockEvents_AttendancePeriodLockEventId] FOREIGN KEY ([AttendancePeriodLockEventId]) REFERENCES [attendance].[AttendancePeriodLockEvents] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendancePeriodSummaries_DailyAttendanceSummaries_EmployeeId_AttendanceDate_DailyAttendanceSummaryId] FOREIGN KEY ([EmployeeId], [AttendanceDate], [DailyAttendanceSummaryId]) REFERENCES [attendance].[DailyAttendanceSummaries] ([EmployeeId], [AttendanceDate], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[LeaveCancellationRequests] (
    [Id] uniqueidentifier NOT NULL,
    [LeaveRequestId] uniqueidentifier NOT NULL,
    [RequestedByUserId] nvarchar(200) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [SubmittedAt] datetimeoffset NULL,
    CONSTRAINT [PK_LeaveCancellationRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_LeaveCancellationRequests_1] CHECK (([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)),
    CONSTRAINT [FK_LeaveCancellationRequests_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveCancellationRequests_LeaveRequests_LeaveRequestId] FOREIGN KEY ([LeaveRequestId]) REFERENCES [hr].[LeaveRequests] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[LeaveLedgerEntries] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [LeaveTypeId] uniqueidentifier NOT NULL,
    [LeaveYearStart] date NOT NULL,
    [LeaveYearEnd] date NOT NULL,
    [EntryType] int NOT NULL,
    [MinutesDelta] int NOT NULL,
    [LeaveRequestId] uniqueidentifier NULL,
    [ReversesEntryId] uniqueidentifier NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [IdempotencyKey] nvarchar(100) NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_LeaveLedgerEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_LeaveLedgerEntries_Id_EmployeeId_LeaveTypeId_LeaveYearStart_LeaveYearEnd] UNIQUE ([Id], [EmployeeId], [LeaveTypeId], [LeaveYearStart], [LeaveYearEnd]),
    CONSTRAINT [CK_LeaveLedgerEntries_1] CHECK ([LeaveYearEnd] > [LeaveYearStart]),
    CONSTRAINT [CK_LeaveLedgerEntries_2] CHECK ([MinutesDelta] <> 0),
    CONSTRAINT [CK_LeaveLedgerEntries_3] CHECK ([EntryType] BETWEEN 1 AND 6),
    CONSTRAINT [CK_LeaveLedgerEntries_4] CHECK ([ReversesEntryId] IS NULL OR [ReversesEntryId] <> [Id]),
    CONSTRAINT [FK_LeaveLedgerEntries_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveLedgerEntries_LeaveLedgerEntries_ReversesEntryId_EmployeeId_LeaveTypeId_LeaveYearStart_LeaveYearEnd] FOREIGN KEY ([ReversesEntryId], [EmployeeId], [LeaveTypeId], [LeaveYearStart], [LeaveYearEnd]) REFERENCES [hr].[LeaveLedgerEntries] ([Id], [EmployeeId], [LeaveTypeId], [LeaveYearStart], [LeaveYearEnd]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveLedgerEntries_LeaveRequests_LeaveRequestId_EmployeeId_LeaveTypeId] FOREIGN KEY ([LeaveRequestId], [EmployeeId], [LeaveTypeId]) REFERENCES [hr].[LeaveRequests] ([Id], [EmployeeId], [LeaveTypeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveLedgerEntries_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId]) REFERENCES [hr].[LeaveTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveLedgerEntries_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[LeaveRecoveryCommitments] (
    [Id] uniqueidentifier NOT NULL,
    [LeaveRequestId] uniqueidentifier NOT NULL,
    [CommitmentDate] date NOT NULL,
    [CommittedMinutes] int NOT NULL,
    [BeforeLeave] bit NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_LeaveRecoveryCommitments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_LeaveRecoveryCommitments_1] CHECK ([CommittedMinutes] > 0),
    CONSTRAINT [FK_LeaveRecoveryCommitments_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveRecoveryCommitments_LeaveRequests_LeaveRequestId] FOREIGN KEY ([LeaveRequestId]) REFERENCES [hr].[LeaveRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveRecoveryCommitments_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[PolicyAssignmentEnds] (
    [Id] uniqueidentifier NOT NULL,
    [PolicyAssignmentId] uniqueidentifier NOT NULL,
    [EffectiveTo] date NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PolicyAssignmentEnds] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PolicyAssignmentEnds_Reason] CHECK (LEN(LTRIM(RTRIM([Reason]))) > 0),
    CONSTRAINT [FK_PolicyAssignmentEnds_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PolicyAssignmentEnds_PolicyAssignments_PolicyAssignmentId] FOREIGN KEY ([PolicyAssignmentId]) REFERENCES [hr].[PolicyAssignments] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[DisciplinaryCases] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ReasonCode] nvarchar(60) NOT NULL,
    [ProtectedEvidenceReference] nvarchar(500) NOT NULL,
    [PolicyImpactAssessmentId] uniqueidentifier NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [OpenedByUserId] nvarchar(200) NOT NULL,
    [OpenedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [State] int NOT NULL,
    [ClosedAt] datetimeoffset NULL,
    [ApprovedOutcomePrivateReference] nvarchar(500) NULL,
    CONSTRAINT [PK_DisciplinaryCases] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_DisciplinaryCases_1] CHECK ([State] BETWEEN 0 AND 2),
    CONSTRAINT [CK_DisciplinaryCases_2] CHECK ([ClosedAt] IS NULL OR [ClosedAt] >= [OpenedAt]),
    CONSTRAINT [CK_DisciplinaryCases_3] CHECK (([State] <> 2 AND [ClosedAt] IS NULL AND [ApprovedOutcomePrivateReference] IS NULL) OR ([State] = 2 AND [ClosedAt] IS NOT NULL AND [ApprovedOutcomePrivateReference] IS NOT NULL)),
    CONSTRAINT [FK_DisciplinaryCases_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_DisciplinaryCases_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_DisciplinaryCases_PolicyImpactAssessments_PolicyImpactAssessmentId_EmployeeId] FOREIGN KEY ([PolicyImpactAssessmentId], [EmployeeId]) REFERENCES [hr].[PolicyImpactAssessments] ([Id], [EmployeeId]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[DutyMonitoringWindows] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ZoneAssignmentId] uniqueidentifier NOT NULL,
    [StartsAtUtc] datetimeoffset(7) NOT NULL,
    [EndsAtUtc] datetimeoffset(7) NOT NULL,
    [NoticeShownAtUtc] datetimeoffset(7) NOT NULL,
    [RetentionPolicyVersionId] uniqueidentifier NOT NULL,
    [DutyReference] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_DutyMonitoringWindows] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_DutyMonitoringWindows_EmployeeId_ZoneAssignmentId_Id] UNIQUE ([EmployeeId], [ZoneAssignmentId], [Id]),
    CONSTRAINT [CK_DutyMonitoringWindows_EndsAtUtc_Utc] CHECK ([EndsAtUtc] IS NULL OR DATEPART(TZOFFSET, [EndsAtUtc]) = 0),
    CONSTRAINT [CK_DutyMonitoringWindows_NoticeShownAtUtc_Utc] CHECK ([NoticeShownAtUtc] IS NULL OR DATEPART(TZOFFSET, [NoticeShownAtUtc]) = 0),
    CONSTRAINT [CK_DutyMonitoringWindows_StartsAtUtc_Utc] CHECK ([StartsAtUtc] IS NULL OR DATEPART(TZOFFSET, [StartsAtUtc]) = 0),
    CONSTRAINT [CK_DutyMonitoringWindows_Window] CHECK ([EndsAtUtc] > [StartsAtUtc] AND [NoticeShownAtUtc] <= [StartsAtUtc]),
    CONSTRAINT [FK_DutyMonitoringWindows_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_DutyMonitoringWindows_PolicyVersions_RetentionPolicyVersionId] FOREIGN KEY ([RetentionPolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_DutyMonitoringWindows_ZoneAssignments_ZoneAssignmentId] FOREIGN KEY ([ZoneAssignmentId]) REFERENCES [attendance].[ZoneAssignments] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[RepaymentLedgerEntries] (
    [Id] uniqueidentifier NOT NULL,
    [AdvanceLoanAgreementId] uniqueidentifier NOT NULL,
    [RepaymentScheduleEntryId] uniqueidentifier NULL,
    [Kind] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [ValueDate] date NOT NULL,
    [SourceReference] nvarchar(150) NOT NULL,
    [IdempotencyKey] nvarchar(100) NOT NULL,
    [ReversesEntryId] uniqueidentifier NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_RepaymentLedgerEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_RepaymentLedgerEntries_Id_AdvanceLoanAgreementId_Currency] UNIQUE ([Id], [AdvanceLoanAgreementId], [Currency]),
    CONSTRAINT [CK_RepaymentLedgerEntries_1] CHECK ([Amount] <> 0),
    CONSTRAINT [CK_RepaymentLedgerEntries_2] CHECK ([Kind] BETWEEN 1 AND 4),
    CONSTRAINT [CK_RepaymentLedgerEntries_3] CHECK ([ReversesEntryId] IS NULL OR [ReversesEntryId] <> [Id]),
    CONSTRAINT [CK_RepaymentLedgerEntries_4] CHECK (([Kind] IN (1, 2) AND [Amount] > 0) OR ([Kind] = 3 AND [Amount] < 0) OR [Kind] = 4),
    CONSTRAINT [CK_RepaymentLedgerEntries_5] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [CK_RepaymentLedgerEntries_ReversalSource] CHECK ([Kind] <> 3 OR [ReversesEntryId] IS NOT NULL),
    CONSTRAINT [FK_RepaymentLedgerEntries_AdvanceLoanAgreements_AdvanceLoanAgreementId_Currency] FOREIGN KEY ([AdvanceLoanAgreementId], [Currency]) REFERENCES [hr].[AdvanceLoanAgreements] ([Id], [Currency]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RepaymentLedgerEntries_RepaymentLedgerEntries_ReversesEntryId_AdvanceLoanAgreementId_Currency] FOREIGN KEY ([ReversesEntryId], [AdvanceLoanAgreementId], [Currency]) REFERENCES [hr].[RepaymentLedgerEntries] ([Id], [AdvanceLoanAgreementId], [Currency]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RepaymentLedgerEntries_RepaymentScheduleEntries_RepaymentScheduleEntryId_AdvanceLoanAgreementId_Currency] FOREIGN KEY ([RepaymentScheduleEntryId], [AdvanceLoanAgreementId], [Currency]) REFERENCES [hr].[RepaymentScheduleEntries] ([Id], [AdvanceLoanAgreementId], [Currency]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[WorkSessions] (
    [Id] uniqueidentifier NOT NULL,
    [SessionKey] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [AttendanceDate] date NOT NULL,
    [StartsAtUtc] datetimeoffset(7) NOT NULL,
    [EndsAtUtc] datetimeoffset(7) NULL,
    [CheckInPunchId] uniqueidentifier NULL,
    [CheckOutPunchId] uniqueidentifier NULL,
    [CorrectionId] uniqueidentifier NULL,
    [SiteId] uniqueidentifier NULL,
    [ShiftVersionId] uniqueidentifier NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [CalculatedAtUtc] datetimeoffset(7) NOT NULL,
    [EndsContinuousDuty] bit NOT NULL,
    CONSTRAINT [PK_WorkSessions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_WorkSessions_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]),
    CONSTRAINT [AK_WorkSessions_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]),
    CONSTRAINT [CK_WorkSessions_CalculatedAtUtc_Utc] CHECK ([CalculatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CalculatedAtUtc]) = 0),
    CONSTRAINT [CK_WorkSessions_CheckOut] CHECK ([CheckOutPunchId] IS NULL OR [EndsAtUtc] IS NOT NULL),
    CONSTRAINT [CK_WorkSessions_ContinuousDuty] CHECK ([EndsContinuousDuty] = 0 OR [EndsAtUtc] IS NOT NULL),
    CONSTRAINT [CK_WorkSessions_EndsAtUtc_Utc] CHECK ([EndsAtUtc] IS NULL OR DATEPART(TZOFFSET, [EndsAtUtc]) = 0),
    CONSTRAINT [CK_WorkSessions_Evidence] CHECK ([CorrectionId] IS NOT NULL OR ([CheckInPunchId] IS NOT NULL AND ([EndsAtUtc] IS NULL OR [CheckOutPunchId] IS NOT NULL))),
    CONSTRAINT [CK_WorkSessions_Revision] CHECK ([Revision] > 0),
    CONSTRAINT [CK_WorkSessions_StartsAtUtc_Utc] CHECK ([StartsAtUtc] IS NULL OR DATEPART(TZOFFSET, [StartsAtUtc]) = 0),
    CONSTRAINT [CK_WorkSessions_Window] CHECK ([EndsAtUtc] IS NULL OR [EndsAtUtc] > [StartsAtUtc]),
    CONSTRAINT [FK_WorkSessions_AttendanceCorrections_EmployeeId_CorrectionId] FOREIGN KEY ([EmployeeId], [CorrectionId]) REFERENCES [attendance].[AttendanceCorrections] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkSessions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkSessions_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkSessions_RawPunchEvents_EmployeeId_CheckInPunchId] FOREIGN KEY ([EmployeeId], [CheckInPunchId]) REFERENCES [attendance].[RawPunchEvents] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkSessions_RawPunchEvents_EmployeeId_CheckOutPunchId] FOREIGN KEY ([EmployeeId], [CheckOutPunchId]) REFERENCES [attendance].[RawPunchEvents] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkSessions_ShiftVersions_ShiftVersionId] FOREIGN KEY ([ShiftVersionId]) REFERENCES [attendance].[ShiftVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkSessions_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[AllowanceClaims] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [EmployeeAssignmentId] uniqueidentifier NOT NULL,
    [DeploymentCategoryCode] nvarchar(60) NOT NULL,
    [DeploymentApprovalRequestId] uniqueidentifier NULL,
    [WorkSessionId] uniqueidentifier NULL,
    [EligibilityPeriodStart] datetimeoffset NOT NULL,
    [EligibilityPeriodEnd] datetimeoffset NOT NULL,
    [EligibilityKey] nvarchar(120) NOT NULL,
    [ClaimedAmount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [PayableDate] date NOT NULL,
    [PayrollPeriodStart] date NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [CalculationPrivateReference] nvarchar(500) NOT NULL,
    [EvidencePrivateReference] nvarchar(500) NULL,
    [RequestedByUserId] nvarchar(200) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [SubmittedAt] datetimeoffset NULL,
    CONSTRAINT [PK_AllowanceClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_AllowanceClaims_1] CHECK ([EligibilityPeriodEnd] > [EligibilityPeriodStart]),
    CONSTRAINT [CK_AllowanceClaims_2] CHECK ([Kind] BETWEEN 1 AND 6),
    CONSTRAINT [CK_AllowanceClaims_3] CHECK ([ClaimedAmount] >= 0),
    CONSTRAINT [CK_AllowanceClaims_4] CHECK (([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)),
    CONSTRAINT [CK_AllowanceClaims_5] CHECK ([Kind] <> 5 OR [DeploymentApprovalRequestId] IS NOT NULL),
    CONSTRAINT [CK_AllowanceClaims_6] CHECK ([Kind] NOT BETWEEN 1 AND 4 OR [WorkSessionId] IS NOT NULL),
    CONSTRAINT [CK_AllowanceClaims_7] CHECK ([Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')),
    CONSTRAINT [FK_AllowanceClaims_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllowanceClaims_ApprovalRequests_DeploymentApprovalRequestId] FOREIGN KEY ([DeploymentApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllowanceClaims_EmployeeAssignments_EmployeeAssignmentId_EmployeeId] FOREIGN KEY ([EmployeeAssignmentId], [EmployeeId]) REFERENCES [hr].[EmployeeAssignments] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllowanceClaims_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllowanceClaims_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllowanceClaims_WorkSessions_WorkSessionId_EmployeeId] FOREIGN KEY ([WorkSessionId], [EmployeeId]) REFERENCES [attendance].[WorkSessions] ([Id], [EmployeeId]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[AttendanceExceptions] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [AttendanceDate] date NOT NULL,
    [Kind] int NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [DetectedAtUtc] datetimeoffset(7) NOT NULL,
    [RawPunchEventId] uniqueidentifier NULL,
    [WorkSessionId] uniqueidentifier NULL,
    [DailyAttendanceSummaryId] uniqueidentifier NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    CONSTRAINT [PK_AttendanceExceptions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_AttendanceExceptions_DetectedAtUtc_Utc] CHECK ([DetectedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DetectedAtUtc]) = 0),
    CONSTRAINT [CK_AttendanceExceptions_Kind] CHECK ([Kind] BETWEEN 1 AND 10),
    CONSTRAINT [FK_AttendanceExceptions_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceExceptions_DailyAttendanceSummaries_EmployeeId_DailyAttendanceSummaryId] FOREIGN KEY ([EmployeeId], [DailyAttendanceSummaryId]) REFERENCES [attendance].[DailyAttendanceSummaries] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceExceptions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceExceptions_RawPunchEvents_EmployeeId_RawPunchEventId] FOREIGN KEY ([EmployeeId], [RawPunchEventId]) REFERENCES [attendance].[RawPunchEvents] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceExceptions_WorkSessions_EmployeeId_WorkSessionId] FOREIGN KEY ([EmployeeId], [WorkSessionId]) REFERENCES [attendance].[WorkSessions] ([EmployeeId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[AttendanceSummarySessions] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [DailyAttendanceSummaryId] uniqueidentifier NOT NULL,
    [WorkSessionId] uniqueidentifier NOT NULL,
    [AllocatedMinutes] int NOT NULL,
    CONSTRAINT [PK_AttendanceSummarySessions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_AttendanceSummarySessions_Minutes] CHECK ([AllocatedMinutes] >= 0),
    CONSTRAINT [FK_AttendanceSummarySessions_DailyAttendanceSummaries_EmployeeId_DailyAttendanceSummaryId] FOREIGN KEY ([EmployeeId], [DailyAttendanceSummaryId]) REFERENCES [attendance].[DailyAttendanceSummaries] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceSummarySessions_WorkSessions_EmployeeId_WorkSessionId] FOREIGN KEY ([EmployeeId], [WorkSessionId]) REFERENCES [attendance].[WorkSessions] ([EmployeeId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[OvertimeRequests] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [WorkSessionId] uniqueidentifier NOT NULL,
    [StartsAtUtc] datetimeoffset(7) NOT NULL,
    [EndsAtUtc] datetimeoffset(7) NOT NULL,
    [RequestedMinutes] int NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [RateRuleCode] nvarchar(100) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    [PrivateEvidenceReference] nvarchar(500) NOT NULL,
    [RequestedBy] nvarchar(200) NOT NULL,
    [RequestedAtUtc] datetimeoffset(7) NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [SiteId] uniqueidentifier NULL,
    [JobReference] nvarchar(200) NULL,
    [BulkSubmissionId] uniqueidentifier NULL,
    CONSTRAINT [PK_OvertimeRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_OvertimeRequests_EndsAtUtc_Utc] CHECK ([EndsAtUtc] IS NULL OR DATEPART(TZOFFSET, [EndsAtUtc]) = 0),
    CONSTRAINT [CK_OvertimeRequests_Minutes] CHECK ([RequestedMinutes] > 0 AND CAST([RequestedMinutes] AS bigint) * 600000000 <= DATEDIFF_BIG(microsecond, [StartsAtUtc], [EndsAtUtc]) * 10),
    CONSTRAINT [CK_OvertimeRequests_RequestedAtUtc_Utc] CHECK ([RequestedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedAtUtc]) = 0),
    CONSTRAINT [CK_OvertimeRequests_StartsAtUtc_Utc] CHECK ([StartsAtUtc] IS NULL OR DATEPART(TZOFFSET, [StartsAtUtc]) = 0),
    CONSTRAINT [CK_OvertimeRequests_Window] CHECK ([EndsAtUtc] > [StartsAtUtc]),
    CONSTRAINT [FK_OvertimeRequests_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OvertimeRequests_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OvertimeRequests_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OvertimeRequests_WorkSessions_EmployeeId_WorkSessionId] FOREIGN KEY ([EmployeeId], [WorkSessionId]) REFERENCES [attendance].[WorkSessions] ([EmployeeId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[AllowanceSettlementRecords] (
    [Id] uniqueidentifier NOT NULL,
    [AllowanceClaimId] uniqueidentifier NOT NULL,
    [State] int NOT NULL,
    [PayrollHandoffLineId] uniqueidentifier NULL,
    [ExternalPaymentReference] nvarchar(150) NULL,
    [IdempotencyKey] nvarchar(100) NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AllowanceSettlementRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_AllowanceSettlementRecords_1] CHECK ([State] BETWEEN 0 AND 2),
    CONSTRAINT [CK_AllowanceSettlementRecords_2] CHECK ([State] <> 1 OR [PayrollHandoffLineId] IS NOT NULL),
    CONSTRAINT [CK_AllowanceSettlementRecords_3] CHECK ([State] <> 2 OR [ExternalPaymentReference] IS NOT NULL),
    CONSTRAINT [FK_AllowanceSettlementRecords_AllowanceClaims_AllowanceClaimId] FOREIGN KEY ([AllowanceClaimId]) REFERENCES [hr].[AllowanceClaims] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllowanceSettlementRecords_PayrollHandoffLines_PayrollHandoffLineId] FOREIGN KEY ([PayrollHandoffLineId]) REFERENCES [hr].[PayrollHandoffLines] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[AccountSetupInvitations] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [ApprovalDecisionId] uniqueidentifier NOT NULL,
    [TokenHash] varchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Channel] nvarchar(60) NOT NULL,
    [ProtectedDestinationReference] nvarchar(500) NOT NULL,
    [IssuedByUserId] uniqueidentifier NOT NULL,
    [IssuedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [ConsumedAt] datetimeoffset NULL,
    [RevokedAt] datetimeoffset NULL,
    [DeliveryStatus] int NOT NULL,
    [DeliveryAttempts] int NOT NULL,
    [LastDeliveryAttemptAt] datetimeoffset NULL,
    [NextDeliveryAttemptAt] datetimeoffset NULL,
    [LastDeliveryFailureCode] nvarchar(100) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_AccountSetupInvitations] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_AccountSetupInvitations_UserAccountId_Id] UNIQUE ([UserAccountId], [Id]),
    CONSTRAINT [CK_AccountSetupInvitations_Dates] CHECK ([ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt])) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [IssuedAt])),
    CONSTRAINT [CK_AccountSetupInvitations_Delivery] CHECK ([DeliveryStatus] BETWEEN 0 AND 2 AND [DeliveryAttempts] >= 0),
    CONSTRAINT [FK_AccountSetupInvitations_EmployeeOnboardingDecisions_ApprovalDecisionId] FOREIGN KEY ([ApprovalDecisionId]) REFERENCES [hr].[EmployeeOnboardingDecisions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[FaceVerificationAttempts] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [FaceEnrollmentId] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [RegisteredDeviceKey] uniqueidentifier NOT NULL,
    [RegisteredDeviceId] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [PunchIdempotencyKey] nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ChallengeSha256] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Provider] nvarchar(100) NOT NULL,
    [ProviderVersion] nvarchar(100) NOT NULL,
    [CorrelationId] nvarchar(100) NOT NULL,
    [IssuedAtUtc] datetimeoffset(7) NOT NULL,
    [ExpiresAtUtc] datetimeoffset(7) NOT NULL,
    [Outcome] int NOT NULL,
    [LivenessPassed] bit NULL,
    [MatchPassed] bit NULL,
    [ResultReasonCode] nvarchar(100) NULL,
    [CompletedAtUtc] datetimeoffset(7) NULL,
    [ConsumedAtUtc] datetimeoffset(7) NULL,
    [ConsumedByPunchId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_FaceVerificationAttempts] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_FaceVerificationAttempts_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]),
    CONSTRAINT [CK_FaceVerificationAttempts_CompletedAtUtc_Utc] CHECK ([CompletedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CompletedAtUtc]) = 0),
    CONSTRAINT [CK_FaceVerificationAttempts_Completion] CHECK (([Outcome] = 1 AND [CompletedAtUtc] IS NULL AND [LivenessPassed] IS NULL AND [MatchPassed] IS NULL AND [ResultReasonCode] IS NULL) OR ([Outcome] <> 1 AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [IssuedAtUtc] AND [CompletedAtUtc] < [ExpiresAtUtc] AND [LivenessPassed] IS NOT NULL AND [MatchPassed] IS NOT NULL AND [ResultReasonCode] IS NOT NULL)),
    CONSTRAINT [CK_FaceVerificationAttempts_ConsumedAtUtc_Utc] CHECK ([ConsumedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ConsumedAtUtc]) = 0),
    CONSTRAINT [CK_FaceVerificationAttempts_Consumption] CHECK (([ConsumedAtUtc] IS NULL AND [ConsumedByPunchId] IS NULL) OR ([ConsumedAtUtc] IS NOT NULL AND [ConsumedByPunchId] IS NOT NULL AND [Outcome] = 2 AND [ConsumedAtUtc] >= [CompletedAtUtc] AND [ConsumedAtUtc] < [ExpiresAtUtc])),
    CONSTRAINT [CK_FaceVerificationAttempts_Digest] CHECK (LEN([ChallengeSha256]) = 64 AND [ChallengeSha256] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_FaceVerificationAttempts_ExpiresAtUtc_Utc] CHECK ([ExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [ExpiresAtUtc]) = 0),
    CONSTRAINT [CK_FaceVerificationAttempts_IssuedAtUtc_Utc] CHECK ([IssuedAtUtc] IS NULL OR DATEPART(TZOFFSET, [IssuedAtUtc]) = 0),
    CONSTRAINT [CK_FaceVerificationAttempts_Outcome] CHECK ([Outcome] BETWEEN 1 AND 4),
    CONSTRAINT [CK_FaceVerificationAttempts_Passed] CHECK ([Outcome] <> 2 OR ([LivenessPassed] = 1 AND [MatchPassed] = 1)),
    CONSTRAINT [CK_FaceVerificationAttempts_Window] CHECK ([ExpiresAtUtc] > [IssuedAtUtc]),
    CONSTRAINT [FK_FaceVerificationAttempts_FaceEnrollments_EmployeeId_FaceEnrollmentId] FOREIGN KEY ([EmployeeId], [FaceEnrollmentId]) REFERENCES [attendance].[FaceEnrollments] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FaceVerificationAttempts_RawPunchEvents_EmployeeId_RegisteredDeviceId_PunchIdempotencyKey_ConsumedByPunchId] FOREIGN KEY ([EmployeeId], [RegisteredDeviceId], [PunchIdempotencyKey], [ConsumedByPunchId]) REFERENCES [attendance].[RawPunchEvents] ([EmployeeId], [RegisteredDeviceId], [IdempotencyKey], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[PunchAssessments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [RawPunchEventId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [Outcome] int NOT NULL,
    [ReasonCode] nvarchar(100) NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [CalculationVersion] nvarchar(100) NOT NULL,
    [AssessedAtUtc] datetimeoffset(7) NOT NULL,
    [FaceVerificationAttemptId] uniqueidentifier NULL,
    [GeofenceDistanceMeters] decimal(10,2) NULL,
    [LocationReliable] bit NULL,
    [InsideGeofence] bit NULL,
    [DeviceRiskCode] nvarchar(100) NULL,
    CONSTRAINT [PK_PunchAssessments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PunchAssessments_AssessedAtUtc_Utc] CHECK ([AssessedAtUtc] IS NULL OR DATEPART(TZOFFSET, [AssessedAtUtc]) = 0),
    CONSTRAINT [CK_PunchAssessments_Distance] CHECK ([GeofenceDistanceMeters] IS NULL OR [GeofenceDistanceMeters] >= 0),
    CONSTRAINT [CK_PunchAssessments_Outcome] CHECK ([Outcome] BETWEEN 1 AND 5),
    CONSTRAINT [CK_PunchAssessments_Revision] CHECK ([Revision] > 0),
    CONSTRAINT [FK_PunchAssessments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PunchAssessments_FaceVerificationAttempts_EmployeeId_FaceVerificationAttemptId] FOREIGN KEY ([EmployeeId], [FaceVerificationAttemptId]) REFERENCES [attendance].[FaceVerificationAttempts] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PunchAssessments_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PunchAssessments_RawPunchEvents_EmployeeId_RawPunchEventId] FOREIGN KEY ([EmployeeId], [RawPunchEventId]) REFERENCES [attendance].[RawPunchEvents] ([EmployeeId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[MfaChallenges] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [RegisteredDeviceId] uniqueidentifier NULL,
    [VerifiedCredentialId] uniqueidentifier NULL,
    [SecurityVersion] int NOT NULL,
    [ChallengeHash] varchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [IssuedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [VerifiedAt] datetimeoffset NULL,
    [ConsumedAt] datetimeoffset NULL,
    [RevokedAt] datetimeoffset NULL,
    [FailedAttempts] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_MfaChallenges] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_MfaChallenges_UserAccountId_Id] UNIQUE ([UserAccountId], [Id]),
    CONSTRAINT [CK_MfaChallenges_Counters] CHECK ([SecurityVersion] > 0 AND [FailedAttempts] >= 0),
    CONSTRAINT [CK_MfaChallenges_Lifetime] CHECK ([ExpiresAt] > [IssuedAt] AND ([VerifiedAt] IS NULL OR ([VerifiedAt] >= [IssuedAt] AND [VerifiedAt] < [ExpiresAt])) AND ([ConsumedAt] IS NULL OR ([VerifiedAt] IS NOT NULL AND [ConsumedAt] >= [VerifiedAt] AND [ConsumedAt] < [ExpiresAt]))),
    CONSTRAINT [CK_MfaChallenges_Verified] CHECK (([VerifiedAt] IS NULL AND [VerifiedCredentialId] IS NULL) OR ([VerifiedAt] IS NOT NULL AND [VerifiedCredentialId] IS NOT NULL))
);

CREATE TABLE [auth].[MfaCredentials] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [Method] int NOT NULL,
    [ProtectedSecretReference] nvarchar(500) NULL,
    [RecoveryCodeHash] varchar(256) COLLATE Latin1_General_100_BIN2 NULL,
    [EnrolledAt] datetimeoffset NOT NULL,
    [ConfirmedAt] datetimeoffset NULL,
    [RevokedAt] datetimeoffset NULL,
    [LastAcceptedCounter] bigint NULL,
    [RecoveryCodeConsumedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_MfaCredentials] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_MfaCredentials_UserAccountId_Id] UNIQUE ([UserAccountId], [Id]),
    CONSTRAINT [CK_MfaCredentials_Counter] CHECK ([LastAcceptedCounter] IS NULL OR ([Method] = 1 AND [LastAcceptedCounter] >= 0)),
    CONSTRAINT [CK_MfaCredentials_Dates] CHECK (([ConfirmedAt] IS NULL OR [ConfirmedAt] >= [EnrolledAt]) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [EnrolledAt]) AND ([RecoveryCodeConsumedAt] IS NULL OR ([Method] = 2 AND [ConfirmedAt] IS NOT NULL AND [RecoveryCodeConsumedAt] >= [ConfirmedAt]))),
    CONSTRAINT [CK_MfaCredentials_Secret] CHECK (([Method] = 2 AND [RecoveryCodeHash] IS NOT NULL AND [ProtectedSecretReference] IS NULL) OR ([Method] IN (1,3) AND [ProtectedSecretReference] IS NOT NULL AND [RecoveryCodeHash] IS NULL))
);

CREATE TABLE [auth].[PasswordResetChallenges] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [SecurityVersion] int NOT NULL,
    [TokenHash] varchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Channel] nvarchar(60) NOT NULL,
    [ProtectedDestinationReference] nvarchar(500) NOT NULL,
    [IssuedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [ConsumedAt] datetimeoffset NULL,
    [RevokedAt] datetimeoffset NULL,
    [FailedVerificationCount] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_PasswordResetChallenges] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PasswordResetChallenges_Counters] CHECK ([SecurityVersion] > 0 AND [FailedVerificationCount] >= 0),
    CONSTRAINT [CK_PasswordResetChallenges_Dates] CHECK ([ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt])) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [IssuedAt]))
);

CREATE TABLE [auth].[RefreshTokens] (
    [Id] uniqueidentifier NOT NULL,
    [UserSessionId] uniqueidentifier NOT NULL,
    [TokenHash] varchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [IssuedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [ConsumedAt] datetimeoffset NULL,
    [ReplacedByTokenId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_RefreshTokens_UserSessionId_Id] UNIQUE ([UserSessionId], [Id]),
    CONSTRAINT [CK_RefreshTokens_Lifetime] CHECK ([ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt]))),
    CONSTRAINT [CK_RefreshTokens_Rotation] CHECK (([ConsumedAt] IS NULL AND [ReplacedByTokenId] IS NULL) OR ([ConsumedAt] IS NOT NULL AND [ReplacedByTokenId] IS NOT NULL AND [ReplacedByTokenId] <> [Id])),
    CONSTRAINT [FK_RefreshTokens_RefreshTokens_UserSessionId_ReplacedByTokenId] FOREIGN KEY ([UserSessionId], [ReplacedByTokenId]) REFERENCES [auth].[RefreshTokens] ([UserSessionId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[RegisteredDevices] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [DeviceIdentifierHash] varchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Platform] nvarchar(40) NOT NULL,
    [DisplayName] nvarchar(150) NOT NULL,
    [RegisteredAt] datetimeoffset NOT NULL,
    [VerifiedAt] datetimeoffset NULL,
    [LastSeenAt] datetimeoffset NULL,
    [RevokedAt] datetimeoffset NULL,
    [RevocationReason] nvarchar(1000) NULL,
    [IntegrityAssessmentCode] nvarchar(100) NULL,
    [IntegrityAssessedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_RegisteredDevices] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_RegisteredDevices_UserAccountId_Id] UNIQUE ([UserAccountId], [Id]),
    CONSTRAINT [CK_RegisteredDevices_Dates] CHECK (([VerifiedAt] IS NULL OR [VerifiedAt] >= [RegisteredAt]) AND ([LastSeenAt] IS NULL OR [LastSeenAt] >= [RegisteredAt]) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [RegisteredAt]))
);

CREATE TABLE [attendance].[SiteReportReviews] (
    [Id] uniqueidentifier NOT NULL,
    [SiteReportId] uniqueidentifier NOT NULL,
    [Sequence] int NOT NULL,
    [Outcome] int NOT NULL,
    [Reviewer] nvarchar(200) NOT NULL,
    [ReviewerRole] nvarchar(200) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    [ReviewedAtUtc] datetimeoffset(7) NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    CONSTRAINT [PK_SiteReportReviews] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_SiteReportReviews_Outcome] CHECK ([Outcome] BETWEEN 1 AND 4),
    CONSTRAINT [CK_SiteReportReviews_ReviewedAtUtc_Utc] CHECK ([ReviewedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ReviewedAtUtc]) = 0),
    CONSTRAINT [CK_SiteReportReviews_Sequence] CHECK ([Sequence] > 0),
    CONSTRAINT [FK_SiteReportReviews_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[SiteReports] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [SiteId] uniqueidentifier NOT NULL,
    [EmployeeAssignmentId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [TaskReference] nvarchar(200) NULL,
    [Notes] nvarchar(4000) NOT NULL,
    [RegisteredDeviceId] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [IdempotencyKey] nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ReceivedAtUtc] datetimeoffset(7) NOT NULL,
    [DeviceCapturedAtUtc] datetimeoffset(7) NULL,
    [Latitude] decimal(10,7) NULL,
    [Longitude] decimal(10,7) NULL,
    [AccuracyMeters] decimal(10,2) NULL,
    [IsOffline] bit NOT NULL,
    [PrivateEvidenceReference] nvarchar(500) NULL,
    CONSTRAINT [PK_SiteReports] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_SiteReports_Coordinates] CHECK ((([Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND ([AccuracyMeters] IS NULL OR [AccuracyMeters] >= 0)))),
    CONSTRAINT [CK_SiteReports_DeviceCapturedAtUtc_Utc] CHECK ([DeviceCapturedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeviceCapturedAtUtc]) = 0),
    CONSTRAINT [CK_SiteReports_Kind] CHECK ([Kind] BETWEEN 1 AND 5),
    CONSTRAINT [CK_SiteReports_ReceivedAtUtc_Utc] CHECK ([ReceivedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ReceivedAtUtc]) = 0),
    CONSTRAINT [FK_SiteReports_EmployeeAssignments_EmployeeId_EmployeeAssignmentId] FOREIGN KEY ([EmployeeId], [EmployeeAssignmentId]) REFERENCES [hr].[EmployeeAssignments] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SiteReports_RegisteredDevices_UserAccountId_RegisteredDeviceId] FOREIGN KEY ([UserAccountId], [RegisteredDeviceId]) REFERENCES [auth].[RegisteredDevices] ([UserAccountId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SiteReports_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[UserAccessScopes] (
    [Id] uniqueidentifier NOT NULL,
    [UserRoleAssignmentId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [EmployeeId] uniqueidentifier NULL,
    [DepartmentId] uniqueidentifier NULL,
    [TeamId] uniqueidentifier NULL,
    [SiteId] uniqueidentifier NULL,
    [ProjectId] uniqueidentifier NULL,
    CONSTRAINT [PK_UserAccessScopes] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_UserAccessScopes_UserRoleAssignmentId_Id] UNIQUE ([UserRoleAssignmentId], [Id]),
    CONSTRAINT [CK_UserAccessScopes_Target] CHECK (([Kind] IN (1,2) AND [EmployeeId] IS NOT NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL) OR ([Kind] = 3 AND [EmployeeId] IS NULL AND [DepartmentId] IS NOT NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL) OR ([Kind] = 4 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NOT NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL) OR ([Kind] = 5 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NOT NULL AND [ProjectId] IS NULL) OR ([Kind] = 6 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NOT NULL) OR ([Kind] = 7 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL)),
    CONSTRAINT [FK_UserAccessScopes_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [hr].[Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserAccessScopes_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserAccessScopes_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [auth].[Projects] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserAccessScopes_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserAccessScopes_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [auth].[Teams] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[UserAccounts] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ApprovalDecisionId] uniqueidentifier NOT NULL,
    [LoginName] nvarchar(320) NOT NULL,
    [NormalizedLoginName] nvarchar(320) NOT NULL,
    [PasswordHash] nvarchar(2000) NULL,
    [PasswordHashFormat] nvarchar(100) NULL,
    [PasswordChangedAt] datetimeoffset NULL,
    [Status] int NOT NULL,
    [InitialRoleAssignmentId] uniqueidentifier NULL,
    [InitialAccessScopeId] uniqueidentifier NULL,
    [CurrentSetupInvitationId] uniqueidentifier NULL,
    [MfaRequired] bit NOT NULL,
    [SecurityVersion] int NOT NULL,
    [FailedLoginCount] int NOT NULL,
    [LockedUntil] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ActivatedAt] datetimeoffset NULL,
    [StatusChangedAt] datetimeoffset NULL,
    [StatusReason] nvarchar(1000) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_UserAccounts] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_UserAccounts_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]),
    CONSTRAINT [CK_UserAccounts_Active] CHECK ([Status] <> 3 OR ([PasswordHash] IS NOT NULL AND [ActivatedAt] IS NOT NULL AND [InitialRoleAssignmentId] IS NOT NULL)),
    CONSTRAINT [CK_UserAccounts_Counters] CHECK ([SecurityVersion] > 0 AND [FailedLoginCount] >= 0),
    CONSTRAINT [CK_UserAccounts_Credentials] CHECK (([PasswordHash] IS NULL AND [PasswordHashFormat] IS NULL AND [PasswordChangedAt] IS NULL) OR ([PasswordHash] IS NOT NULL AND [PasswordHashFormat] IS NOT NULL AND [PasswordChangedAt] IS NOT NULL)),
    CONSTRAINT [CK_UserAccounts_InitialAccess] CHECK (([InitialRoleAssignmentId] IS NULL AND [InitialAccessScopeId] IS NULL) OR ([InitialRoleAssignmentId] IS NOT NULL AND [InitialAccessScopeId] IS NOT NULL)),
    CONSTRAINT [CK_UserAccounts_Invitation] CHECK ([Status] <> 2 OR ([CurrentSetupInvitationId] IS NOT NULL AND [InitialAccessScopeId] IS NOT NULL)),
    CONSTRAINT [CK_UserAccounts_Status] CHECK ([Status] BETWEEN 0 AND 5),
    CONSTRAINT [FK_UserAccounts_AccountSetupInvitations_Id_CurrentSetupInvitationId] FOREIGN KEY ([Id], [CurrentSetupInvitationId]) REFERENCES [auth].[AccountSetupInvitations] ([UserAccountId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserAccounts_EmployeeOnboardingDecisions_EmployeeId_ApprovalDecisionId] FOREIGN KEY ([EmployeeId], [ApprovalDecisionId]) REFERENCES [hr].[EmployeeOnboardingDecisions] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserAccounts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserAccounts_UserAccessScopes_InitialRoleAssignmentId_InitialAccessScopeId] FOREIGN KEY ([InitialRoleAssignmentId], [InitialAccessScopeId]) REFERENCES [auth].[UserAccessScopes] ([UserRoleAssignmentId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[UserRoleAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [AssignedByUserId] uniqueidentifier NOT NULL,
    [EffectiveFrom] datetimeoffset NOT NULL,
    [EffectiveTo] datetimeoffset NULL,
    [RevokedAt] datetimeoffset NULL,
    [RevokedByUserId] uniqueidentifier NULL,
    [RevocationReason] nvarchar(1000) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_UserRoleAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_UserRoleAssignments_UserAccountId_Id] UNIQUE ([UserAccountId], [Id]),
    CONSTRAINT [CK_UserRoleAssignments_Dates] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [CK_UserRoleAssignments_Revocation] CHECK (([RevokedAt] IS NULL AND [RevokedByUserId] IS NULL AND [RevocationReason] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedByUserId] IS NOT NULL AND [RevocationReason] IS NOT NULL)),
    CONSTRAINT [FK_UserRoleAssignments_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [auth].[Roles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserRoleAssignments_UserAccounts_EmployeeId_UserAccountId] FOREIGN KEY ([EmployeeId], [UserAccountId]) REFERENCES [auth].[UserAccounts] ([EmployeeId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[UserSessions] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [Client] int NOT NULL,
    [RegisteredDeviceId] uniqueidentifier NULL,
    [MfaChallengeId] uniqueidentifier NULL,
    [RefreshTokenFamilyId] uniqueidentifier NOT NULL,
    [SecurityVersion] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [RevokedAt] datetimeoffset NULL,
    [RevocationReason] nvarchar(1000) NULL,
    [CorrelationId] nvarchar(100) NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_UserSessions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_UserSessions_Client] CHECK ([Client] IN (1,2) AND ([Client] <> 2 OR [RegisteredDeviceId] IS NOT NULL)),
    CONSTRAINT [CK_UserSessions_Lifetime] CHECK ([ExpiresAt] > [CreatedAt] AND ([RevokedAt] IS NULL OR [RevokedAt] >= [CreatedAt])),
    CONSTRAINT [CK_UserSessions_Version] CHECK ([SecurityVersion] > 0),
    CONSTRAINT [FK_UserSessions_MfaChallenges_UserAccountId_MfaChallengeId] FOREIGN KEY ([UserAccountId], [MfaChallengeId]) REFERENCES [auth].[MfaChallenges] ([UserAccountId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserSessions_RegisteredDevices_UserAccountId_RegisteredDeviceId] FOREIGN KEY ([UserAccountId], [RegisteredDeviceId]) REFERENCES [auth].[RegisteredDevices] ([UserAccountId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserSessions_UserAccounts_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ZoneEvents] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [DutyMonitoringWindowId] uniqueidentifier NOT NULL,
    [ZoneAssignmentId] uniqueidentifier NOT NULL,
    [ZoneVersionId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [Source] int NOT NULL,
    [RegisteredDeviceId] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [IdempotencyKey] nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ReceivedAtUtc] datetimeoffset(7) NOT NULL,
    [DeviceCapturedAtUtc] datetimeoffset(7) NULL,
    [Latitude] decimal(10,7) NOT NULL,
    [Longitude] decimal(10,7) NOT NULL,
    [AccuracyMeters] decimal(10,2) NOT NULL,
    [IsOffline] bit NOT NULL,
    [ShownInstruction] nvarchar(2000) NULL,
    CONSTRAINT [PK_ZoneEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ZoneEvents_Coordinates] CHECK ((([Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND ([AccuracyMeters] IS NULL OR [AccuracyMeters] >= 0)))),
    CONSTRAINT [CK_ZoneEvents_DeviceCapturedAtUtc_Utc] CHECK ([DeviceCapturedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeviceCapturedAtUtc]) = 0),
    CONSTRAINT [CK_ZoneEvents_Kind] CHECK ([Kind] BETWEEN 1 AND 5),
    CONSTRAINT [CK_ZoneEvents_ReceivedAtUtc_Utc] CHECK ([ReceivedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ReceivedAtUtc]) = 0),
    CONSTRAINT [CK_ZoneEvents_Source] CHECK ([Source] BETWEEN 1 AND 3),
    CONSTRAINT [FK_ZoneEvents_DutyMonitoringWindows_EmployeeId_ZoneAssignmentId_DutyMonitoringWindowId] FOREIGN KEY ([EmployeeId], [ZoneAssignmentId], [DutyMonitoringWindowId]) REFERENCES [attendance].[DutyMonitoringWindows] ([EmployeeId], [ZoneAssignmentId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneEvents_RegisteredDevices_UserAccountId_RegisteredDeviceId] FOREIGN KEY ([UserAccountId], [RegisteredDeviceId]) REFERENCES [auth].[RegisteredDevices] ([UserAccountId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneEvents_UserAccounts_EmployeeId_UserAccountId] FOREIGN KEY ([EmployeeId], [UserAccountId]) REFERENCES [auth].[UserAccounts] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneEvents_ZoneAssignments_ZoneVersionId_ZoneAssignmentId] FOREIGN KEY ([ZoneVersionId], [ZoneAssignmentId]) REFERENCES [attendance].[ZoneAssignments] ([ZoneVersionId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneEvents_ZoneVersions_ZoneVersionId] FOREIGN KEY ([ZoneVersionId]) REFERENCES [attendance].[ZoneVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ZoneEventResponses] (
    [Id] uniqueidentifier NOT NULL,
    [ZoneEventId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [Actor] nvarchar(200) NOT NULL,
    [AtUtc] datetimeoffset(7) NOT NULL,
    [Reason] nvarchar(2000) NULL,
    [PrivateEvidenceReference] nvarchar(500) NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    CONSTRAINT [PK_ZoneEventResponses] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ZoneEventResponses_AtUtc_Utc] CHECK ([AtUtc] IS NULL OR DATEPART(TZOFFSET, [AtUtc]) = 0),
    CONSTRAINT [CK_ZoneEventResponses_Kind] CHECK ([Kind] BETWEEN 1 AND 3),
    CONSTRAINT [CK_ZoneEventResponses_Reason] CHECK ([Kind] = 1 OR [Reason] IS NOT NULL),
    CONSTRAINT [FK_ZoneEventResponses_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ZoneEventResponses_ZoneEvents_ZoneEventId] FOREIGN KEY ([ZoneEventId]) REFERENCES [attendance].[ZoneEvents] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Sites_CityId] ON [attendance].[Sites] ([CityId]);

CREATE INDEX [IX_Sites_ProjectId] ON [attendance].[Sites] ([ProjectId]);

ALTER TABLE [attendance].[Sites] ADD CONSTRAINT [CK_Sites_Dates] CHECK ([EndsOn] IS NULL OR ([StartsOn] IS NOT NULL AND [EndsOn] >= [StartsOn]));

CREATE INDEX [IX_RawPunchEvents_EvidenceFileId] ON [attendance].[RawPunchEvents] ([EvidenceFileId]);

CREATE INDEX [IX_Employees_Id_ApprovedOnboardingDecisionId] ON [hr].[Employees] ([Id], [ApprovedOnboardingDecisionId]);

CREATE INDEX [IX_Employees_OnboardingStatus_CreatedAt_Id] ON [hr].[Employees] ([OnboardingStatus], [CreatedAt], [Id]);

ALTER TABLE [hr].[Employees] ADD CONSTRAINT [CK_Employees_OnboardingApproval] CHECK (([OnboardingStatus] = 2 AND [ApprovedOnboardingDecisionId] IS NOT NULL) OR ([OnboardingStatus] <> 2 AND [ApprovedOnboardingDecisionId] IS NULL));

ALTER TABLE [hr].[Employees] ADD CONSTRAINT [CK_Employees_OnboardingStatus] CHECK ([OnboardingStatus] BETWEEN 0 AND 4);

ALTER TABLE [hr].[Employees] ADD CONSTRAINT [CK_Employees_SubmittedVersion] CHECK ([SubmittedVersion] >= 0 AND [DraftRevision] > 0);

ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_ScanEvidence] CHECK (([ScanStatus] = 0 AND [ScannedAt] IS NULL) OR ([ScanStatus] <> 0 AND [ScannedAt] IS NOT NULL AND [ScannedAt] >= [UploadedAt] AND [ScannerVersion] IS NOT NULL AND ([ScanStatus] <> 1 OR [SignatureVerified] = 1)));

ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_VerificationEvidence] CHECK (([VerificationStatus] = 0 AND [VerifiedAt] IS NULL AND [VerifiedByUserId] IS NULL) OR ([VerificationStatus] <> 0 AND [VerifiedAt] IS NOT NULL AND [VerifiedAt] >= [UploadedAt] AND [VerifiedByUserId] IS NOT NULL AND ([VerificationStatus] <> 2 OR [VerificationReason] IS NOT NULL)));

CREATE INDEX [IX_EmployeeAddresses_CityId] ON [hr].[EmployeeAddresses] ([CityId]);

CREATE INDEX [IX_Branches_CityId] ON [hr].[Branches] ([CityId]);

CREATE INDEX [IX_AccountSetupInvitations_ApprovalDecisionId] ON [auth].[AccountSetupInvitations] ([ApprovalDecisionId]);

CREATE INDEX [IX_AccountSetupInvitations_DeliveryStatus_NextDeliveryAttemptAt] ON [auth].[AccountSetupInvitations] ([DeliveryStatus], [NextDeliveryAttemptAt]);

CREATE UNIQUE INDEX [IX_AccountSetupInvitations_TokenHash] ON [auth].[AccountSetupInvitations] ([TokenHash]);

CREATE INDEX [IX_AccountSetupInvitations_UserAccountId_IssuedAt] ON [auth].[AccountSetupInvitations] ([UserAccountId], [IssuedAt]);

CREATE UNIQUE INDEX [IX_AdvanceLoanAgreements_AdvanceLoanRequestId] ON [hr].[AdvanceLoanAgreements] ([AdvanceLoanRequestId]);

CREATE INDEX [IX_AdvanceLoanAgreements_AdvanceLoanRequestId_Currency] ON [hr].[AdvanceLoanAgreements] ([AdvanceLoanRequestId], [Currency]);

CREATE INDEX [IX_AdvanceLoanAgreements_ApprovalRequestId] ON [hr].[AdvanceLoanAgreements] ([ApprovalRequestId]);

CREATE INDEX [IX_AdvanceLoanRequests_ApprovalRequestId] ON [hr].[AdvanceLoanRequests] ([ApprovalRequestId]);

CREATE INDEX [IX_AdvanceLoanRequests_EmployeeId] ON [hr].[AdvanceLoanRequests] ([EmployeeId]);

CREATE INDEX [IX_AllowanceClaims_ApprovalRequestId] ON [hr].[AllowanceClaims] ([ApprovalRequestId]);

CREATE INDEX [IX_AllowanceClaims_DeploymentApprovalRequestId] ON [hr].[AllowanceClaims] ([DeploymentApprovalRequestId]);

CREATE INDEX [IX_AllowanceClaims_EmployeeAssignmentId_EmployeeId] ON [hr].[AllowanceClaims] ([EmployeeAssignmentId], [EmployeeId]);

CREATE UNIQUE INDEX [IX_AllowanceClaims_EmployeeId_Kind_EligibilityKey] ON [hr].[AllowanceClaims] ([EmployeeId], [Kind], [EligibilityKey]);

CREATE INDEX [IX_AllowanceClaims_PolicyVersionId] ON [hr].[AllowanceClaims] ([PolicyVersionId]);

CREATE INDEX [IX_AllowanceClaims_WorkSessionId_EmployeeId] ON [hr].[AllowanceClaims] ([WorkSessionId], [EmployeeId]);

CREATE UNIQUE INDEX [IX_AllowanceSettlementRecords_AllowanceClaimId_IdempotencyKey] ON [hr].[AllowanceSettlementRecords] ([AllowanceClaimId], [IdempotencyKey]);

CREATE INDEX [IX_AllowanceSettlementRecords_PayrollHandoffLineId] ON [hr].[AllowanceSettlementRecords] ([PayrollHandoffLineId]);

CREATE UNIQUE INDEX [IX_ApprovalDecisions_ApprovalRequestId] ON [workflow].[ApprovalDecisions] ([ApprovalRequestId]);

CREATE INDEX [IX_ApprovalDelegations_DelegateUserId_RequestType_StartsAt] ON [workflow].[ApprovalDelegations] ([DelegateUserId], [RequestType], [StartsAt]);

CREATE INDEX [IX_ApprovalRequests_RequestType_SubjectId_Stage] ON [workflow].[ApprovalRequests] ([RequestType], [SubjectId], [Stage]);

CREATE INDEX [IX_ApprovalRequests_RouteRuleId] ON [workflow].[ApprovalRequests] ([RouteRuleId]);

CREATE INDEX [IX_ApprovalRequests_State_RequestType_RequestedAt] ON [workflow].[ApprovalRequests] ([State], [RequestType], [RequestedAt]);

CREATE UNIQUE INDEX [IX_ApprovalRouteRules_Code_Version] ON [workflow].[ApprovalRouteRules] ([Code], [Version]);

CREATE INDEX [IX_ApprovalRouteRules_DepartmentId] ON [workflow].[ApprovalRouteRules] ([DepartmentId]);

CREATE INDEX [IX_ApprovalRouteRules_RequestType_EffectiveFrom_EffectiveTo] ON [workflow].[ApprovalRouteRules] ([RequestType], [EffectiveFrom], [EffectiveTo]);

CREATE INDEX [IX_ApprovalRouteRules_SiteId] ON [workflow].[ApprovalRouteRules] ([SiteId]);

CREATE INDEX [IX_ApprovalRouteRules_StaffCategoryId] ON [workflow].[ApprovalRouteRules] ([StaffCategoryId]);

CREATE UNIQUE INDEX [IX_AttendanceCalculationLines_DailyAttendanceSummaryId_Sequence] ON [attendance].[AttendanceCalculationLines] ([DailyAttendanceSummaryId], [Sequence]);

CREATE INDEX [IX_AttendanceCorrections_AdjustmentPeriodLockId] ON [attendance].[AttendanceCorrections] ([AdjustmentPeriodLockId]);

CREATE UNIQUE INDEX [IX_AttendanceCorrections_ApprovalRequestId] ON [attendance].[AttendanceCorrections] ([ApprovalRequestId]);

CREATE INDEX [IX_AttendanceCorrections_EmployeeId_AttendanceDate] ON [attendance].[AttendanceCorrections] ([EmployeeId], [AttendanceDate]);

CREATE INDEX [IX_AttendanceCorrections_EmployeeId_AttendanceDate_OriginalSummaryId] ON [attendance].[AttendanceCorrections] ([EmployeeId], [AttendanceDate], [OriginalSummaryId]);

CREATE INDEX [IX_AttendanceCorrections_EmployeeId_RawPunchEventId] ON [attendance].[AttendanceCorrections] ([EmployeeId], [RawPunchEventId]);

CREATE INDEX [IX_AttendanceExceptions_ApprovalRequestId] ON [attendance].[AttendanceExceptions] ([ApprovalRequestId]);

CREATE INDEX [IX_AttendanceExceptions_EmployeeId_AttendanceDate] ON [attendance].[AttendanceExceptions] ([EmployeeId], [AttendanceDate]);

CREATE INDEX [IX_AttendanceExceptions_EmployeeId_DailyAttendanceSummaryId] ON [attendance].[AttendanceExceptions] ([EmployeeId], [DailyAttendanceSummaryId]);

CREATE INDEX [IX_AttendanceExceptions_EmployeeId_RawPunchEventId] ON [attendance].[AttendanceExceptions] ([EmployeeId], [RawPunchEventId]);

CREATE INDEX [IX_AttendanceExceptions_EmployeeId_WorkSessionId] ON [attendance].[AttendanceExceptions] ([EmployeeId], [WorkSessionId]);

CREATE INDEX [IX_AttendancePeriodLockEvents_ApprovalRequestId] ON [attendance].[AttendancePeriodLockEvents] ([ApprovalRequestId]);

CREATE UNIQUE INDEX [IX_AttendancePeriodLockEvents_AttendancePeriodLockId_Revision] ON [attendance].[AttendancePeriodLockEvents] ([AttendancePeriodLockId], [Revision]);

CREATE UNIQUE INDEX [IX_AttendancePeriodLocks_PeriodStart_PeriodEndExclusive] ON [attendance].[AttendancePeriodLocks] ([PeriodStart], [PeriodEndExclusive]);

CREATE UNIQUE INDEX [IX_AttendancePeriodSummaries_AttendancePeriodLockEventId_EmployeeId_AttendanceDate] ON [attendance].[AttendancePeriodSummaries] ([AttendancePeriodLockEventId], [EmployeeId], [AttendanceDate]);

CREATE INDEX [IX_AttendancePeriodSummaries_EmployeeId_AttendanceDate_DailyAttendanceSummaryId] ON [attendance].[AttendancePeriodSummaries] ([EmployeeId], [AttendanceDate], [DailyAttendanceSummaryId]);

CREATE UNIQUE INDEX [IX_AttendanceSummarySessions_DailyAttendanceSummaryId_WorkSessionId] ON [attendance].[AttendanceSummarySessions] ([DailyAttendanceSummaryId], [WorkSessionId]);

CREATE INDEX [IX_AttendanceSummarySessions_EmployeeId_DailyAttendanceSummaryId] ON [attendance].[AttendanceSummarySessions] ([EmployeeId], [DailyAttendanceSummaryId]);

CREATE INDEX [IX_AttendanceSummarySessions_EmployeeId_WorkSessionId] ON [attendance].[AttendanceSummarySessions] ([EmployeeId], [WorkSessionId]);

CREATE INDEX [IX_AuditEvents_ActorUserId_OccurredAt] ON [audit].[AuditEvents] ([ActorUserId], [OccurredAt]);

CREATE INDEX [IX_AuditEvents_CorrelationId] ON [audit].[AuditEvents] ([CorrelationId]);

CREATE INDEX [IX_AuditEvents_EmployeeScopeId] ON [audit].[AuditEvents] ([EmployeeScopeId]);

CREATE INDEX [IX_AuditEvents_TargetType_TargetId_OccurredAt] ON [audit].[AuditEvents] ([TargetType], [TargetId], [OccurredAt]);

CREATE UNIQUE INDEX [IX_Cities_Code] ON [hr].[Cities] ([Code]);

CREATE INDEX [IX_CompensationRecords_ApprovalRequestId] ON [hr].[CompensationRecords] ([ApprovalRequestId]);

CREATE UNIQUE INDEX [IX_CompensationRecords_EmployeeId_EffectiveFrom_Revision] ON [hr].[CompensationRecords] ([EmployeeId], [EffectiveFrom], [Revision]);

CREATE INDEX [IX_CompensationRecords_SupersedesCompensationRecordId_EmployeeId] ON [hr].[CompensationRecords] ([SupersedesCompensationRecordId], [EmployeeId]);

CREATE UNIQUE INDEX [IX_DailyAttendanceSummaries_EmployeeId_AttendanceDate_Revision] ON [attendance].[DailyAttendanceSummaries] ([EmployeeId], [AttendanceDate], [Revision]);

CREATE INDEX [IX_DailyAttendanceSummaries_EmployeeId_AttendanceDate_SupersedesSummaryId] ON [attendance].[DailyAttendanceSummaries] ([EmployeeId], [AttendanceDate], [SupersedesSummaryId]);

CREATE INDEX [IX_DailyAttendanceSummaries_PolicyVersionId] ON [attendance].[DailyAttendanceSummaries] ([PolicyVersionId]);

CREATE INDEX [IX_DailyAttendanceSummaries_ShiftVersionId] ON [attendance].[DailyAttendanceSummaries] ([ShiftVersionId]);

CREATE INDEX [IX_DisciplinaryCases_ApprovalRequestId] ON [hr].[DisciplinaryCases] ([ApprovalRequestId]);

CREATE INDEX [IX_DisciplinaryCases_EmployeeId] ON [hr].[DisciplinaryCases] ([EmployeeId]);

CREATE INDEX [IX_DisciplinaryCases_PolicyImpactAssessmentId_EmployeeId] ON [hr].[DisciplinaryCases] ([PolicyImpactAssessmentId], [EmployeeId]);

CREATE INDEX [IX_DutyMonitoringWindows_EmployeeId_StartsAtUtc] ON [attendance].[DutyMonitoringWindows] ([EmployeeId], [StartsAtUtc]);

CREATE INDEX [IX_DutyMonitoringWindows_RetentionPolicyVersionId] ON [attendance].[DutyMonitoringWindows] ([RetentionPolicyVersionId]);

CREATE INDEX [IX_DutyMonitoringWindows_ZoneAssignmentId] ON [attendance].[DutyMonitoringWindows] ([ZoneAssignmentId]);

CREATE UNIQUE INDEX [IX_EmployeeAssetCustodies_AssetReference] ON [hr].[EmployeeAssetCustodies] ([AssetReference]) WHERE [ReturnedAt] IS NULL;

CREATE INDEX [IX_EmployeeAssetCustodies_EmployeeId_IssuedAt] ON [hr].[EmployeeAssetCustodies] ([EmployeeId], [IssuedAt]);

CREATE INDEX [IX_EmployeeBankDetails_EmployeeId] ON [hr].[EmployeeBankDetails] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_EmployeeBankDetails_SupersedesEmployeeBankDetailsId] ON [hr].[EmployeeBankDetails] ([SupersedesEmployeeBankDetailsId]) WHERE [SupersedesEmployeeBankDetailsId] IS NOT NULL;

CREATE INDEX [IX_EmployeeBankDetails_SupersedesEmployeeBankDetailsId_EmployeeId] ON [hr].[EmployeeBankDetails] ([SupersedesEmployeeBankDetailsId], [EmployeeId]);

CREATE INDEX [IX_EmployeeCertifications_EmployeeDocumentId_EmployeeId] ON [hr].[EmployeeCertifications] ([EmployeeDocumentId], [EmployeeId]);

CREATE INDEX [IX_EmployeeCertifications_EmployeeId_ExpiresOn] ON [hr].[EmployeeCertifications] ([EmployeeId], [ExpiresOn]);

CREATE INDEX [IX_EmployeeCommuteDistances_EmployeeId] ON [hr].[EmployeeCommuteDistances] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_EmployeeCommuteDistances_SupersedesEmployeeCommuteDistanceId] ON [hr].[EmployeeCommuteDistances] ([SupersedesEmployeeCommuteDistanceId]) WHERE [SupersedesEmployeeCommuteDistanceId] IS NOT NULL;

CREATE INDEX [IX_EmployeeCommuteDistances_SupersedesEmployeeCommuteDistanceId_EmployeeId] ON [hr].[EmployeeCommuteDistances] ([SupersedesEmployeeCommuteDistanceId], [EmployeeId]);

CREATE INDEX [IX_EmployeeMedicalRecords_EmployeeDocumentId_EmployeeId] ON [hr].[EmployeeMedicalRecords] ([EmployeeDocumentId], [EmployeeId]);

CREATE INDEX [IX_EmployeeMedicalRecords_EmployeeId] ON [hr].[EmployeeMedicalRecords] ([EmployeeId]);

CREATE INDEX [IX_EmployeeOnboardingDecisions_ActorRoleId] ON [hr].[EmployeeOnboardingDecisions] ([ActorRoleId]);

CREATE INDEX [IX_EmployeeOnboardingDecisions_EmployeeId_DecidedAt] ON [hr].[EmployeeOnboardingDecisions] ([EmployeeId], [DecidedAt]);

CREATE UNIQUE INDEX [IX_EmployeeOnboardingDecisions_EmployeeId_SubmissionId] ON [hr].[EmployeeOnboardingDecisions] ([EmployeeId], [SubmissionId]);

CREATE UNIQUE INDEX [IX_EmployeeOnboardingDecisions_SubmissionId] ON [hr].[EmployeeOnboardingDecisions] ([SubmissionId]);

CREATE INDEX [IX_EmployeeSimAssignments_EmployeeId] ON [hr].[EmployeeSimAssignments] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_EmployeeSimAssignments_SupersedesEmployeeSimAssignmentId] ON [hr].[EmployeeSimAssignments] ([SupersedesEmployeeSimAssignmentId]) WHERE [SupersedesEmployeeSimAssignmentId] IS NOT NULL;

CREATE INDEX [IX_EmployeeSimAssignments_SupersedesEmployeeSimAssignmentId_EmployeeId] ON [hr].[EmployeeSimAssignments] ([SupersedesEmployeeSimAssignmentId], [EmployeeId]);

CREATE UNIQUE INDEX [IX_EmployeeSubmissions_EmployeeId_Version] ON [hr].[EmployeeSubmissions] ([EmployeeId], [Version]);

CREATE INDEX [IX_EmployeeSubmissions_SubmittedAt_Id] ON [hr].[EmployeeSubmissions] ([SubmittedAt], [Id]);

CREATE UNIQUE INDEX [IX_FaceEnrollments_EmployeeId] ON [attendance].[FaceEnrollments] ([EmployeeId]) WHERE [IsActive] = 1;

CREATE INDEX [IX_FaceEnrollments_EmployeeId_ReplacesEnrollmentId] ON [attendance].[FaceEnrollments] ([EmployeeId], [ReplacesEnrollmentId]);

CREATE UNIQUE INDEX [IX_FaceVerificationAttempts_ChallengeSha256] ON [attendance].[FaceVerificationAttempts] ([ChallengeSha256]);

CREATE UNIQUE INDEX [IX_FaceVerificationAttempts_ConsumedByPunchId] ON [attendance].[FaceVerificationAttempts] ([ConsumedByPunchId]) WHERE [ConsumedByPunchId] IS NOT NULL;

CREATE INDEX [IX_FaceVerificationAttempts_EmployeeId_FaceEnrollmentId] ON [attendance].[FaceVerificationAttempts] ([EmployeeId], [FaceEnrollmentId]);

CREATE INDEX [IX_FaceVerificationAttempts_EmployeeId_PunchIdempotencyKey_IssuedAtUtc] ON [attendance].[FaceVerificationAttempts] ([EmployeeId], [PunchIdempotencyKey], [IssuedAtUtc]);

CREATE INDEX [IX_FaceVerificationAttempts_EmployeeId_RegisteredDeviceId_PunchIdempotencyKey_ConsumedByPunchId] ON [attendance].[FaceVerificationAttempts] ([EmployeeId], [RegisteredDeviceId], [PunchIdempotencyKey], [ConsumedByPunchId]);

CREATE INDEX [IX_FaceVerificationAttempts_EmployeeId_UserAccountId] ON [attendance].[FaceVerificationAttempts] ([EmployeeId], [UserAccountId]);

CREATE INDEX [IX_FaceVerificationAttempts_UserAccountId_RegisteredDeviceKey] ON [attendance].[FaceVerificationAttempts] ([UserAccountId], [RegisteredDeviceKey]);

CREATE INDEX [IX_FaceVerificationExceptions_ApprovalRequestId] ON [attendance].[FaceVerificationExceptions] ([ApprovalRequestId]);

CREATE INDEX [IX_FaceVerificationExceptions_EmployeeId_EffectiveFrom] ON [attendance].[FaceVerificationExceptions] ([EmployeeId], [EffectiveFrom]);

CREATE INDEX [IX_FuelClaims_ApprovalRequestId] ON [hr].[FuelClaims] ([ApprovalRequestId]);

CREATE INDEX [IX_FuelClaims_EmployeeCommuteDistanceId_EmployeeId] ON [hr].[FuelClaims] ([EmployeeCommuteDistanceId], [EmployeeId]);

CREATE INDEX [IX_FuelClaims_EmployeeId] ON [hr].[FuelClaims] ([EmployeeId]);

CREATE INDEX [IX_FuelClaims_PolicyVersionId] ON [hr].[FuelClaims] ([PolicyVersionId]);

CREATE INDEX [IX_FuelClaims_VehicleTripId_EmployeeId] ON [hr].[FuelClaims] ([VehicleTripId], [EmployeeId]);

CREATE UNIQUE INDEX [IX_HolidayCalendars_Code_EffectiveFrom] ON [hr].[HolidayCalendars] ([Code], [EffectiveFrom]);

CREATE UNIQUE INDEX [IX_Holidays_HolidayCalendarId_HolidayDate] ON [hr].[Holidays] ([HolidayCalendarId], [HolidayDate]);

CREATE UNIQUE INDEX [IX_IdempotencyReceipts_ActorUserId_Operation_Key] ON [workflow].[IdempotencyReceipts] ([ActorUserId], [Operation], [Key]);

CREATE INDEX [IX_IdempotencyReceipts_ExpiresAt] ON [workflow].[IdempotencyReceipts] ([ExpiresAt]);

CREATE UNIQUE INDEX [IX_InsuranceExpiryReminders_InsurancePolicyId_RecipientUserId_Channel_RemindAt] ON [hr].[InsuranceExpiryReminders] ([InsurancePolicyId], [RecipientUserId], [Channel], [RemindAt]);

CREATE UNIQUE INDEX [IX_InsuranceExpiryReminders_NotificationId] ON [hr].[InsuranceExpiryReminders] ([NotificationId]) WHERE [NotificationId] IS NOT NULL;

CREATE INDEX [IX_InsuranceExpiryReminders_RemindAt] ON [hr].[InsuranceExpiryReminders] ([RemindAt]) WHERE [NotificationId] IS NULL AND [CancelledAt] IS NULL;

CREATE INDEX [IX_InsurancePolicies_EmployeeId_ExpiryDate] ON [hr].[InsurancePolicies] ([EmployeeId], [ExpiryDate]) WHERE [EmployeeId] IS NOT NULL;

CREATE INDEX [IX_InsurancePolicies_ExpiryDate] ON [hr].[InsurancePolicies] ([ExpiryDate]);

CREATE INDEX [IX_InsurancePolicies_StoredFileId] ON [hr].[InsurancePolicies] ([StoredFileId]);

CREATE UNIQUE INDEX [IX_InsurancePolicies_SupersedesPolicyId] ON [hr].[InsurancePolicies] ([SupersedesPolicyId]) WHERE [SupersedesPolicyId] IS NOT NULL;

CREATE INDEX [IX_InsurancePolicies_VehicleId_ExpiryDate] ON [hr].[InsurancePolicies] ([VehicleId], [ExpiryDate]) WHERE [VehicleId] IS NOT NULL;

CREATE INDEX [IX_LeaveCancellationRequests_ApprovalRequestId] ON [hr].[LeaveCancellationRequests] ([ApprovalRequestId]);

CREATE INDEX [IX_LeaveCancellationRequests_LeaveRequestId] ON [hr].[LeaveCancellationRequests] ([LeaveRequestId]);

CREATE INDEX [IX_LeaveEncashmentRequests_ApprovalRequestId] ON [hr].[LeaveEncashmentRequests] ([ApprovalRequestId]);

CREATE INDEX [IX_LeaveEncashmentRequests_EmployeeId] ON [hr].[LeaveEncashmentRequests] ([EmployeeId]);

CREATE INDEX [IX_LeaveEncashmentRequests_PolicyVersionId] ON [hr].[LeaveEncashmentRequests] ([PolicyVersionId]);

CREATE UNIQUE INDEX [IX_LeaveLedgerEntries_EmployeeId_IdempotencyKey] ON [hr].[LeaveLedgerEntries] ([EmployeeId], [IdempotencyKey]);

CREATE INDEX [IX_LeaveLedgerEntries_EmployeeId_LeaveTypeId_LeaveYearStart] ON [hr].[LeaveLedgerEntries] ([EmployeeId], [LeaveTypeId], [LeaveYearStart]);

CREATE INDEX [IX_LeaveLedgerEntries_LeaveRequestId_EmployeeId_LeaveTypeId] ON [hr].[LeaveLedgerEntries] ([LeaveRequestId], [EmployeeId], [LeaveTypeId]);

CREATE INDEX [IX_LeaveLedgerEntries_LeaveTypeId] ON [hr].[LeaveLedgerEntries] ([LeaveTypeId]);

CREATE INDEX [IX_LeaveLedgerEntries_PolicyVersionId] ON [hr].[LeaveLedgerEntries] ([PolicyVersionId]);

CREATE INDEX [IX_LeaveLedgerEntries_ReversesEntryId_EmployeeId_LeaveTypeId_LeaveYearStart_LeaveYearEnd] ON [hr].[LeaveLedgerEntries] ([ReversesEntryId], [EmployeeId], [LeaveTypeId], [LeaveYearStart], [LeaveYearEnd]);

CREATE INDEX [IX_LeaveRecoveryCommitments_ApprovalRequestId] ON [hr].[LeaveRecoveryCommitments] ([ApprovalRequestId]);

CREATE INDEX [IX_LeaveRecoveryCommitments_LeaveRequestId] ON [hr].[LeaveRecoveryCommitments] ([LeaveRequestId]);

CREATE INDEX [IX_LeaveRecoveryCommitments_PolicyVersionId] ON [hr].[LeaveRecoveryCommitments] ([PolicyVersionId]);

CREATE INDEX [IX_LeaveRequests_ApprovalRequestId] ON [hr].[LeaveRequests] ([ApprovalRequestId]);

CREATE INDEX [IX_LeaveRequests_EmployeeId] ON [hr].[LeaveRequests] ([EmployeeId]);

CREATE INDEX [IX_LeaveRequests_LeaveTypeId] ON [hr].[LeaveRequests] ([LeaveTypeId]);

CREATE INDEX [IX_LeaveRequests_PolicyVersionId] ON [hr].[LeaveRequests] ([PolicyVersionId]);

CREATE INDEX [IX_LeaveRequests_RelieverEmployeeId] ON [hr].[LeaveRequests] ([RelieverEmployeeId]);

CREATE UNIQUE INDEX [IX_LeaveTypes_Code] ON [hr].[LeaveTypes] ([Code]);

CREATE UNIQUE INDEX [IX_MfaChallenges_ChallengeHash] ON [auth].[MfaChallenges] ([ChallengeHash]);

CREATE INDEX [IX_MfaChallenges_UserAccountId_IssuedAt] ON [auth].[MfaChallenges] ([UserAccountId], [IssuedAt]);

CREATE INDEX [IX_MfaChallenges_UserAccountId_RegisteredDeviceId] ON [auth].[MfaChallenges] ([UserAccountId], [RegisteredDeviceId]);

CREATE INDEX [IX_MfaChallenges_UserAccountId_VerifiedCredentialId] ON [auth].[MfaChallenges] ([UserAccountId], [VerifiedCredentialId]);

CREATE UNIQUE INDEX [IX_MfaCredentials_RecoveryCodeHash] ON [auth].[MfaCredentials] ([RecoveryCodeHash]) WHERE [RecoveryCodeHash] IS NOT NULL;

CREATE INDEX [IX_MfaCredentials_UserAccountId_RevokedAt] ON [auth].[MfaCredentials] ([UserAccountId], [RevokedAt]);

CREATE INDEX [IX_Notifications_RecipientUserId_ReadAt_CreatedAt] ON [communication].[Notifications] ([RecipientUserId], [ReadAt], [CreatedAt]);

CREATE UNIQUE INDEX [IX_OperationalZones_Code] ON [attendance].[OperationalZones] ([Code]);

CREATE INDEX [IX_OperationalZones_SiteId] ON [attendance].[OperationalZones] ([SiteId]);

CREATE UNIQUE INDEX [IX_OutboxMessages_MessageType_DeduplicationKey] ON [jobs].[OutboxMessages] ([MessageType], [DeduplicationKey]);

CREATE INDEX [IX_OutboxMessages_State_AvailableAt_LeaseExpiresAt] ON [jobs].[OutboxMessages] ([State], [AvailableAt], [LeaseExpiresAt]);

CREATE INDEX [IX_OvertimeRequests_ApprovalRequestId] ON [attendance].[OvertimeRequests] ([ApprovalRequestId]);

CREATE INDEX [IX_OvertimeRequests_BulkSubmissionId] ON [attendance].[OvertimeRequests] ([BulkSubmissionId]);

CREATE INDEX [IX_OvertimeRequests_EmployeeId_StartsAtUtc] ON [attendance].[OvertimeRequests] ([EmployeeId], [StartsAtUtc]);

CREATE INDEX [IX_OvertimeRequests_EmployeeId_WorkSessionId] ON [attendance].[OvertimeRequests] ([EmployeeId], [WorkSessionId]);

CREATE INDEX [IX_OvertimeRequests_PolicyVersionId] ON [attendance].[OvertimeRequests] ([PolicyVersionId]);

CREATE INDEX [IX_OvertimeRequests_SiteId] ON [attendance].[OvertimeRequests] ([SiteId]);

CREATE UNIQUE INDEX [IX_PasswordResetChallenges_TokenHash] ON [auth].[PasswordResetChallenges] ([TokenHash]);

CREATE INDEX [IX_PasswordResetChallenges_UserAccountId_IssuedAt] ON [auth].[PasswordResetChallenges] ([UserAccountId], [IssuedAt]);

CREATE INDEX [IX_PayrollHandoffBatches_ApprovalRequestId] ON [hr].[PayrollHandoffBatches] ([ApprovalRequestId]);

CREATE UNIQUE INDEX [IX_PayrollHandoffBatches_BatchReference] ON [hr].[PayrollHandoffBatches] ([BatchReference]);

CREATE INDEX [IX_PayrollHandoffLines_ApprovalRequestId] ON [hr].[PayrollHandoffLines] ([ApprovalRequestId]);

CREATE INDEX [IX_PayrollHandoffLines_AttendancePeriodLockEventId] ON [hr].[PayrollHandoffLines] ([AttendancePeriodLockEventId]);

CREATE INDEX [IX_PayrollHandoffLines_EmployeeId] ON [hr].[PayrollHandoffLines] ([EmployeeId]);

CREATE INDEX [IX_PayrollHandoffLines_PayrollHandoffBatchId_Currency] ON [hr].[PayrollHandoffLines] ([PayrollHandoffBatchId], [Currency]);

CREATE UNIQUE INDEX [IX_PayrollHandoffLines_PayrollHandoffBatchId_EmployeeId_SourceType_SourceId_SourceVersion_Component] ON [hr].[PayrollHandoffLines] ([PayrollHandoffBatchId], [EmployeeId], [SourceType], [SourceId], [SourceVersion], [Component]);

CREATE INDEX [IX_PayrollHandoffLines_PolicyVersionId] ON [hr].[PayrollHandoffLines] ([PolicyVersionId]);

CREATE UNIQUE INDEX [IX_PayrollHandoffLines_TraceId] ON [hr].[PayrollHandoffLines] ([TraceId]);

CREATE UNIQUE INDEX [IX_PayrollHandoffProcessingRecords_PayrollHandoffBatchId_ExternalProcessingReference] ON [hr].[PayrollHandoffProcessingRecords] ([PayrollHandoffBatchId], [ExternalProcessingReference]);

CREATE UNIQUE INDEX [IX_Permissions_Code] ON [auth].[Permissions] ([Code]);

CREATE UNIQUE INDEX [IX_Policies_Code] ON [hr].[Policies] ([Code]);

CREATE INDEX [IX_PolicyAssignmentEnds_ApprovalRequestId] ON [hr].[PolicyAssignmentEnds] ([ApprovalRequestId]);

CREATE UNIQUE INDEX [IX_PolicyAssignmentEnds_PolicyAssignmentId] ON [hr].[PolicyAssignmentEnds] ([PolicyAssignmentId]);

CREATE INDEX [IX_PolicyAssignments_ApprovalRequestId] ON [hr].[PolicyAssignments] ([ApprovalRequestId]);

CREATE INDEX [IX_PolicyAssignments_DepartmentId] ON [hr].[PolicyAssignments] ([DepartmentId]);

CREATE INDEX [IX_PolicyAssignments_EmployeeId] ON [hr].[PolicyAssignments] ([EmployeeId]);

CREATE INDEX [IX_PolicyAssignments_PolicyVersionId] ON [hr].[PolicyAssignments] ([PolicyVersionId]);

CREATE INDEX [IX_PolicyAssignments_RoleId] ON [hr].[PolicyAssignments] ([RoleId]);

CREATE INDEX [IX_PolicyAssignments_SiteId] ON [hr].[PolicyAssignments] ([SiteId]);

CREATE INDEX [IX_PolicyAssignments_StaffCategoryId] ON [hr].[PolicyAssignments] ([StaffCategoryId]);

CREATE INDEX [IX_PolicyImpactAssessments_ApprovalRequestId] ON [hr].[PolicyImpactAssessments] ([ApprovalRequestId]);

CREATE INDEX [IX_PolicyImpactAssessments_EmployeeId] ON [hr].[PolicyImpactAssessments] ([EmployeeId]);

CREATE INDEX [IX_PolicyImpactAssessments_PolicyVersionId] ON [hr].[PolicyImpactAssessments] ([PolicyVersionId]);

CREATE INDEX [IX_PolicyVersions_ApprovalRequestId] ON [hr].[PolicyVersions] ([ApprovalRequestId]);

CREATE UNIQUE INDEX [IX_PolicyVersions_PolicyId_VersionNumber] ON [hr].[PolicyVersions] ([PolicyId], [VersionNumber]);

CREATE INDEX [IX_PreviousEmployments_EmployeeId] ON [hr].[PreviousEmployments] ([EmployeeId]);

CREATE INDEX [IX_ProjectMembers_EmployeeId_EffectiveFrom_EffectiveTo] ON [auth].[ProjectMembers] ([EmployeeId], [EffectiveFrom], [EffectiveTo]);

CREATE UNIQUE INDEX [IX_ProjectMembers_ProjectId_EmployeeId_EffectiveFrom] ON [auth].[ProjectMembers] ([ProjectId], [EmployeeId], [EffectiveFrom]);

CREATE UNIQUE INDEX [IX_Projects_Code] ON [auth].[Projects] ([Code]);

CREATE INDEX [IX_PunchAssessments_EmployeeId_FaceVerificationAttemptId] ON [attendance].[PunchAssessments] ([EmployeeId], [FaceVerificationAttemptId]);

CREATE INDEX [IX_PunchAssessments_EmployeeId_RawPunchEventId] ON [attendance].[PunchAssessments] ([EmployeeId], [RawPunchEventId]);

CREATE INDEX [IX_PunchAssessments_PolicyVersionId] ON [attendance].[PunchAssessments] ([PolicyVersionId]);

CREATE UNIQUE INDEX [IX_PunchAssessments_RawPunchEventId_Revision] ON [attendance].[PunchAssessments] ([RawPunchEventId], [Revision]);

CREATE UNIQUE INDEX [IX_RefreshTokens_ReplacedByTokenId] ON [auth].[RefreshTokens] ([ReplacedByTokenId]) WHERE [ReplacedByTokenId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [auth].[RefreshTokens] ([TokenHash]);

CREATE INDEX [IX_RefreshTokens_UserSessionId_ExpiresAt] ON [auth].[RefreshTokens] ([UserSessionId], [ExpiresAt]);

CREATE INDEX [IX_RefreshTokens_UserSessionId_ReplacedByTokenId] ON [auth].[RefreshTokens] ([UserSessionId], [ReplacedByTokenId]);

CREATE UNIQUE INDEX [IX_RegisteredDevices_UserAccountId_DeviceIdentifierHash] ON [auth].[RegisteredDevices] ([UserAccountId], [DeviceIdentifierHash]) WHERE [RevokedAt] IS NULL;

CREATE INDEX [IX_RepaymentLedgerEntries_AdvanceLoanAgreementId_Currency] ON [hr].[RepaymentLedgerEntries] ([AdvanceLoanAgreementId], [Currency]);

CREATE UNIQUE INDEX [IX_RepaymentLedgerEntries_AdvanceLoanAgreementId_IdempotencyKey] ON [hr].[RepaymentLedgerEntries] ([AdvanceLoanAgreementId], [IdempotencyKey]);

CREATE INDEX [IX_RepaymentLedgerEntries_RepaymentScheduleEntryId_AdvanceLoanAgreementId_Currency] ON [hr].[RepaymentLedgerEntries] ([RepaymentScheduleEntryId], [AdvanceLoanAgreementId], [Currency]);

CREATE INDEX [IX_RepaymentLedgerEntries_ReversesEntryId_AdvanceLoanAgreementId_Currency] ON [hr].[RepaymentLedgerEntries] ([ReversesEntryId], [AdvanceLoanAgreementId], [Currency]);

CREATE INDEX [IX_RepaymentScheduleEntries_AdvanceLoanAgreementId_Currency] ON [hr].[RepaymentScheduleEntries] ([AdvanceLoanAgreementId], [Currency]);

CREATE UNIQUE INDEX [IX_RepaymentScheduleEntries_AdvanceLoanAgreementId_Sequence] ON [hr].[RepaymentScheduleEntries] ([AdvanceLoanAgreementId], [Sequence]);

CREATE INDEX [IX_ReportExportJobs_RequestedByUserId_RequestedAt] ON [reports].[ReportExportJobs] ([RequestedByUserId], [RequestedAt]);

CREATE INDEX [IX_ReportExportJobs_State] ON [reports].[ReportExportJobs] ([State]);

CREATE INDEX [IX_ReportExportJobs_StoredFileId] ON [reports].[ReportExportJobs] ([StoredFileId]);

CREATE INDEX [IX_RolePermissions_PermissionId] ON [auth].[RolePermissions] ([PermissionId]);

CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_PermissionId] ON [auth].[RolePermissions] ([RoleId], [PermissionId]) WHERE [RevokedAt] IS NULL;

CREATE UNIQUE INDEX [IX_Roles_Code] ON [auth].[Roles] ([Code]);

CREATE INDEX [IX_ShiftPopulationAssignments_ApprovalRequestId] ON [attendance].[ShiftPopulationAssignments] ([ApprovalRequestId]);

CREATE INDEX [IX_ShiftPopulationAssignments_DepartmentId_EffectiveFrom] ON [attendance].[ShiftPopulationAssignments] ([DepartmentId], [EffectiveFrom]);

CREATE INDEX [IX_ShiftPopulationAssignments_ShiftId] ON [attendance].[ShiftPopulationAssignments] ([ShiftId]);

CREATE INDEX [IX_ShiftPopulationAssignments_SiteId_EffectiveFrom] ON [attendance].[ShiftPopulationAssignments] ([SiteId], [EffectiveFrom]);

CREATE INDEX [IX_ShiftPopulationAssignments_StaffCategoryId_EffectiveFrom] ON [attendance].[ShiftPopulationAssignments] ([StaffCategoryId], [EffectiveFrom]);

CREATE INDEX [IX_ShiftPopulationAssignments_TeamId_EffectiveFrom] ON [attendance].[ShiftPopulationAssignments] ([TeamId], [EffectiveFrom]);

CREATE INDEX [IX_SiteReportReviews_ApprovalRequestId] ON [attendance].[SiteReportReviews] ([ApprovalRequestId]);

CREATE UNIQUE INDEX [IX_SiteReportReviews_SiteReportId_Sequence] ON [attendance].[SiteReportReviews] ([SiteReportId], [Sequence]);

CREATE INDEX [IX_SiteReports_EmployeeId_EmployeeAssignmentId] ON [attendance].[SiteReports] ([EmployeeId], [EmployeeAssignmentId]);

CREATE UNIQUE INDEX [IX_SiteReports_EmployeeId_IdempotencyKey] ON [attendance].[SiteReports] ([EmployeeId], [IdempotencyKey]);

CREATE INDEX [IX_SiteReports_EmployeeId_UserAccountId] ON [attendance].[SiteReports] ([EmployeeId], [UserAccountId]);

CREATE INDEX [IX_SiteReports_SiteId_ReceivedAtUtc] ON [attendance].[SiteReports] ([SiteId], [ReceivedAtUtc]);

CREATE INDEX [IX_SiteReports_UserAccountId_RegisteredDeviceId] ON [attendance].[SiteReports] ([UserAccountId], [RegisteredDeviceId]);

CREATE INDEX [IX_StoredFiles_ScanState_UploadedAt] ON [files].[StoredFiles] ([ScanState], [UploadedAt]);

CREATE UNIQUE INDEX [IX_StoredFiles_StorageKey] ON [files].[StoredFiles] ([StorageKey]);

CREATE INDEX [IX_TeamMembers_EmployeeId_EffectiveFrom_EffectiveTo] ON [auth].[TeamMembers] ([EmployeeId], [EffectiveFrom], [EffectiveTo]);

CREATE UNIQUE INDEX [IX_TeamMembers_TeamId_EmployeeId_EffectiveFrom] ON [auth].[TeamMembers] ([TeamId], [EmployeeId], [EffectiveFrom]);

CREATE UNIQUE INDEX [IX_Teams_Code] ON [auth].[Teams] ([Code]);

CREATE INDEX [IX_TemporaryShiftChangeEmployees_EmployeeId] ON [attendance].[TemporaryShiftChangeEmployees] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_TemporaryShiftChangeEmployees_TemporaryShiftChangeId_EmployeeId] ON [attendance].[TemporaryShiftChangeEmployees] ([TemporaryShiftChangeId], [EmployeeId]);

CREATE INDEX [IX_TemporaryShiftChanges_ApprovalRequestId] ON [attendance].[TemporaryShiftChanges] ([ApprovalRequestId]);

CREATE INDEX [IX_TemporaryShiftChanges_OriginalShiftVersionId] ON [attendance].[TemporaryShiftChanges] ([OriginalShiftVersionId]);

CREATE INDEX [IX_TemporaryShiftChanges_ReplacementShiftVersionId] ON [attendance].[TemporaryShiftChanges] ([ReplacementShiftVersionId]);

CREATE INDEX [IX_TemporaryShiftChanges_SiteId_AttendanceDate] ON [attendance].[TemporaryShiftChanges] ([SiteId], [AttendanceDate]);

CREATE INDEX [IX_UserAccessScopes_DepartmentId] ON [auth].[UserAccessScopes] ([DepartmentId]);

CREATE INDEX [IX_UserAccessScopes_EmployeeId] ON [auth].[UserAccessScopes] ([EmployeeId]);

CREATE INDEX [IX_UserAccessScopes_ProjectId] ON [auth].[UserAccessScopes] ([ProjectId]);

CREATE INDEX [IX_UserAccessScopes_SiteId] ON [auth].[UserAccessScopes] ([SiteId]);

CREATE INDEX [IX_UserAccessScopes_TeamId] ON [auth].[UserAccessScopes] ([TeamId]);

CREATE UNIQUE INDEX [IX_UserAccessScopes_UserRoleAssignmentId_DepartmentId] ON [auth].[UserAccessScopes] ([UserRoleAssignmentId], [DepartmentId]) WHERE [DepartmentId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_UserAccessScopes_UserRoleAssignmentId_Kind] ON [auth].[UserAccessScopes] ([UserRoleAssignmentId], [Kind]) WHERE [Kind] = 7;

CREATE UNIQUE INDEX [IX_UserAccessScopes_UserRoleAssignmentId_Kind_EmployeeId] ON [auth].[UserAccessScopes] ([UserRoleAssignmentId], [Kind], [EmployeeId]) WHERE [EmployeeId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_UserAccessScopes_UserRoleAssignmentId_ProjectId] ON [auth].[UserAccessScopes] ([UserRoleAssignmentId], [ProjectId]) WHERE [ProjectId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_UserAccessScopes_UserRoleAssignmentId_SiteId] ON [auth].[UserAccessScopes] ([UserRoleAssignmentId], [SiteId]) WHERE [SiteId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_UserAccessScopes_UserRoleAssignmentId_TeamId] ON [auth].[UserAccessScopes] ([UserRoleAssignmentId], [TeamId]) WHERE [TeamId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_UserAccounts_EmployeeId] ON [auth].[UserAccounts] ([EmployeeId]);

CREATE INDEX [IX_UserAccounts_EmployeeId_ApprovalDecisionId] ON [auth].[UserAccounts] ([EmployeeId], [ApprovalDecisionId]);

CREATE INDEX [IX_UserAccounts_Id_CurrentSetupInvitationId] ON [auth].[UserAccounts] ([Id], [CurrentSetupInvitationId]);

CREATE INDEX [IX_UserAccounts_Id_InitialRoleAssignmentId] ON [auth].[UserAccounts] ([Id], [InitialRoleAssignmentId]);

CREATE INDEX [IX_UserAccounts_InitialRoleAssignmentId_InitialAccessScopeId] ON [auth].[UserAccounts] ([InitialRoleAssignmentId], [InitialAccessScopeId]);

CREATE UNIQUE INDEX [IX_UserAccounts_NormalizedLoginName] ON [auth].[UserAccounts] ([NormalizedLoginName]);

CREATE INDEX [IX_UserAccounts_Status_Id] ON [auth].[UserAccounts] ([Status], [Id]);

CREATE INDEX [IX_UserRoleAssignments_EmployeeId_UserAccountId] ON [auth].[UserRoleAssignments] ([EmployeeId], [UserAccountId]);

CREATE INDEX [IX_UserRoleAssignments_RoleId] ON [auth].[UserRoleAssignments] ([RoleId]);

CREATE INDEX [IX_UserRoleAssignments_UserAccountId_EffectiveFrom_EffectiveTo] ON [auth].[UserRoleAssignments] ([UserAccountId], [EffectiveFrom], [EffectiveTo]);

CREATE UNIQUE INDEX [IX_UserRoleAssignments_UserAccountId_RoleId_EffectiveFrom] ON [auth].[UserRoleAssignments] ([UserAccountId], [RoleId], [EffectiveFrom]);

CREATE UNIQUE INDEX [IX_UserSessions_MfaChallengeId] ON [auth].[UserSessions] ([MfaChallengeId]) WHERE [MfaChallengeId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_UserSessions_RefreshTokenFamilyId] ON [auth].[UserSessions] ([RefreshTokenFamilyId]);

CREATE INDEX [IX_UserSessions_UserAccountId_MfaChallengeId] ON [auth].[UserSessions] ([UserAccountId], [MfaChallengeId]);

CREATE INDEX [IX_UserSessions_UserAccountId_RegisteredDeviceId] ON [auth].[UserSessions] ([UserAccountId], [RegisteredDeviceId]);

CREATE INDEX [IX_UserSessions_UserAccountId_RevokedAt_ExpiresAt] ON [auth].[UserSessions] ([UserAccountId], [RevokedAt], [ExpiresAt]);

CREATE INDEX [IX_VehicleCustodies_EmployeeId] ON [hr].[VehicleCustodies] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_VehicleCustodies_VehicleId] ON [hr].[VehicleCustodies] ([VehicleId]) WHERE [ReturnedAt] IS NULL;

CREATE INDEX [IX_VehicleIncidents_ApprovalRequestId] ON [hr].[VehicleIncidents] ([ApprovalRequestId]);

CREATE INDEX [IX_VehicleIncidents_VehicleCustodyId] ON [hr].[VehicleIncidents] ([VehicleCustodyId]);

CREATE INDEX [IX_VehicleMileageEntries_ApprovalRequestId] ON [hr].[VehicleMileageEntries] ([ApprovalRequestId]);

CREATE INDEX [IX_VehicleMileageEntries_EmployeeId_BusinessDate] ON [hr].[VehicleMileageEntries] ([EmployeeId], [BusinessDate]);

CREATE INDEX [IX_VehicleMileageEntries_PolicyVersionId] ON [hr].[VehicleMileageEntries] ([PolicyVersionId]);

CREATE UNIQUE INDEX [IX_VehicleMileageEntries_VehicleId_IdempotencyKey] ON [hr].[VehicleMileageEntries] ([VehicleId], [IdempotencyKey]);

CREATE INDEX [IX_VehicleMileageEntries_VehicleTripId_EmployeeId_VehicleId] ON [hr].[VehicleMileageEntries] ([VehicleTripId], [EmployeeId], [VehicleId]);

CREATE UNIQUE INDEX [IX_Vehicles_RegistrationNumber] ON [hr].[Vehicles] ([RegistrationNumber]);

CREATE INDEX [IX_VehicleServiceRecords_ApprovalRequestId] ON [hr].[VehicleServiceRecords] ([ApprovalRequestId]);

CREATE INDEX [IX_VehicleServiceRecords_EmployeeId] ON [hr].[VehicleServiceRecords] ([EmployeeId]);

CREATE INDEX [IX_VehicleServiceRecords_PolicyVersionId] ON [hr].[VehicleServiceRecords] ([PolicyVersionId]);

CREATE INDEX [IX_VehicleServiceRecords_VehicleId] ON [hr].[VehicleServiceRecords] ([VehicleId]);

CREATE INDEX [IX_VehicleTrips_ApprovalRequestId] ON [hr].[VehicleTrips] ([ApprovalRequestId]);

CREATE INDEX [IX_VehicleTrips_EmployeeId] ON [hr].[VehicleTrips] ([EmployeeId]);

CREATE INDEX [IX_VehicleTrips_VehicleId] ON [hr].[VehicleTrips] ([VehicleId]);

CREATE INDEX [IX_WaitingTimeEvidences_ApprovalRequestId] ON [hr].[WaitingTimeEvidences] ([ApprovalRequestId]);

CREATE INDEX [IX_WaitingTimeEvidences_DelayedByEmployeeId] ON [hr].[WaitingTimeEvidences] ([DelayedByEmployeeId]);

CREATE INDEX [IX_WaitingTimeEvidences_EmployeeId] ON [hr].[WaitingTimeEvidences] ([EmployeeId]);

CREATE INDEX [IX_WaitingTimeEvidences_PolicyVersionId] ON [hr].[WaitingTimeEvidences] ([PolicyVersionId]);

CREATE INDEX [IX_WaitingTimeEvidences_SiteId] ON [hr].[WaitingTimeEvidences] ([SiteId]);

CREATE INDEX [IX_WorkSessions_EmployeeId_AttendanceDate] ON [attendance].[WorkSessions] ([EmployeeId], [AttendanceDate]);

CREATE INDEX [IX_WorkSessions_EmployeeId_CheckInPunchId] ON [attendance].[WorkSessions] ([EmployeeId], [CheckInPunchId]);

CREATE INDEX [IX_WorkSessions_EmployeeId_CheckOutPunchId] ON [attendance].[WorkSessions] ([EmployeeId], [CheckOutPunchId]);

CREATE INDEX [IX_WorkSessions_EmployeeId_CorrectionId] ON [attendance].[WorkSessions] ([EmployeeId], [CorrectionId]);

CREATE INDEX [IX_WorkSessions_PolicyVersionId] ON [attendance].[WorkSessions] ([PolicyVersionId]);

CREATE UNIQUE INDEX [IX_WorkSessions_SessionKey_Revision] ON [attendance].[WorkSessions] ([SessionKey], [Revision]);

CREATE INDEX [IX_WorkSessions_ShiftVersionId] ON [attendance].[WorkSessions] ([ShiftVersionId]);

CREATE INDEX [IX_WorkSessions_SiteId] ON [attendance].[WorkSessions] ([SiteId]);

CREATE INDEX [IX_ZoneAssignments_ApprovalRequestId] ON [attendance].[ZoneAssignments] ([ApprovalRequestId]);

CREATE INDEX [IX_ZoneAssignments_EmployeeId_EffectiveFromUtc] ON [attendance].[ZoneAssignments] ([EmployeeId], [EffectiveFromUtc]);

CREATE INDEX [IX_ZoneAssignments_RoleId_EffectiveFromUtc] ON [attendance].[ZoneAssignments] ([RoleId], [EffectiveFromUtc]);

CREATE INDEX [IX_ZoneAssignments_SiteId_EffectiveFromUtc] ON [attendance].[ZoneAssignments] ([SiteId], [EffectiveFromUtc]);

CREATE INDEX [IX_ZoneAssignments_TeamId_EffectiveFromUtc] ON [attendance].[ZoneAssignments] ([TeamId], [EffectiveFromUtc]);

CREATE INDEX [IX_ZoneEventResponses_ApprovalRequestId] ON [attendance].[ZoneEventResponses] ([ApprovalRequestId]);

CREATE INDEX [IX_ZoneEventResponses_ZoneEventId_AtUtc] ON [attendance].[ZoneEventResponses] ([ZoneEventId], [AtUtc]);

CREATE UNIQUE INDEX [IX_ZoneEvents_EmployeeId_IdempotencyKey] ON [attendance].[ZoneEvents] ([EmployeeId], [IdempotencyKey]);

CREATE INDEX [IX_ZoneEvents_EmployeeId_ReceivedAtUtc] ON [attendance].[ZoneEvents] ([EmployeeId], [ReceivedAtUtc]);

CREATE INDEX [IX_ZoneEvents_EmployeeId_UserAccountId] ON [attendance].[ZoneEvents] ([EmployeeId], [UserAccountId]);

CREATE INDEX [IX_ZoneEvents_EmployeeId_ZoneAssignmentId_DutyMonitoringWindowId] ON [attendance].[ZoneEvents] ([EmployeeId], [ZoneAssignmentId], [DutyMonitoringWindowId]);

CREATE INDEX [IX_ZoneEvents_UserAccountId_RegisteredDeviceId] ON [attendance].[ZoneEvents] ([UserAccountId], [RegisteredDeviceId]);

CREATE INDEX [IX_ZoneEvents_ZoneVersionId_ZoneAssignmentId] ON [attendance].[ZoneEvents] ([ZoneVersionId], [ZoneAssignmentId]);

CREATE INDEX [IX_ZoneExceptionRequests_ApprovalRequestId] ON [attendance].[ZoneExceptionRequests] ([ApprovalRequestId]);

CREATE INDEX [IX_ZoneExceptionRequests_EmployeeId_StartsAtUtc] ON [attendance].[ZoneExceptionRequests] ([EmployeeId], [StartsAtUtc]);

CREATE INDEX [IX_ZoneExceptionRequests_ZoneVersionId] ON [attendance].[ZoneExceptionRequests] ([ZoneVersionId]);

CREATE UNIQUE INDEX [IX_ZoneTypes_Code] ON [attendance].[ZoneTypes] ([Code]);

CREATE INDEX [IX_ZoneVersions_ApprovalRequestId] ON [attendance].[ZoneVersions] ([ApprovalRequestId]);

CREATE INDEX [IX_ZoneVersions_OperationalZoneId_EffectiveFromUtc] ON [attendance].[ZoneVersions] ([OperationalZoneId], [EffectiveFromUtc]);

CREATE UNIQUE INDEX [IX_ZoneVersions_OperationalZoneId_VersionNumber] ON [attendance].[ZoneVersions] ([OperationalZoneId], [VersionNumber]);

CREATE INDEX [IX_ZoneVersions_ZoneTypeId] ON [attendance].[ZoneVersions] ([ZoneTypeId]);

ALTER TABLE [hr].[Branches] ADD CONSTRAINT [FK_Branches_Cities_CityId] FOREIGN KEY ([CityId]) REFERENCES [hr].[Cities] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [hr].[EmployeeAddresses] ADD CONSTRAINT [FK_EmployeeAddresses_Cities_CityId] FOREIGN KEY ([CityId]) REFERENCES [hr].[Cities] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [hr].[Employees] ADD CONSTRAINT [FK_Employees_EmployeeOnboardingDecisions_Id_ApprovedOnboardingDecisionId] FOREIGN KEY ([Id], [ApprovedOnboardingDecisionId]) REFERENCES [hr].[EmployeeOnboardingDecisions] ([EmployeeId], [Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_StoredFiles_EvidenceFileId] FOREIGN KEY ([EvidenceFileId]) REFERENCES [files].[StoredFiles] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[Sites] ADD CONSTRAINT [FK_Sites_Cities_CityId] FOREIGN KEY ([CityId]) REFERENCES [hr].[Cities] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[Sites] ADD CONSTRAINT [FK_Sites_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [auth].[Projects] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [auth].[AccountSetupInvitations] ADD CONSTRAINT [FK_AccountSetupInvitations_UserAccounts_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[FaceVerificationAttempts] ADD CONSTRAINT [FK_FaceVerificationAttempts_RegisteredDevices_UserAccountId_RegisteredDeviceKey] FOREIGN KEY ([UserAccountId], [RegisteredDeviceKey]) REFERENCES [auth].[RegisteredDevices] ([UserAccountId], [Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[FaceVerificationAttempts] ADD CONSTRAINT [FK_FaceVerificationAttempts_UserAccounts_EmployeeId_UserAccountId] FOREIGN KEY ([EmployeeId], [UserAccountId]) REFERENCES [auth].[UserAccounts] ([EmployeeId], [Id]) ON DELETE NO ACTION;

ALTER TABLE [auth].[MfaChallenges] ADD CONSTRAINT [FK_MfaChallenges_MfaCredentials_UserAccountId_VerifiedCredentialId] FOREIGN KEY ([UserAccountId], [VerifiedCredentialId]) REFERENCES [auth].[MfaCredentials] ([UserAccountId], [Id]) ON DELETE NO ACTION;

ALTER TABLE [auth].[MfaChallenges] ADD CONSTRAINT [FK_MfaChallenges_RegisteredDevices_UserAccountId_RegisteredDeviceId] FOREIGN KEY ([UserAccountId], [RegisteredDeviceId]) REFERENCES [auth].[RegisteredDevices] ([UserAccountId], [Id]) ON DELETE NO ACTION;

ALTER TABLE [auth].[MfaChallenges] ADD CONSTRAINT [FK_MfaChallenges_UserAccounts_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [auth].[MfaCredentials] ADD CONSTRAINT [FK_MfaCredentials_UserAccounts_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [auth].[PasswordResetChallenges] ADD CONSTRAINT [FK_PasswordResetChallenges_UserAccounts_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [auth].[RefreshTokens] ADD CONSTRAINT [FK_RefreshTokens_UserSessions_UserSessionId] FOREIGN KEY ([UserSessionId]) REFERENCES [auth].[UserSessions] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [auth].[RegisteredDevices] ADD CONSTRAINT [FK_RegisteredDevices_UserAccounts_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[SiteReportReviews] ADD CONSTRAINT [FK_SiteReportReviews_SiteReports_SiteReportId] FOREIGN KEY ([SiteReportId]) REFERENCES [attendance].[SiteReports] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[SiteReports] ADD CONSTRAINT [FK_SiteReports_UserAccounts_EmployeeId_UserAccountId] FOREIGN KEY ([EmployeeId], [UserAccountId]) REFERENCES [auth].[UserAccounts] ([EmployeeId], [Id]) ON DELETE NO ACTION;

ALTER TABLE [auth].[UserAccessScopes] ADD CONSTRAINT [FK_UserAccessScopes_UserRoleAssignments_UserRoleAssignmentId] FOREIGN KEY ([UserRoleAssignmentId]) REFERENCES [auth].[UserRoleAssignments] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [auth].[UserAccounts] ADD CONSTRAINT [FK_UserAccounts_UserRoleAssignments_Id_InitialRoleAssignmentId] FOREIGN KEY ([Id], [InitialRoleAssignmentId]) REFERENCES [auth].[UserRoleAssignments] ([UserAccountId], [Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261006074233_CompleteReviewedHrAttendanceDataModel', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM [hr].[AllowanceClaims] WHERE [SubmittedAt] IS NOT NULL OR [ApprovalRequestId] IS NOT NULL)
    OR EXISTS (SELECT 1 FROM [hr].[AllowanceSettlementRecords])
    THROW 51002, 'Migration stopped: existing submitted allowance claims or settlements require a reviewed submission/payment-history conversion.', 1;

ALTER TABLE [auth].[TeamMembers] ADD [TerminatedAt] datetimeoffset NULL;

ALTER TABLE [auth].[TeamMembers] ADD [TerminatedByUserId] uniqueidentifier NULL;

ALTER TABLE [auth].[TeamMembers] ADD [TerminatedEffectiveTo] date NULL;

ALTER TABLE [auth].[TeamMembers] ADD [TerminationBusinessDate] date NULL;

ALTER TABLE [auth].[TeamMembers] ADD [TerminationReason] nvarchar(1000) NULL;

ALTER TABLE [auth].[ProjectMembers] ADD [TerminatedAt] datetimeoffset NULL;

ALTER TABLE [auth].[ProjectMembers] ADD [TerminatedByUserId] uniqueidentifier NULL;

ALTER TABLE [auth].[ProjectMembers] ADD [TerminatedEffectiveTo] date NULL;

ALTER TABLE [auth].[ProjectMembers] ADD [TerminationBusinessDate] date NULL;

ALTER TABLE [auth].[ProjectMembers] ADD [TerminationReason] nvarchar(1000) NULL;

ALTER TABLE [hr].[EmployeeDocuments] ADD [HasScanConflict] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [hr].[EmployeeDocuments] ADD [VerifiedScanAt] datetimeoffset NULL;

ALTER TABLE [hr].[AllowanceSettlementRecords] ADD [SubmissionVersion] int NOT NULL DEFAULT 0;

ALTER TABLE [hr].[AllowanceClaims] ADD [LastSettledAt] datetimeoffset NULL;

ALTER TABLE [hr].[AllowanceClaims] ADD [SettlementState] int NOT NULL DEFAULT 0;

ALTER TABLE [hr].[AllowanceClaims] ADD [SubmissionVersion] int NOT NULL DEFAULT 0;

CREATE TABLE [hr].[AllowanceClaimSubmissions] (
    [Id] uniqueidentifier NOT NULL,
    [AllowanceClaimId] uniqueidentifier NOT NULL,
    [Version] int NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [SnapshotPrivateReference] nvarchar(500) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ClaimedAmount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [PolicyVersionId] uniqueidentifier NOT NULL,
    [CalculationPrivateReference] nvarchar(500) NOT NULL,
    [EvidencePrivateReference] nvarchar(500) NULL,
    [PayableDate] date NOT NULL,
    [PayrollPeriodStart] date NULL,
    [CorrectionReason] nvarchar(2000) NULL,
    [SubmittedByUserId] nvarchar(200) NOT NULL,
    [SubmittedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AllowanceClaimSubmissions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_AllowanceClaimSubmissions_AllowanceClaimId_Version] UNIQUE ([AllowanceClaimId], [Version]),
    CONSTRAINT [CK_AllowanceClaimSubmissions_Amount] CHECK ([ClaimedAmount] >= 0),
    CONSTRAINT [CK_AllowanceClaimSubmissions_Correction] CHECK (([Version] = 1 AND [CorrectionReason] IS NULL) OR ([Version] > 1 AND [CorrectionReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CorrectionReason]))) > 0)),
    CONSTRAINT [CK_AllowanceClaimSubmissions_Version] CHECK ([Version] > 0),
    CONSTRAINT [FK_AllowanceClaimSubmissions_AllowanceClaims_AllowanceClaimId] FOREIGN KEY ([AllowanceClaimId]) REFERENCES [hr].[AllowanceClaims] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllowanceClaimSubmissions_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AllowanceClaimSubmissions_PolicyVersions_PolicyVersionId] FOREIGN KEY ([PolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ShiftPopulationAssignmentEnds] (
    [Id] uniqueidentifier NOT NULL,
    [ShiftPopulationAssignmentId] uniqueidentifier NOT NULL,
    [EffectiveTo] date NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ShiftPopulationAssignmentEnds] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ShiftPopulationAssignmentEnds_Reason] CHECK (LEN(LTRIM(RTRIM([Reason]))) > 0),
    CONSTRAINT [FK_ShiftPopulationAssignmentEnds_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ShiftPopulationAssignmentEnds_ShiftPopulationAssignments_ShiftPopulationAssignmentId] FOREIGN KEY ([ShiftPopulationAssignmentId]) REFERENCES [attendance].[ShiftPopulationAssignments] ([Id]) ON DELETE NO ACTION
);

ALTER TABLE [auth].[TeamMembers] ADD CONSTRAINT [CK_TeamMembers_Termination] CHECK (([TerminatedEffectiveTo] IS NULL AND [TerminationReason] IS NULL AND [TerminatedByUserId] IS NULL AND [TerminatedAt] IS NULL AND [TerminationBusinessDate] IS NULL) OR ([TerminatedEffectiveTo] IS NOT NULL AND [TerminationReason] IS NOT NULL AND [TerminatedByUserId] IS NOT NULL AND [TerminatedAt] IS NOT NULL AND [TerminationBusinessDate] IS NOT NULL AND LEN(LTRIM(RTRIM([TerminationReason]))) > 0 AND [TerminatedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [TerminatedEffectiveTo] > [EffectiveFrom] AND [TerminatedEffectiveTo] >= [TerminationBusinessDate] AND ([EffectiveTo] IS NULL OR ([TerminatedEffectiveTo] <= [EffectiveTo] AND [EffectiveTo] > [TerminationBusinessDate]))));

ALTER TABLE [auth].[ProjectMembers] ADD CONSTRAINT [CK_ProjectMembers_Termination] CHECK (([TerminatedEffectiveTo] IS NULL AND [TerminationReason] IS NULL AND [TerminatedByUserId] IS NULL AND [TerminatedAt] IS NULL AND [TerminationBusinessDate] IS NULL) OR ([TerminatedEffectiveTo] IS NOT NULL AND [TerminationReason] IS NOT NULL AND [TerminatedByUserId] IS NOT NULL AND [TerminatedAt] IS NOT NULL AND [TerminationBusinessDate] IS NOT NULL AND LEN(LTRIM(RTRIM([TerminationReason]))) > 0 AND [TerminatedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [TerminatedEffectiveTo] > [EffectiveFrom] AND [TerminatedEffectiveTo] >= [TerminationBusinessDate] AND ([EffectiveTo] IS NULL OR ([TerminatedEffectiveTo] <= [EffectiveTo] AND [EffectiveTo] > [TerminationBusinessDate]))));

ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_ScanConflict] CHECK ([HasScanConflict] = 0 OR [ScanStatus] <> 0);

ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_VerifiedScan] CHECK ([VerifiedScanAt] IS NULL OR ([VerifiedAt] IS NOT NULL AND [ScannedAt] IS NOT NULL AND [VerifiedScanAt] >= [UploadedAt] AND [VerifiedScanAt] <= [VerifiedAt] AND [VerifiedScanAt] <= [ScannedAt]));

CREATE UNIQUE INDEX [IX_AllowanceSettlementRecords_AllowanceClaimId_State] ON [hr].[AllowanceSettlementRecords] ([AllowanceClaimId], [State]) WHERE [State] > 0;

CREATE INDEX [IX_AllowanceSettlementRecords_AllowanceClaimId_SubmissionVersion] ON [hr].[AllowanceSettlementRecords] ([AllowanceClaimId], [SubmissionVersion]);

ALTER TABLE [hr].[AllowanceSettlementRecords] ADD CONSTRAINT [CK_AllowanceSettlementRecords_Submission] CHECK ([SubmissionVersion] > 0 AND [State] BETWEEN 1 AND 2);

ALTER TABLE [hr].[AllowanceClaims] ADD CONSTRAINT [CK_AllowanceClaims_Settlement] CHECK (([SettlementState] = 0 AND [LastSettledAt] IS NULL) OR ([SettlementState] BETWEEN 1 AND 2 AND [LastSettledAt] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [LastSettledAt] >= [SubmittedAt]));

ALTER TABLE [hr].[AllowanceClaims] ADD CONSTRAINT [CK_AllowanceClaims_SubmissionVersion] CHECK (([SubmissionVersion] = 0 AND [SubmittedAt] IS NULL) OR ([SubmissionVersion] > 0 AND [SubmittedAt] IS NOT NULL));

CREATE UNIQUE INDEX [IX_AllowanceClaimSubmissions_AllowanceClaimId_SnapshotPrivateReference] ON [hr].[AllowanceClaimSubmissions] ([AllowanceClaimId], [SnapshotPrivateReference]);

CREATE UNIQUE INDEX [IX_AllowanceClaimSubmissions_ApprovalRequestId] ON [hr].[AllowanceClaimSubmissions] ([ApprovalRequestId]);

CREATE INDEX [IX_AllowanceClaimSubmissions_PolicyVersionId] ON [hr].[AllowanceClaimSubmissions] ([PolicyVersionId]);

CREATE INDEX [IX_ShiftPopulationAssignmentEnds_ApprovalRequestId] ON [attendance].[ShiftPopulationAssignmentEnds] ([ApprovalRequestId]);

CREATE UNIQUE INDEX [IX_ShiftPopulationAssignmentEnds_ShiftPopulationAssignmentId] ON [attendance].[ShiftPopulationAssignmentEnds] ([ShiftPopulationAssignmentId]);

ALTER TABLE [hr].[AllowanceSettlementRecords] ADD CONSTRAINT [FK_AllowanceSettlementRecords_AllowanceClaimSubmissions_AllowanceClaimId_SubmissionVersion] FOREIGN KEY ([AllowanceClaimId], [SubmissionVersion]) REFERENCES [hr].[AllowanceClaimSubmissions] ([AllowanceClaimId], [Version]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261006105213_CorrectReviewedModelLifecycles', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [auth].[UserAccounts] DROP CONSTRAINT [CK_UserAccounts_Active];

ALTER TABLE [auth].[UserAccounts] ADD [MobileAccessEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [auth].[UserAccounts] ADD [PhoneVerifiedAt] datetimeoffset NULL;

ALTER TABLE [auth].[UserAccounts] ADD [WebAccessEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [hr].[EmployeeSubmissions] ADD [ApprovedLoginPhoneNumber] varchar(16) COLLATE Latin1_General_100_BIN2 NULL;

ALTER TABLE [hr].[EmployeeSubmissions] ADD [MobileAccessRequested] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [hr].[Employees] ADD [ApprovedLoginPhoneNumber] varchar(16) COLLATE Latin1_General_100_BIN2 NULL;

ALTER TABLE [hr].[Employees] ADD [MobileAccessRequested] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE TABLE [auth].[AuthRateBuckets] (
    [Key] nvarchar(150) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [WindowStartsAt] datetimeoffset NOT NULL,
    [WindowEndsAt] datetimeoffset NOT NULL,
    [Count] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_AuthRateBuckets] PRIMARY KEY ([Key]),
    CONSTRAINT [CK_AuthRateBuckets_Count] CHECK ([Count] >= 0),
    CONSTRAINT [CK_AuthRateBuckets_Window] CHECK ([WindowEndsAt] > [WindowStartsAt])
);

CREATE TABLE [auth].[LoginPhoneBindings] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [PhoneNumber] varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ApprovedSubmissionId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [VerifiedAt] datetimeoffset NULL,
    [RevokedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_LoginPhoneBindings] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_LoginPhoneBindings_UserAccountId_Id] UNIQUE ([UserAccountId], [Id]),
    CONSTRAINT [CK_LoginPhoneBindings_Dates] CHECK (([VerifiedAt] IS NULL OR [VerifiedAt] >= [CreatedAt]) AND ([RevokedAt] IS NULL OR ([RevokedAt] >= [CreatedAt] AND ([VerifiedAt] IS NULL OR [RevokedAt] >= [VerifiedAt])))),
    CONSTRAINT [CK_LoginPhoneBindings_Phone] CHECK (DATALENGTH([PhoneNumber]) BETWEEN 3 AND 16 AND LEFT([PhoneNumber], 1) = '+' AND SUBSTRING([PhoneNumber], 2, 1) BETWEEN '1' AND '9' AND SUBSTRING([PhoneNumber], 2, 15) NOT LIKE '%[^0-9]%'),
    CONSTRAINT [FK_LoginPhoneBindings_EmployeeSubmissions_ApprovedSubmissionId] FOREIGN KEY ([ApprovedSubmissionId]) REFERENCES [hr].[EmployeeSubmissions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LoginPhoneBindings_UserAccounts_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[MobileOtpChallenges] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [PhoneBindingId] uniqueidentifier NOT NULL,
    [Purpose] varchar(30) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [SecurityVersion] int NOT NULL,
    [DeviceHash] varchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Platform] nvarchar(40) NOT NULL,
    [DeviceName] nvarchar(150) NOT NULL,
    [Verifier] varchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [IssuedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [MaximumAttempts] int NOT NULL,
    [FailedAttempts] int NOT NULL,
    [ConsumedAt] datetimeoffset NULL,
    [RevokedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_MobileOtpChallenges] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_MobileOtpChallenges_Attempts] CHECK ([SecurityVersion] > 0 AND [MaximumAttempts] > 0 AND [FailedAttempts] BETWEEN 0 AND [MaximumAttempts] AND ([ConsumedAt] IS NULL OR [FailedAttempts] < [MaximumAttempts])),
    CONSTRAINT [CK_MobileOtpChallenges_Dates] CHECK ([ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt])) AND ([RevokedAt] IS NULL OR ([RevokedAt] >= [IssuedAt] AND ([ConsumedAt] IS NULL OR [RevokedAt] >= [ConsumedAt])))),
    CONSTRAINT [CK_MobileOtpChallenges_Purpose] CHECK ([Purpose] = 'MobileLogin'),
    CONSTRAINT [FK_MobileOtpChallenges_LoginPhoneBindings_UserAccountId_PhoneBindingId] FOREIGN KEY ([UserAccountId], [PhoneBindingId]) REFERENCES [auth].[LoginPhoneBindings] ([UserAccountId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[OtpDeliveries] (
    [Id] uniqueidentifier NOT NULL,
    [ChallengeId] uniqueidentifier NOT NULL,
    [ProtectedPayload] varchar(8000) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [Attempts] int NOT NULL,
    [Status] int NOT NULL,
    [ClaimedAt] datetimeoffset NULL,
    [LeaseUntil] datetimeoffset NULL,
    [SubmittedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_OtpDeliveries] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_OtpDeliveries_Claim] CHECK (([Attempts] = 0 AND [ClaimedAt] IS NULL AND [LeaseUntil] IS NULL) OR ([Attempts] = 1 AND [ClaimedAt] IS NOT NULL AND [LeaseUntil] IS NOT NULL AND [ClaimedAt] >= [CreatedAt] AND [ClaimedAt] < [ExpiresAt] AND [LeaseUntil] > [ClaimedAt] AND [LeaseUntil] <= [ExpiresAt])),
    CONSTRAINT [CK_OtpDeliveries_Lifetime] CHECK ([ExpiresAt] > [CreatedAt]),
    CONSTRAINT [CK_OtpDeliveries_Payload] CHECK (([Status] IN (0,4) AND [ProtectedPayload] IS NOT NULL AND DATALENGTH([ProtectedPayload]) > 0) OR ([Status] IN (1,2,3) AND [ProtectedPayload] IS NULL)),
    CONSTRAINT [CK_OtpDeliveries_Status] CHECK ([Status] IN (0,1,2,3,4) AND ([Status] <> 0 OR [Attempts] = 0) AND ([Status] NOT IN (1,4) OR [Attempts] = 1)),
    CONSTRAINT [CK_OtpDeliveries_Submitted] CHECK (([Status] = 1 AND [SubmittedAt] IS NOT NULL AND [ClaimedAt] IS NOT NULL AND [LeaseUntil] IS NOT NULL AND [SubmittedAt] >= [ClaimedAt] AND [SubmittedAt] < [LeaseUntil] AND [SubmittedAt] < [ExpiresAt]) OR ([Status] <> 1 AND [SubmittedAt] IS NULL)),
    CONSTRAINT [FK_OtpDeliveries_MobileOtpChallenges_ChallengeId] FOREIGN KEY ([ChallengeId]) REFERENCES [auth].[MobileOtpChallenges] ([Id]) ON DELETE NO ACTION
);

ALTER TABLE [auth].[UserAccounts] ADD CONSTRAINT [CK_UserAccounts_Active] CHECK ([Status] <> 3 OR (([PasswordHash] IS NOT NULL OR [PhoneVerifiedAt] IS NOT NULL) AND [ActivatedAt] IS NOT NULL AND [InitialRoleAssignmentId] IS NOT NULL));

ALTER TABLE [auth].[UserAccounts] ADD CONSTRAINT [CK_UserAccounts_PhoneVerified] CHECK ([PhoneVerifiedAt] IS NULL OR ([ActivatedAt] IS NOT NULL AND [PhoneVerifiedAt] >= [CreatedAt]));

ALTER TABLE [auth].[UserAccounts] ADD CONSTRAINT [CK_UserAccounts_WebAccess] CHECK ([WebAccessEnabled] = 0 OR [PasswordHash] IS NOT NULL);

ALTER TABLE [hr].[EmployeeSubmissions] ADD CONSTRAINT [CK_EmployeeSubmissions_MobileEnrollment] CHECK (([MobileAccessRequested] = 0 AND [ApprovedLoginPhoneNumber] IS NULL) OR ([MobileAccessRequested] = 1 AND [ApprovedLoginPhoneNumber] IS NOT NULL));

ALTER TABLE [hr].[Employees] ADD CONSTRAINT [CK_Employees_MobileEnrollment] CHECK (([MobileAccessRequested] = 0 AND [ApprovedLoginPhoneNumber] IS NULL) OR ([MobileAccessRequested] = 1 AND [ApprovedLoginPhoneNumber] IS NOT NULL));

CREATE INDEX [IX_AuthRateBuckets_WindowEndsAt] ON [auth].[AuthRateBuckets] ([WindowEndsAt]);

CREATE INDEX [IX_LoginPhoneBindings_ApprovedSubmissionId] ON [auth].[LoginPhoneBindings] ([ApprovedSubmissionId]);

CREATE UNIQUE INDEX [IX_LoginPhoneBindings_PhoneNumber] ON [auth].[LoginPhoneBindings] ([PhoneNumber]) WHERE [RevokedAt] IS NULL;

CREATE UNIQUE INDEX [IX_LoginPhoneBindings_UserAccountId] ON [auth].[LoginPhoneBindings] ([UserAccountId]) WHERE [RevokedAt] IS NULL;

CREATE INDEX [IX_MobileOtpChallenges_PhoneBindingId_ExpiresAt] ON [auth].[MobileOtpChallenges] ([PhoneBindingId], [ExpiresAt]);

CREATE INDEX [IX_MobileOtpChallenges_UserAccountId_IssuedAt] ON [auth].[MobileOtpChallenges] ([UserAccountId], [IssuedAt]);

CREATE INDEX [IX_MobileOtpChallenges_UserAccountId_PhoneBindingId] ON [auth].[MobileOtpChallenges] ([UserAccountId], [PhoneBindingId]);

CREATE UNIQUE INDEX [IX_OtpDeliveries_ChallengeId] ON [auth].[OtpDeliveries] ([ChallengeId]);

CREATE INDEX [IX_OtpDeliveries_Status_ExpiresAt_CreatedAt] ON [auth].[OtpDeliveries] ([Status], [ExpiresAt], [CreatedAt]);

CREATE INDEX [IX_OtpDeliveries_Status_LeaseUntil] ON [auth].[OtpDeliveries] ([Status], [LeaseUntil]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261006144249_AddSharedMobileAuthentication', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [auth].[MobileOtpChallenges] DROP CONSTRAINT [CK_MobileOtpChallenges_Purpose];

ALTER TABLE [hr].[EmployeeSubmissions] DROP CONSTRAINT [CK_EmployeeSubmissions_MobileEnrollment];

ALTER TABLE [hr].[Employees] DROP CONSTRAINT [CK_Employees_MobileEnrollment];

ALTER TABLE [auth].[UserSessions] ADD [SmsOtpChallengeId] uniqueidentifier NULL;

ALTER TABLE [auth].[Roles] ADD [SystemRole] int NOT NULL DEFAULT 0;

ALTER TABLE [auth].[MobileOtpChallenges] ADD CONSTRAINT [AK_MobileOtpChallenges_UserAccountId_Id] UNIQUE ([UserAccountId], [Id]);

CREATE UNIQUE INDEX [IX_UserSessions_SmsOtpChallengeId] ON [auth].[UserSessions] ([SmsOtpChallengeId]) WHERE [SmsOtpChallengeId] IS NOT NULL;

CREATE INDEX [IX_UserSessions_UserAccountId_SmsOtpChallengeId] ON [auth].[UserSessions] ([UserAccountId], [SmsOtpChallengeId]);

ALTER TABLE [auth].[UserSessions] ADD CONSTRAINT [CK_UserSessions_SmsProof] CHECK ([SmsOtpChallengeId] IS NULL OR ([Client] = 1 AND [MfaChallengeId] IS NULL AND [RegisteredDeviceId] IS NULL));

CREATE UNIQUE INDEX [IX_Roles_SystemRole] ON [auth].[Roles] ([SystemRole]) WHERE [SystemRole] <> 0;

ALTER TABLE [auth].[Roles] ADD CONSTRAINT [CK_Roles_SystemRole] CHECK ([SystemRole] IN (0, 1, 2));

ALTER TABLE [auth].[MobileOtpChallenges] ADD CONSTRAINT [CK_MobileOtpChallenges_Purpose] CHECK (([Purpose] = 'MobileLogin' AND [Platform] IN ('android','ios')) OR ([Purpose] = 'WebLogin' AND [Platform] = 'web'));

ALTER TABLE [hr].[EmployeeSubmissions] ADD CONSTRAINT [CK_EmployeeSubmissions_MobileEnrollment] CHECK ([MobileAccessRequested] = 0 OR [ApprovedLoginPhoneNumber] IS NOT NULL);

ALTER TABLE [hr].[Employees] ADD CONSTRAINT [CK_Employees_MobileEnrollment] CHECK ([MobileAccessRequested] = 0 OR [ApprovedLoginPhoneNumber] IS NOT NULL);

ALTER TABLE [auth].[UserSessions] ADD CONSTRAINT [FK_UserSessions_MobileOtpChallenges_UserAccountId_SmsOtpChallengeId] FOREIGN KEY ([UserAccountId], [SmsOtpChallengeId]) REFERENCES [auth].[MobileOtpChallenges] ([UserAccountId], [Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007080926_AddWebSmsAuthenticationAndProtectedRoles', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [hr].[EmployeeOnboardingDecisions] DROP CONSTRAINT [FK_EmployeeOnboardingDecisions_EmployeeSubmissions_EmployeeId_SubmissionId];

ALTER TABLE [auth].[MobileOtpChallenges] DROP CONSTRAINT [CK_MobileOtpChallenges_Purpose];

DROP INDEX [IX_EmployeeOnboardingDecisions_EmployeeId_SubmissionId] ON [hr].[EmployeeOnboardingDecisions];

ALTER TABLE [auth].[LoginPhoneBindings] ADD [LoginPhoneChangeId] uniqueidentifier NULL;

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeSubmissions]') AND [c].[name] = N'SubmittedByUserId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeSubmissions] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [hr].[EmployeeSubmissions] ALTER COLUMN [SubmittedByUserId] uniqueidentifier NULL;

ALTER TABLE [hr].[EmployeeSubmissions] ADD [ExternalSubmitterReference] nvarchar(200) NULL;

ALTER TABLE [hr].[EmployeeSubmissions] ADD [Provenance] int NOT NULL DEFAULT 0;

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeOnboardingDecisions]') AND [c].[name] = N'ActorUserId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeOnboardingDecisions] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [hr].[EmployeeOnboardingDecisions] ALTER COLUMN [ActorUserId] uniqueidentifier NULL;

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeOnboardingDecisions]') AND [c].[name] = N'ActorRoleId');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeOnboardingDecisions] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [hr].[EmployeeOnboardingDecisions] ALTER COLUMN [ActorRoleId] uniqueidentifier NULL;

ALTER TABLE [hr].[EmployeeOnboardingDecisions] ADD [ExternalApproverReference] nvarchar(200) NULL;

ALTER TABLE [hr].[EmployeeOnboardingDecisions] ADD [OwnerApprovalReference] nvarchar(500) NULL;

ALTER TABLE [hr].[EmployeeOnboardingDecisions] ADD [Provenance] int NOT NULL DEFAULT 0;

ALTER TABLE [hr].[EmployeeSubmissions] ADD CONSTRAINT [AK_EmployeeSubmissions_EmployeeId_Id_Provenance] UNIQUE ([EmployeeId], [Id], [Provenance]);

CREATE TABLE [auth].[LoginPhoneChanges] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [PreviousBindingId] uniqueidentifier NOT NULL,
    [NewPhoneNumber] varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ApprovedByUserId] uniqueidentifier NOT NULL,
    [ApprovedByAssignmentId] uniqueidentifier NOT NULL,
    [ApprovedBySystemRole] int NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [ApprovalReference] nvarchar(500) NOT NULL,
    [SecurityVersion] int NOT NULL,
    [ApprovedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [CompletedAt] datetimeoffset NULL,
    [CompletionChallengeId] uniqueidentifier NULL,
    [SupersededAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_LoginPhoneChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_LoginPhoneChanges_UserAccountId_Id] UNIQUE ([UserAccountId], [Id]),
    CONSTRAINT [CK_LoginPhoneChanges_Approval] CHECK ([ApprovedByUserId] <> [UserAccountId] AND [ApprovedBySystemRole] IN (1,2) AND [SecurityVersion] > 1 AND [ExpiresAt] > [ApprovedAt] AND [ExpiresAt] <= DATEADD(hour,24,[ApprovedAt])),
    CONSTRAINT [CK_LoginPhoneChanges_Completion] CHECK (([CompletedAt] IS NULL AND [CompletionChallengeId] IS NULL) OR ([CompletedAt] IS NOT NULL AND [CompletionChallengeId] IS NOT NULL AND [CompletedAt] >= [ApprovedAt] AND [CompletedAt] < [ExpiresAt] AND [SupersededAt] IS NULL)),
    CONSTRAINT [CK_LoginPhoneChanges_Phone] CHECK (DATALENGTH([NewPhoneNumber]) BETWEEN 3 AND 16 AND LEFT([NewPhoneNumber],1)='+' AND SUBSTRING([NewPhoneNumber],2,1) BETWEEN '1' AND '9' AND SUBSTRING([NewPhoneNumber],2,15) NOT LIKE '%[^0-9]%'),
    CONSTRAINT [CK_LoginPhoneChanges_Superseded] CHECK ([SupersededAt] IS NULL OR ([SupersededAt] >= [ApprovedAt] AND [CompletedAt] IS NULL)),
    CONSTRAINT [FK_LoginPhoneChanges_LoginPhoneBindings_UserAccountId_PreviousBindingId] FOREIGN KEY ([UserAccountId], [PreviousBindingId]) REFERENCES [auth].[LoginPhoneBindings] ([UserAccountId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LoginPhoneChanges_MobileOtpChallenges_UserAccountId_CompletionChallengeId] FOREIGN KEY ([UserAccountId], [CompletionChallengeId]) REFERENCES [auth].[MobileOtpChallenges] ([UserAccountId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LoginPhoneChanges_UserRoleAssignments_ApprovedByUserId_ApprovedByAssignmentId] FOREIGN KEY ([ApprovedByUserId], [ApprovedByAssignmentId]) REFERENCES [auth].[UserRoleAssignments] ([UserAccountId], [Id]) ON DELETE NO ACTION
);

ALTER TABLE [auth].[MobileOtpChallenges] ADD CONSTRAINT [CK_MobileOtpChallenges_Purpose] CHECK (([Purpose] = 'MobileLogin' AND [Platform] IN ('android','ios')) OR ([Purpose] IN ('WebLogin','WebPasswordReset') AND [Platform] = 'web') OR ([Purpose] = 'PhoneRecovery' AND [Platform] IN ('web','android','ios')));

CREATE UNIQUE INDEX [IX_LoginPhoneBindings_LoginPhoneChangeId] ON [auth].[LoginPhoneBindings] ([LoginPhoneChangeId]) WHERE [LoginPhoneChangeId] IS NOT NULL;

CREATE INDEX [IX_LoginPhoneBindings_UserAccountId_LoginPhoneChangeId] ON [auth].[LoginPhoneBindings] ([UserAccountId], [LoginPhoneChangeId]);

CREATE UNIQUE INDEX [IX_EmployeeSubmissions_Provenance] ON [hr].[EmployeeSubmissions] ([Provenance]) WHERE [Provenance] = 1;

ALTER TABLE [hr].[EmployeeSubmissions] ADD CONSTRAINT [CK_EmployeeSubmissions_Provenance] CHECK (([Provenance] = 0 AND [SubmittedByUserId] IS NOT NULL AND [SubmittedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [ExternalSubmitterReference] IS NULL) OR ([Provenance] = 1 AND [SubmittedByUserId] IS NULL AND [ExternalSubmitterReference] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalSubmitterReference]))) > 0 AND [Version] = 1 AND [MobileAccessRequested] = 0 AND [ApprovedLoginPhoneNumber] IS NOT NULL));

CREATE UNIQUE INDEX [IX_EmployeeOnboardingDecisions_EmployeeId_SubmissionId_Provenance] ON [hr].[EmployeeOnboardingDecisions] ([EmployeeId], [SubmissionId], [Provenance]);

ALTER TABLE [hr].[EmployeeOnboardingDecisions] ADD CONSTRAINT [CK_EmployeeOnboardingDecisions_Provenance] CHECK (([Provenance] = 0 AND [ActorUserId] IS NOT NULL AND [ActorUserId] <> '00000000-0000-0000-0000-000000000000' AND [ActorRoleId] IS NOT NULL AND [ActorRoleId] <> '00000000-0000-0000-0000-000000000000' AND [ExternalApproverReference] IS NULL AND [OwnerApprovalReference] IS NULL) OR ([Provenance] = 1 AND [Decision] = 1 AND [ActorUserId] IS NULL AND [ActorRoleId] IS NULL AND [ExternalApproverReference] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalApproverReference]))) > 0 AND [OwnerApprovalReference] IS NOT NULL AND LEN(LTRIM(RTRIM([OwnerApprovalReference]))) > 0));

CREATE INDEX [IX_LoginPhoneChanges_ApprovedByUserId_ApprovedByAssignmentId] ON [auth].[LoginPhoneChanges] ([ApprovedByUserId], [ApprovedByAssignmentId]);

CREATE UNIQUE INDEX [IX_LoginPhoneChanges_CompletionChallengeId] ON [auth].[LoginPhoneChanges] ([CompletionChallengeId]) WHERE [CompletionChallengeId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_LoginPhoneChanges_PreviousBindingId] ON [auth].[LoginPhoneChanges] ([PreviousBindingId]);

CREATE INDEX [IX_LoginPhoneChanges_UserAccountId_CompletionChallengeId] ON [auth].[LoginPhoneChanges] ([UserAccountId], [CompletionChallengeId]);

CREATE INDEX [IX_LoginPhoneChanges_UserAccountId_PreviousBindingId] ON [auth].[LoginPhoneChanges] ([UserAccountId], [PreviousBindingId]);

ALTER TABLE [hr].[EmployeeOnboardingDecisions] ADD CONSTRAINT [FK_EmployeeOnboardingDecisions_EmployeeSubmissions_EmployeeId_SubmissionId_Provenance] FOREIGN KEY ([EmployeeId], [SubmissionId], [Provenance]) REFERENCES [hr].[EmployeeSubmissions] ([EmployeeId], [Id], [Provenance]) ON DELETE NO ACTION;

ALTER TABLE [auth].[LoginPhoneBindings] ADD CONSTRAINT [FK_LoginPhoneBindings_LoginPhoneChanges_UserAccountId_LoginPhoneChangeId] FOREIGN KEY ([UserAccountId], [LoginPhoneChangeId]) REFERENCES [auth].[LoginPhoneChanges] ([UserAccountId], [Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007090713_AddAccountRecoveryAndOwnerInitialization', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_HolidayCalendars_Code_EffectiveFrom] ON [hr].[HolidayCalendars];

ALTER TABLE [hr].[HolidayCalendars] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [hr].[HolidayCalendars] ADD [Revision] int NOT NULL DEFAULT 1;

ALTER TABLE [hr].[HolidayCalendars] ADD [SupersedesCalendarId] uniqueidentifier NULL;

CREATE TABLE [hr].[OrganizationChanges] (
    [Id] uniqueidentifier NOT NULL,
    [AuditEventId] uniqueidentifier NOT NULL,
    [Resource] nvarchar(40) NOT NULL,
    [ResourceId] uniqueidentifier NOT NULL,
    [Operation] nvarchar(100) NOT NULL,
    [Reason] nvarchar(500) NOT NULL,
    [BeforeJson] nvarchar(max) NULL,
    [AfterJson] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_OrganizationChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_OrganizationChanges_Json] CHECK (ISJSON([AfterJson]) = 1 AND ([BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1)),
    CONSTRAINT [FK_OrganizationChanges_AuditEvents_AuditEventId] FOREIGN KEY ([AuditEventId]) REFERENCES [audit].[AuditEvents] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_HolidayCalendars_Code_EffectiveFrom_Revision] ON [hr].[HolidayCalendars] ([Code], [EffectiveFrom], [Revision]);

CREATE UNIQUE INDEX [IX_HolidayCalendars_SupersedesCalendarId] ON [hr].[HolidayCalendars] ([SupersedesCalendarId]) WHERE [SupersedesCalendarId] IS NOT NULL;

ALTER TABLE [hr].[HolidayCalendars] ADD CONSTRAINT [CK_HolidayCalendars_Predecessor] CHECK (([Revision] = 1 AND [SupersedesCalendarId] IS NULL) OR ([Revision] > 1 AND [SupersedesCalendarId] IS NOT NULL));

ALTER TABLE [hr].[HolidayCalendars] ADD CONSTRAINT [CK_HolidayCalendars_Revision] CHECK ([Revision] >= 1);

CREATE UNIQUE INDEX [IX_OrganizationChanges_AuditEventId] ON [hr].[OrganizationChanges] ([AuditEventId]);

CREATE INDEX [IX_OrganizationChanges_Resource_ResourceId] ON [hr].[OrganizationChanges] ([Resource], [ResourceId]);

ALTER TABLE [hr].[HolidayCalendars] ADD CONSTRAINT [FK_HolidayCalendars_HolidayCalendars_SupersedesCalendarId] FOREIGN KEY ([SupersedesCalendarId]) REFERENCES [hr].[HolidayCalendars] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007122140_AddOrganizationMastersAndHolidayRevisions', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM [hr].[EmployeeDocuments]) THROW 51002, 'Existing employee documents require an approved privacy-classification migration before this upgrade.', 1;

ALTER TABLE [hr].[EmployeeDocuments] ADD [Privacy] int NOT NULL DEFAULT 0;

CREATE TABLE [hr].[EmployeeHiringDrafts] (
    [EmployeeId] uniqueidentifier NOT NULL,
    [DepartmentId] uniqueidentifier NULL,
    [DesignationId] uniqueidentifier NULL,
    [StaffCategoryId] uniqueidentifier NULL,
    [EmploymentTypeId] uniqueidentifier NULL,
    [BranchId] uniqueidentifier NULL,
    [ReportingManagerEmployeeId] uniqueidentifier NULL,
    [ProposedShiftId] uniqueidentifier NULL,
    [ProfileReference] nvarchar(500) NULL,
    [FinancialReference] nvarchar(500) NULL,
    [MedicalReference] nvarchar(500) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeHiringDrafts] PRIMARY KEY ([EmployeeId]),
    CONSTRAINT [CK_EmployeeHiringDrafts_Manager] CHECK ([ReportingManagerEmployeeId] IS NULL OR [ReportingManagerEmployeeId] <> [EmployeeId]),
    CONSTRAINT [FK_EmployeeHiringDrafts_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [hr].[Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_Designations_DesignationId] FOREIGN KEY ([DesignationId]) REFERENCES [hr].[Designations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_Employees_ReportingManagerEmployeeId] FOREIGN KEY ([ReportingManagerEmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_EmploymentTypes_EmploymentTypeId] FOREIGN KEY ([EmploymentTypeId]) REFERENCES [hr].[EmploymentTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_Shifts_ProposedShiftId] FOREIGN KEY ([ProposedShiftId]) REFERENCES [attendance].[Shifts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_StaffCategories_StaffCategoryId] FOREIGN KEY ([StaffCategoryId]) REFERENCES [hr].[StaffCategories] ([Id]) ON DELETE NO ACTION
);

ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_Privacy] CHECK ([Privacy] IN (0, 1));

CREATE INDEX [IX_EmployeeHiringDrafts_BranchId] ON [hr].[EmployeeHiringDrafts] ([BranchId]);

CREATE INDEX [IX_EmployeeHiringDrafts_DepartmentId] ON [hr].[EmployeeHiringDrafts] ([DepartmentId]);

CREATE INDEX [IX_EmployeeHiringDrafts_DesignationId] ON [hr].[EmployeeHiringDrafts] ([DesignationId]);

CREATE INDEX [IX_EmployeeHiringDrafts_EmploymentTypeId] ON [hr].[EmployeeHiringDrafts] ([EmploymentTypeId]);

CREATE INDEX [IX_EmployeeHiringDrafts_ProposedShiftId] ON [hr].[EmployeeHiringDrafts] ([ProposedShiftId]);

CREATE INDEX [IX_EmployeeHiringDrafts_ReportingManagerEmployeeId] ON [hr].[EmployeeHiringDrafts] ([ReportingManagerEmployeeId]);

CREATE INDEX [IX_EmployeeHiringDrafts_StaffCategoryId] ON [hr].[EmployeeHiringDrafts] ([StaffCategoryId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007141249_AddEmployeeHiringDraftsAndDocumentPrivacy', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeAssignments]') AND [c].[name] = N'BranchId');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeAssignments] DROP CONSTRAINT ' + @var3 + ';');
ALTER TABLE [hr].[EmployeeAssignments] ALTER COLUMN [BranchId] uniqueidentifier NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007151945_CompleteEmployeeHiringApproval', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [hr].[EmployeeStatusChanges] ADD [ApprovalRequestId] uniqueidentifier NULL;

CREATE UNIQUE INDEX [IX_EmployeeStatusChanges_ApprovalRequestId] ON [hr].[EmployeeStatusChanges] ([ApprovalRequestId]) WHERE [ApprovalRequestId] IS NOT NULL;

ALTER TABLE [hr].[EmployeeStatusChanges] ADD CONSTRAINT [FK_EmployeeStatusChanges_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007164427_LinkEmployeeStatusApprovals', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [hr].[EmployeeChanges] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Section] int NOT NULL,
    [Revision] int NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [BeforePrivateReference] nvarchar(1000) NOT NULL,
    [AfterPrivateReference] nvarchar(1000) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmployeeChanges_Approval] CHECK ([Section] = 1 OR [ApprovalRequestId] IS NOT NULL),
    CONSTRAINT [CK_EmployeeChanges_Revision] CHECK ([Revision] > 0),
    CONSTRAINT [CK_EmployeeChanges_Section] CHECK ([Section] IN (1, 2, 3, 4, 5, 6)),
    CONSTRAINT [FK_EmployeeChanges_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeChanges_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_EmployeeChanges_ApprovalRequestId] ON [hr].[EmployeeChanges] ([ApprovalRequestId]) WHERE [ApprovalRequestId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_EmployeeChanges_EmployeeId_Section_Revision] ON [hr].[EmployeeChanges] ([EmployeeId], [Section], [Revision]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261008074438_AddEmployeeChangeJournal', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var4 nvarchar(max);
SELECT @var4 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[TemporaryShiftChanges]') AND [c].[name] = N'ApprovalRequestId');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[TemporaryShiftChanges] DROP CONSTRAINT ' + @var4 + ';');
ALTER TABLE [attendance].[TemporaryShiftChanges] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

DECLARE @var5 nvarchar(max);
SELECT @var5 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ShiftPopulationAssignments]') AND [c].[name] = N'ApprovalRequestId');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ShiftPopulationAssignments] DROP CONSTRAINT ' + @var5 + ';');
ALTER TABLE [attendance].[ShiftPopulationAssignments] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [attendance].[ShiftPopulationAssignments] ADD [HolidayCalendarId] uniqueidentifier NULL;

DECLARE @var6 nvarchar(max);
SELECT @var6 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ShiftPopulationAssignmentEnds]') AND [c].[name] = N'ApprovalRequestId');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ShiftPopulationAssignmentEnds] DROP CONSTRAINT ' + @var6 + ';');
ALTER TABLE [attendance].[ShiftPopulationAssignmentEnds] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

CREATE TABLE [attendance].[TemporaryShiftChangeAmendments] (
    [Id] uniqueidentifier NOT NULL,
    [OriginalChangeId] uniqueidentifier NOT NULL,
    [ReplacementChangeId] uniqueidentifier NULL,
    [RecordedBy] nvarchar(200) NOT NULL,
    [RecordedAtUtc] datetimeoffset(7) NOT NULL,
    [WrittenEvidenceReference] nvarchar(500) NOT NULL,
    [Reason] nvarchar(2000) NOT NULL,
    CONSTRAINT [PK_TemporaryShiftChangeAmendments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_TemporaryShiftChangeAmendments_DifferentChanges] CHECK ([ReplacementChangeId] IS NULL OR [OriginalChangeId] <> [ReplacementChangeId]),
    CONSTRAINT [CK_TemporaryShiftChangeAmendments_Evidence] CHECK (LEN(LTRIM(RTRIM([RecordedBy]))) > 0 AND LEN(LTRIM(RTRIM([WrittenEvidenceReference]))) > 0 AND LEN(LTRIM(RTRIM([Reason]))) > 0),
    CONSTRAINT [CK_TemporaryShiftChangeAmendments_RecordedAtUtc_Utc] CHECK ([RecordedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RecordedAtUtc]) = 0),
    CONSTRAINT [FK_TemporaryShiftChangeAmendments_TemporaryShiftChanges_OriginalChangeId] FOREIGN KEY ([OriginalChangeId]) REFERENCES [attendance].[TemporaryShiftChanges] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TemporaryShiftChangeAmendments_TemporaryShiftChanges_ReplacementChangeId] FOREIGN KEY ([ReplacementChangeId]) REFERENCES [attendance].[TemporaryShiftChanges] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ShiftPopulationAssignments_HolidayCalendarId] ON [attendance].[ShiftPopulationAssignments] ([HolidayCalendarId]);

CREATE UNIQUE INDEX [IX_TemporaryShiftChangeAmendments_OriginalChangeId] ON [attendance].[TemporaryShiftChangeAmendments] ([OriginalChangeId]);

CREATE UNIQUE INDEX [IX_TemporaryShiftChangeAmendments_ReplacementChangeId] ON [attendance].[TemporaryShiftChangeAmendments] ([ReplacementChangeId]) WHERE [ReplacementChangeId] IS NOT NULL;

ALTER TABLE [attendance].[ShiftPopulationAssignments] ADD CONSTRAINT [FK_ShiftPopulationAssignments_HolidayCalendars_HolidayCalendarId] FOREIGN KEY ([HolidayCalendarId]) REFERENCES [hr].[HolidayCalendars] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [attendance].[FaceVerificationAttempts] DROP CONSTRAINT [CK_FaceVerificationAttempts_Completion];

ALTER TABLE [attendance].[FaceVerificationAttempts] DROP CONSTRAINT [CK_FaceVerificationAttempts_Outcome];

DECLARE @var7 nvarchar(max);
SELECT @var7 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[WorkSessions]') AND [c].[name] = N'PolicyVersionId');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[WorkSessions] DROP CONSTRAINT ' + @var7 + ';');
ALTER TABLE [attendance].[WorkSessions] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [BusinessDate] date NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [HolidayCalendarId] uniqueidentifier NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [SiteRosterAssignmentId] uniqueidentifier NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [TemporaryShiftChangeId] uniqueidentifier NULL;

DECLARE @var8 nvarchar(max);
SELECT @var8 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[PunchAssessments]') AND [c].[name] = N'PolicyVersionId');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[PunchAssessments] DROP CONSTRAINT ' + @var8 + ';');
ALTER TABLE [attendance].[PunchAssessments] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;

DECLARE @var9 nvarchar(max);
SELECT @var9 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'ShiftVersionId');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var9 + ';');
ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [ShiftVersionId] uniqueidentifier NULL;

DECLARE @var10 nvarchar(max);
SELECT @var10 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'PolicyVersionId');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var10 + ';');
ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;

DECLARE @var11 nvarchar(max);
SELECT @var11 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'PayableMinutes');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var11 + ';');
ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [PayableMinutes] int NULL;

DECLARE @var12 nvarchar(max);
SELECT @var12 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'OvertimeMinutes');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var12 + ';');
ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [OvertimeMinutes] int NULL;

DECLARE @var13 nvarchar(max);
SELECT @var13 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'LateMinutes');
IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var13 + ';');
ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [LateMinutes] int NULL;

ALTER TABLE [attendance].[DailyAttendanceSummaries] ADD [InputEvidenceReference] nvarchar(500) NULL;

CREATE INDEX [IX_RawPunchEvents_HolidayCalendarId] ON [attendance].[RawPunchEvents] ([HolidayCalendarId]);

CREATE INDEX [IX_RawPunchEvents_SiteRosterAssignmentId] ON [attendance].[RawPunchEvents] ([SiteRosterAssignmentId]);

CREATE INDEX [IX_RawPunchEvents_TemporaryShiftChangeId] ON [attendance].[RawPunchEvents] ([TemporaryShiftChangeId]);

ALTER TABLE [attendance].[FaceVerificationAttempts] ADD CONSTRAINT [CK_FaceVerificationAttempts_Completion] CHECK (([Outcome] = 1 AND [CompletedAtUtc] IS NULL AND [LivenessPassed] IS NULL AND [MatchPassed] IS NULL AND [ResultReasonCode] IS NULL) OR ([Outcome] BETWEEN 2 AND 4 AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [IssuedAtUtc] AND [CompletedAtUtc] < [ExpiresAtUtc] AND [LivenessPassed] IS NOT NULL AND [MatchPassed] IS NOT NULL AND [ResultReasonCode] IS NOT NULL) OR ([Outcome] = 5 AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [ExpiresAtUtc] AND [LivenessPassed] IS NOT NULL AND [MatchPassed] IS NOT NULL AND [LivenessPassed] = 0 AND [MatchPassed] = 0 AND [ResultReasonCode] IS NOT NULL));

ALTER TABLE [attendance].[FaceVerificationAttempts] ADD CONSTRAINT [CK_FaceVerificationAttempts_Outcome] CHECK ([Outcome] BETWEEN 1 AND 5);

ALTER TABLE [attendance].[DailyAttendanceSummaries] ADD CONSTRAINT [CK_DailyAttendanceSummaries_PolicyOutputs] CHECK (([PolicyVersionId] IS NULL AND [PayableMinutes] IS NULL AND [LateMinutes] IS NULL AND [OvertimeMinutes] IS NULL AND [Status] IN (1,9)) OR ([PolicyVersionId] IS NOT NULL AND [PayableMinutes] IS NOT NULL AND [LateMinutes] IS NOT NULL AND [OvertimeMinutes] IS NOT NULL));

ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_HolidayCalendars_HolidayCalendarId] FOREIGN KEY ([HolidayCalendarId]) REFERENCES [hr].[HolidayCalendars] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_ShiftPopulationAssignments_SiteRosterAssignmentId] FOREIGN KEY ([SiteRosterAssignmentId]) REFERENCES [attendance].[ShiftPopulationAssignments] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_TemporaryShiftChanges_TemporaryShiftChangeId] FOREIGN KEY ([TemporaryShiftChangeId]) REFERENCES [attendance].[TemporaryShiftChanges] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261009083807_AddAttendanceCaptureAndFactualSummaries', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var14 nvarchar(max);
SELECT @var14 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[AttendancePeriodLockEvents]') AND [c].[name] = N'ApprovalRequestId');
IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[AttendancePeriodLockEvents] DROP CONSTRAINT ' + @var14 + ';');
ALTER TABLE [attendance].[AttendancePeriodLockEvents] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [attendance].[AttendancePeriodLockEvents] ADD [ProtectedEvidenceReference] nvarchar(500) NULL;

CREATE TABLE [attendance].[AttendanceCarryForwardAdjustments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [SourceBusinessDate] date NOT NULL,
    [SourcePeriodLockEventId] uniqueidentifier NOT NULL,
    [SourceSummaryId] uniqueidentifier NOT NULL,
    [CorrectionId] uniqueidentifier NOT NULL,
    [ApprovalDecisionId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [SupersedesAdjustmentId] uniqueidentifier NULL,
    [TargetMonthStart] date NOT NULL,
    [TargetMonthEndExclusive] date NOT NULL,
    [BeforeWorkedTicks] bigint NOT NULL,
    [AfterWorkedTicks] bigint NOT NULL,
    [DeltaWorkedTicks] bigint NOT NULL,
    [FirstStartsAtUtc] datetimeoffset(7) NOT NULL,
    [LastEndsAtUtc] datetimeoffset(7) NOT NULL,
    [InputFingerprint] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [InterpretationFingerprint] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ProtectedEvidenceReference] nvarchar(500) NOT NULL,
    [AppliedAtUtc] datetimeoffset(7) NOT NULL,
    CONSTRAINT [PK_AttendanceCarryForwardAdjustments] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_Id] UNIQUE ([EmployeeId], [SourceBusinessDate], [Id]),
    CONSTRAINT [CK_AttendanceCarryForwardAdjustments_AppliedAtUtc_Utc] CHECK ([AppliedAtUtc] IS NULL OR DATEPART(TZOFFSET, [AppliedAtUtc]) = 0),
    CONSTRAINT [CK_AttendanceCarryForwardAdjustments_Fingerprints] CHECK (LEN([InputFingerprint]) = 64 AND [InputFingerprint] NOT LIKE '%[^0-9A-F]%' AND LEN([InterpretationFingerprint]) = 64 AND [InterpretationFingerprint] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_AttendanceCarryForwardAdjustments_FirstStartsAtUtc_Utc] CHECK ([FirstStartsAtUtc] IS NULL OR DATEPART(TZOFFSET, [FirstStartsAtUtc]) = 0),
    CONSTRAINT [CK_AttendanceCarryForwardAdjustments_LastEndsAtUtc_Utc] CHECK ([LastEndsAtUtc] IS NULL OR DATEPART(TZOFFSET, [LastEndsAtUtc]) = 0),
    CONSTRAINT [CK_AttendanceCarryForwardAdjustments_Month] CHECK (DAY([TargetMonthStart]) = 1 AND [TargetMonthEndExclusive] = DATEADD(month, 1, [TargetMonthStart]) AND [TargetMonthStart] > DATEFROMPARTS(YEAR([SourceBusinessDate]), MONTH([SourceBusinessDate]), 1)),
    CONSTRAINT [CK_AttendanceCarryForwardAdjustments_Revision] CHECK (([Revision] = 1 AND [SupersedesAdjustmentId] IS NULL) OR ([Revision] > 1 AND [SupersedesAdjustmentId] IS NOT NULL)),
    CONSTRAINT [CK_AttendanceCarryForwardAdjustments_Ticks] CHECK ([BeforeWorkedTicks] >= 0 AND [AfterWorkedTicks] >= 0 AND [DeltaWorkedTicks] = [AfterWorkedTicks] - [BeforeWorkedTicks]),
    CONSTRAINT [CK_AttendanceCarryForwardAdjustments_Window] CHECK ([LastEndsAtUtc] > [FirstStartsAtUtc]),
    CONSTRAINT [FK_AttendanceCarryForwardAdjustments_ApprovalDecisions_ApprovalDecisionId] FOREIGN KEY ([ApprovalDecisionId]) REFERENCES [workflow].[ApprovalDecisions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceCarryForwardAdjustments_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_SupersedesAdjustmentId] FOREIGN KEY ([EmployeeId], [SourceBusinessDate], [SupersedesAdjustmentId]) REFERENCES [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [SourceBusinessDate], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceCarryForwardAdjustments_AttendanceCorrections_EmployeeId_CorrectionId] FOREIGN KEY ([EmployeeId], [CorrectionId]) REFERENCES [attendance].[AttendanceCorrections] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceCarryForwardAdjustments_AttendancePeriodLockEvents_SourcePeriodLockEventId] FOREIGN KEY ([SourcePeriodLockEventId]) REFERENCES [attendance].[AttendancePeriodLockEvents] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceCarryForwardAdjustments_DailyAttendanceSummaries_EmployeeId_SourceBusinessDate_SourceSummaryId] FOREIGN KEY ([EmployeeId], [SourceBusinessDate], [SourceSummaryId]) REFERENCES [attendance].[DailyAttendanceSummaries] ([EmployeeId], [AttendanceDate], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceCarryForwardAdjustments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

EXEC(N'ALTER TABLE [attendance].[AttendancePeriodLockEvents] ADD CONSTRAINT [CK_AttendancePeriodLockEvents_Provenance] CHECK (([ApprovalRequestId] IS NOT NULL AND [ProtectedEvidenceReference] IS NULL) OR ([ApprovalRequestId] IS NULL AND [ProtectedEvidenceReference] IS NOT NULL AND [IsLocked] = 1));');

CREATE UNIQUE INDEX [IX_AttendanceCarryForwardAdjustments_ApprovalDecisionId] ON [attendance].[AttendanceCarryForwardAdjustments] ([ApprovalDecisionId]);

CREATE UNIQUE INDEX [IX_AttendanceCarryForwardAdjustments_CorrectionId] ON [attendance].[AttendanceCarryForwardAdjustments] ([CorrectionId]);

CREATE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_CorrectionId] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [CorrectionId]);

CREATE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_FirstStartsAtUtc_LastEndsAtUtc] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [FirstStartsAtUtc], [LastEndsAtUtc]);

CREATE UNIQUE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_Revision] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [SourceBusinessDate], [Revision]);

CREATE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_SourceSummaryId] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [SourceBusinessDate], [SourceSummaryId]);

CREATE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_SupersedesAdjustmentId] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [SourceBusinessDate], [SupersedesAdjustmentId]);

CREATE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_TargetMonthStart] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [TargetMonthStart]);

CREATE INDEX [IX_AttendanceCarryForwardAdjustments_SourcePeriodLockEventId] ON [attendance].[AttendanceCarryForwardAdjustments] ([SourcePeriodLockEventId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var15 nvarchar(max);
SELECT @var15 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneVersions]') AND [c].[name] = N'ApprovalRequestId');
IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneVersions] DROP CONSTRAINT ' + @var15 + ';');
ALTER TABLE [attendance].[ZoneVersions] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [attendance].[ZoneVersions] ADD [DirectAuthorityEvidenceReference] nvarchar(500) NULL;

DECLARE @var16 nvarchar(max);
SELECT @var16 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneEvents]') AND [c].[name] = N'Longitude');
IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneEvents] DROP CONSTRAINT ' + @var16 + ';');
ALTER TABLE [attendance].[ZoneEvents] ALTER COLUMN [Longitude] decimal(10,7) NULL;

DECLARE @var17 nvarchar(max);
SELECT @var17 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneEvents]') AND [c].[name] = N'Latitude');
IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneEvents] DROP CONSTRAINT ' + @var17 + ';');
ALTER TABLE [attendance].[ZoneEvents] ALTER COLUMN [Latitude] decimal(10,7) NULL;

DECLARE @var18 nvarchar(max);
SELECT @var18 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneEvents]') AND [c].[name] = N'AccuracyMeters');
IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneEvents] DROP CONSTRAINT ' + @var18 + ';');
ALTER TABLE [attendance].[ZoneEvents] ALTER COLUMN [AccuracyMeters] decimal(10,2) NULL;

ALTER TABLE [attendance].[ZoneEvents] ADD [LocationExpiresAtUtc] datetimeoffset(7) NULL;

ALTER TABLE [attendance].[ZoneEvents] ADD [LocationPrivateReference] nvarchar(500) NULL;

DECLARE @var19 nvarchar(max);
SELECT @var19 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneAssignments]') AND [c].[name] = N'ApprovalRequestId');
IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneAssignments] DROP CONSTRAINT ' + @var19 + ';');
ALTER TABLE [attendance].[ZoneAssignments] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [attendance].[ZoneAssignments] ADD [DirectAuthorityEvidenceReference] nvarchar(500) NULL;

DECLARE @var20 nvarchar(max);
SELECT @var20 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[OvertimeRequests]') AND [c].[name] = N'RateRuleCode');
IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[OvertimeRequests] DROP CONSTRAINT ' + @var20 + ';');
ALTER TABLE [attendance].[OvertimeRequests] ALTER COLUMN [RateRuleCode] nvarchar(100) NULL;

DECLARE @var21 nvarchar(max);
SELECT @var21 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[OvertimeRequests]') AND [c].[name] = N'PolicyVersionId');
IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[OvertimeRequests] DROP CONSTRAINT ' + @var21 + ';');
ALTER TABLE [attendance].[OvertimeRequests] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;

DECLARE @var22 nvarchar(max);
SELECT @var22 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DutyMonitoringWindows]') AND [c].[name] = N'RetentionPolicyVersionId');
IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DutyMonitoringWindows] DROP CONSTRAINT ' + @var22 + ';');
ALTER TABLE [attendance].[DutyMonitoringWindows] ALTER COLUMN [RetentionPolicyVersionId] uniqueidentifier NULL;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [AcceptedCheckInId] uniqueidentifier NULL;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [ConfigurationVersionId] uniqueidentifier NULL;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [RegisteredDeviceId] uniqueidentifier NULL;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [WorkSessionKey] uniqueidentifier NULL;

CREATE TABLE [attendance].[OvertimeEligibilityVersions] (
    [Id] uniqueidentifier NOT NULL,
    [Target] int NOT NULL,
    [EmployeeId] uniqueidentifier NULL,
    [Revision] int NOT NULL,
    [SupersedesVersionId] uniqueidentifier NULL,
    [EffectiveFrom] date NOT NULL,
    [IsEnabled] bit NOT NULL,
    [RecordedBy] nvarchar(200) NOT NULL,
    [RecordedAtUtc] datetimeoffset(7) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [ProtectedEvidenceReference] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_OvertimeEligibilityVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_OvertimeEligibilityVersions_RecordedAtUtc_Utc] CHECK ([RecordedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RecordedAtUtc]) = 0),
    CONSTRAINT [CK_OvertimeEligibilityVersions_Revision] CHECK (([Revision] = 1 AND [SupersedesVersionId] IS NULL) OR ([Revision] > 1 AND [SupersedesVersionId] IS NOT NULL)),
    CONSTRAINT [CK_OvertimeEligibilityVersions_Target] CHECK (([Target] = 1 AND [EmployeeId] IS NULL) OR ([Target] = 2 AND [EmployeeId] IS NOT NULL)),
    CONSTRAINT [FK_OvertimeEligibilityVersions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OvertimeEligibilityVersions_OvertimeEligibilityVersions_SupersedesVersionId] FOREIGN KEY ([SupersedesVersionId]) REFERENCES [attendance].[OvertimeEligibilityVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ZoneAssignmentEnds] (
    [Id] uniqueidentifier NOT NULL,
    [ZoneAssignmentId] uniqueidentifier NOT NULL,
    [EffectiveToUtc] datetimeoffset(7) NOT NULL,
    [Actor] nvarchar(200) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [RecordedAtUtc] datetimeoffset(7) NOT NULL,
    [AuthorityEvidenceReference] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_ZoneAssignmentEnds] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ZoneAssignmentEnds_EffectiveToUtc_Utc] CHECK ([EffectiveToUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveToUtc]) = 0),
    CONSTRAINT [CK_ZoneAssignmentEnds_Future] CHECK ([EffectiveToUtc] >= [RecordedAtUtc]),
    CONSTRAINT [CK_ZoneAssignmentEnds_RecordedAtUtc_Utc] CHECK ([RecordedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RecordedAtUtc]) = 0),
    CONSTRAINT [FK_ZoneAssignmentEnds_ZoneAssignments_ZoneAssignmentId] FOREIGN KEY ([ZoneAssignmentId]) REFERENCES [attendance].[ZoneAssignments] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ZoneLocationDeletions] (
    [Id] uniqueidentifier NOT NULL,
    [ZoneEventId] uniqueidentifier NOT NULL,
    [ExpiresAtUtc] datetimeoffset(7) NOT NULL,
    [DeletedAtUtc] datetimeoffset(7) NOT NULL,
    [ReferenceSha256] nvarchar(64) NOT NULL,
    CONSTRAINT [PK_ZoneLocationDeletions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ZoneLocationDeletions_DeletedAtUtc_Utc] CHECK ([DeletedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeletedAtUtc]) = 0),
    CONSTRAINT [CK_ZoneLocationDeletions_ExpiresAtUtc_Utc] CHECK ([ExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [ExpiresAtUtc]) = 0),
    CONSTRAINT [CK_ZoneLocationDeletions_Expiry] CHECK ([DeletedAtUtc] >= [ExpiresAtUtc]),
    CONSTRAINT [FK_ZoneLocationDeletions_ZoneEvents_ZoneEventId] FOREIGN KEY ([ZoneEventId]) REFERENCES [attendance].[ZoneEvents] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ZoneMonitoringCheckpoints] (
    [Id] uniqueidentifier NOT NULL,
    [DutyMonitoringWindowId] uniqueidentifier NOT NULL,
    [LastReceivedAtUtc] datetimeoffset(7) NULL,
    [LastPosition] int NULL,
    [LocationId] uniqueidentifier NULL,
    [LocationPrivateReference] nvarchar(500) NULL,
    [LocationExpiresAtUtc] datetimeoffset(7) NULL,
    [LiveVisibleUntilUtc] datetimeoffset(7) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ZoneMonitoringCheckpoints] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ZoneMonitoringCheckpoints_LastReceivedAtUtc_Utc] CHECK ([LastReceivedAtUtc] IS NULL OR DATEPART(TZOFFSET, [LastReceivedAtUtc]) = 0),
    CONSTRAINT [CK_ZoneMonitoringCheckpoints_LiveVisibleUntilUtc_Utc] CHECK ([LiveVisibleUntilUtc] IS NULL OR DATEPART(TZOFFSET, [LiveVisibleUntilUtc]) = 0),
    CONSTRAINT [CK_ZoneMonitoringCheckpoints_LocationExpiresAtUtc_Utc] CHECK ([LocationExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [LocationExpiresAtUtc]) = 0),
    CONSTRAINT [CK_ZoneMonitoringCheckpoints_Position] CHECK (([LastPosition] IS NULL AND [LastReceivedAtUtc] IS NULL AND [LocationId] IS NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [LiveVisibleUntilUtc] IS NULL) OR ([LastPosition] IS NOT NULL AND [LastPosition] BETWEEN 1 AND 3 AND [LastReceivedAtUtc] IS NOT NULL AND [LocationId] IS NOT NULL AND [LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LiveVisibleUntilUtc] IS NOT NULL AND [LiveVisibleUntilUtc] > [LastReceivedAtUtc] AND [LocationExpiresAtUtc] >= [LiveVisibleUntilUtc])),
    CONSTRAINT [FK_ZoneMonitoringCheckpoints_DutyMonitoringWindows_DutyMonitoringWindowId] FOREIGN KEY ([DutyMonitoringWindowId]) REFERENCES [attendance].[DutyMonitoringWindows] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[ZoneMonitoringConfigurationVersions] (
    [Id] uniqueidentifier NOT NULL,
    [SiteId] uniqueidentifier NOT NULL,
    [VersionNumber] int NOT NULL,
    [EffectiveFromUtc] datetimeoffset(7) NOT NULL,
    [EffectiveToUtc] datetimeoffset(7) NOT NULL,
    [MaximumAccuracyMeters] decimal(10,2) NOT NULL,
    [WarningDistanceMeters] decimal(10,2) NOT NULL,
    [MinimumIntervalSeconds] int NOT NULL,
    [RetentionDays] int NOT NULL,
    [MaximumMonitoringSeconds] int NOT NULL,
    [LiveVisibilitySeconds] int NOT NULL,
    [VisibleNotice] nvarchar(2000) NOT NULL,
    [ApprovedBy] nvarchar(200) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [ApprovedAtUtc] datetimeoffset(7) NOT NULL,
    [AuthorityEvidenceReference] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_ZoneMonitoringConfigurationVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ZoneMonitoringConfigurationVersions_ApprovedAtUtc_Utc] CHECK ([ApprovedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ApprovedAtUtc]) = 0),
    CONSTRAINT [CK_ZoneMonitoringConfigurationVersions_EffectiveFromUtc_Utc] CHECK ([EffectiveFromUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveFromUtc]) = 0),
    CONSTRAINT [CK_ZoneMonitoringConfigurationVersions_EffectiveToUtc_Utc] CHECK ([EffectiveToUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveToUtc]) = 0),
    CONSTRAINT [CK_ZoneMonitoringConfigurationVersions_Values] CHECK ([VersionNumber] > 0 AND [MaximumAccuracyMeters] > 0 AND [MaximumAccuracyMeters] <= 100000 AND [WarningDistanceMeters] BETWEEN 0 AND 100000 AND [MinimumIntervalSeconds] BETWEEN 1 AND 86400 AND [RetentionDays] BETWEEN 1 AND 3650 AND [MaximumMonitoringSeconds] BETWEEN 1 AND 172800 AND [MinimumIntervalSeconds] <= [MaximumMonitoringSeconds] AND [LiveVisibilitySeconds] BETWEEN 1 AND 86400 AND [LiveVisibilitySeconds] >= [MinimumIntervalSeconds] AND [LiveVisibilitySeconds] <= [MaximumMonitoringSeconds]),
    CONSTRAINT [CK_ZoneMonitoringConfigurationVersions_Window] CHECK ([EffectiveToUtc] > [EffectiveFromUtc]),
    CONSTRAINT [FK_ZoneMonitoringConfigurationVersions_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION
);

EXEC(N'ALTER TABLE [attendance].[ZoneVersions] ADD CONSTRAINT [CK_ZoneVersions_Authority] CHECK ([ApprovalRequestId] IS NOT NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL);');

CREATE INDEX [IX_ZoneEvents_LocationExpiresAtUtc_Id] ON [attendance].[ZoneEvents] ([LocationExpiresAtUtc], [Id]);

EXEC(N'ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_LocationExpiresAtUtc_Utc] CHECK ([LocationExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [LocationExpiresAtUtc]) = 0);');

EXEC(N'ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_LocationRetention] CHECK (([LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [AccuracyMeters] IS NOT NULL) OR ([LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LocationExpiresAtUtc] > [ReceivedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL));');

EXEC(N'ALTER TABLE [attendance].[ZoneAssignments] ADD CONSTRAINT [CK_ZoneAssignments_Authority] CHECK ([ApprovalRequestId] IS NOT NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL);');

CREATE INDEX [IX_DutyMonitoringWindows_AcceptedCheckInId] ON [attendance].[DutyMonitoringWindows] ([AcceptedCheckInId]);

CREATE INDEX [IX_DutyMonitoringWindows_ConfigurationVersionId] ON [attendance].[DutyMonitoringWindows] ([ConfigurationVersionId]);

CREATE INDEX [IX_DutyMonitoringWindows_RegisteredDeviceId] ON [attendance].[DutyMonitoringWindows] ([RegisteredDeviceId]);

EXEC(N'ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [CK_DutyMonitoringWindows_Provenance] CHECK (([RetentionPolicyVersionId] IS NOT NULL AND [ConfigurationVersionId] IS NULL AND [AcceptedCheckInId] IS NULL AND [WorkSessionKey] IS NULL AND [RegisteredDeviceId] IS NULL) OR ([RetentionPolicyVersionId] IS NULL AND [ConfigurationVersionId] IS NOT NULL AND [AcceptedCheckInId] IS NOT NULL AND [WorkSessionKey] IS NOT NULL AND [RegisteredDeviceId] IS NOT NULL));');

CREATE INDEX [IX_OvertimeEligibilityVersions_EmployeeId] ON [attendance].[OvertimeEligibilityVersions] ([EmployeeId]);

CREATE INDEX [IX_OvertimeEligibilityVersions_SupersedesVersionId] ON [attendance].[OvertimeEligibilityVersions] ([SupersedesVersionId]);

CREATE INDEX [IX_OvertimeEligibilityVersions_Target_EmployeeId_EffectiveFrom_Revision] ON [attendance].[OvertimeEligibilityVersions] ([Target], [EmployeeId], [EffectiveFrom], [Revision]);

CREATE UNIQUE INDEX [IX_OvertimeEligibilityVersions_Target_EmployeeId_Revision] ON [attendance].[OvertimeEligibilityVersions] ([Target], [EmployeeId], [Revision]);

CREATE UNIQUE INDEX [IX_ZoneAssignmentEnds_ZoneAssignmentId] ON [attendance].[ZoneAssignmentEnds] ([ZoneAssignmentId]);

CREATE UNIQUE INDEX [IX_ZoneLocationDeletions_ZoneEventId] ON [attendance].[ZoneLocationDeletions] ([ZoneEventId]);

CREATE UNIQUE INDEX [IX_ZoneMonitoringCheckpoints_DutyMonitoringWindowId] ON [attendance].[ZoneMonitoringCheckpoints] ([DutyMonitoringWindowId]);

CREATE INDEX [IX_ZoneMonitoringConfigurationVersions_SiteId_EffectiveFromUtc] ON [attendance].[ZoneMonitoringConfigurationVersions] ([SiteId], [EffectiveFromUtc]);

CREATE UNIQUE INDEX [IX_ZoneMonitoringConfigurationVersions_SiteId_VersionNumber] ON [attendance].[ZoneMonitoringConfigurationVersions] ([SiteId], [VersionNumber]);

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [FK_DutyMonitoringWindows_RawPunchEvents_AcceptedCheckInId] FOREIGN KEY ([AcceptedCheckInId]) REFERENCES [attendance].[RawPunchEvents] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [FK_DutyMonitoringWindows_RegisteredDevices_RegisteredDeviceId] FOREIGN KEY ([RegisteredDeviceId]) REFERENCES [auth].[RegisteredDevices] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [FK_DutyMonitoringWindows_ZoneMonitoringConfigurationVersions_ConfigurationVersionId] FOREIGN KEY ([ConfigurationVersionId]) REFERENCES [attendance].[ZoneMonitoringConfigurationVersions] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261012081503_AddOvertimeEligibilityAndDutyZones', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [attendance].[ZoneMonitoringCheckpoints] ADD [LastWarning] bit NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261012082833_RetainZoneWarningTransitions', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [attendance].[ShiftVersions] ADD [MissingCheckoutReminderAfterMinutes] int NULL;

ALTER TABLE [attendance].[SiteReports] ADD CONSTRAINT [AK_SiteReports_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]);

ALTER TABLE [attendance].[SiteReportReviews] ADD CONSTRAINT [AK_SiteReportReviews_SiteReportId_Id] UNIQUE ([SiteReportId], [Id]);

CREATE TABLE [attendance].[SiteReportResponses] (
    [Id] uniqueidentifier NOT NULL,
    [SiteReportId] uniqueidentifier NOT NULL,
    [Sequence] int NOT NULL,
    [ReturnedReviewId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [SubmittedByAccountId] uniqueidentifier NOT NULL,
    [SubmittedAtUtc] datetimeoffset(7) NOT NULL,
    [PrivateEvidenceReference] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_SiteReportResponses] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_SiteReportResponses_Sequence] CHECK ([Sequence] > 0),
    CONSTRAINT [CK_SiteReportResponses_SubmittedAtUtc_Utc] CHECK ([SubmittedAtUtc] IS NULL OR DATEPART(TZOFFSET, [SubmittedAtUtc]) = 0),
    CONSTRAINT [FK_SiteReportResponses_SiteReportReviews_SiteReportId_ReturnedReviewId] FOREIGN KEY ([SiteReportId], [ReturnedReviewId]) REFERENCES [attendance].[SiteReportReviews] ([SiteReportId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SiteReportResponses_SiteReports_EmployeeId_SiteReportId] FOREIGN KEY ([EmployeeId], [SiteReportId]) REFERENCES [attendance].[SiteReports] ([EmployeeId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SiteReportResponses_UserAccounts_EmployeeId_SubmittedByAccountId] FOREIGN KEY ([EmployeeId], [SubmittedByAccountId]) REFERENCES [auth].[UserAccounts] ([EmployeeId], [Id]) ON DELETE NO ACTION
);

EXEC(N'ALTER TABLE [attendance].[ShiftVersions] ADD CONSTRAINT [CK_ShiftVersions_MissingCheckoutReminderAfterMinutes] CHECK ([MissingCheckoutReminderAfterMinutes] IS NULL OR [MissingCheckoutReminderAfterMinutes] > 0)');

CREATE INDEX [IX_SiteReportResponses_EmployeeId_SiteReportId] ON [attendance].[SiteReportResponses] ([EmployeeId], [SiteReportId]);

CREATE INDEX [IX_SiteReportResponses_EmployeeId_SubmittedByAccountId] ON [attendance].[SiteReportResponses] ([EmployeeId], [SubmittedByAccountId]);

CREATE UNIQUE INDEX [IX_SiteReportResponses_ReturnedReviewId] ON [attendance].[SiteReportResponses] ([ReturnedReviewId]);

CREATE INDEX [IX_SiteReportResponses_SiteReportId_ReturnedReviewId] ON [attendance].[SiteReportResponses] ([SiteReportId], [ReturnedReviewId]);

CREATE UNIQUE INDEX [IX_SiteReportResponses_SiteReportId_Sequence] ON [attendance].[SiteReportResponses] ([SiteReportId], [Sequence]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261012114635_AddSiteReportResponsesAndShiftReminders', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var23 nvarchar(max);
SELECT @var23 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[PolicyVersions]') AND [c].[name] = N'ApprovalRequestId');
IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [hr].[PolicyVersions] DROP CONSTRAINT ' + @var23 + ';');
ALTER TABLE [hr].[PolicyVersions] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [hr].[PolicyVersions] ADD [Authority] int NOT NULL DEFAULT 1;

DECLARE @var24 nvarchar(max);
SELECT @var24 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[PolicyAssignments]') AND [c].[name] = N'ApprovalRequestId');
IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [hr].[PolicyAssignments] DROP CONSTRAINT ' + @var24 + ';');
ALTER TABLE [hr].[PolicyAssignments] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [hr].[PolicyAssignments] ADD [Authority] int NOT NULL DEFAULT 1;

DECLARE @var25 nvarchar(max);
SELECT @var25 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[PolicyAssignmentEnds]') AND [c].[name] = N'ApprovalRequestId');
IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [hr].[PolicyAssignmentEnds] DROP CONSTRAINT ' + @var25 + ';');
ALTER TABLE [hr].[PolicyAssignmentEnds] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [hr].[PolicyAssignmentEnds] ADD [Authority] int NOT NULL DEFAULT 1;

ALTER TABLE [hr].[Policies] ADD [Revision] int NOT NULL DEFAULT 0;

ALTER TABLE [hr].[LeaveTypes] ADD [Kind] int NOT NULL DEFAULT 0;

DECLARE @var26 nvarchar(max);
SELECT @var26 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[LeaveRequests]') AND [c].[name] = N'PolicyVersionId');
IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [hr].[LeaveRequests] DROP CONSTRAINT ' + @var26 + ';');
ALTER TABLE [hr].[LeaveRequests] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;

ALTER TABLE [hr].[LeaveRequests] ADD [PreviousRequestId] uniqueidentifier NULL;

ALTER TABLE [hr].[LeaveRequests] ADD [SubmissionKey] nvarchar(100) NULL;

CREATE TABLE [hr].[LeaveRequestDays] (
    [Id] uniqueidentifier NOT NULL,
    [LeaveRequestId] uniqueidentifier NOT NULL,
    [Date] date NOT NULL,
    [StartsAtUtc] datetimeoffset NOT NULL,
    [EndsAtUtc] datetimeoffset NOT NULL,
    [Minutes] int NOT NULL,
    [ShiftVersionId] uniqueidentifier NULL,
    CONSTRAINT [PK_LeaveRequestDays] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_LeaveRequestDays_Interval] CHECK ([EndsAtUtc] > [StartsAtUtc] AND [Minutes] > 0 AND [Minutes] <= 1440),
    CONSTRAINT [FK_LeaveRequestDays_LeaveRequests_LeaveRequestId] FOREIGN KEY ([LeaveRequestId]) REFERENCES [hr].[LeaveRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LeaveRequestDays_ShiftVersions_ShiftVersionId] FOREIGN KEY ([ShiftVersionId]) REFERENCES [attendance].[ShiftVersions] ([Id]) ON DELETE NO ACTION
);

EXEC(N'ALTER TABLE [hr].[PolicyVersions] ADD CONSTRAINT [CK_PolicyVersion_Authority] CHECK (([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL))');
EXEC(N'ALTER TABLE [hr].[PolicyAssignments] ADD CONSTRAINT [CK_PolicyAssignment_Authority] CHECK (([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL))');
EXEC(N'ALTER TABLE [hr].[PolicyAssignmentEnds] ADD CONSTRAINT [CK_PolicyAssignmentEnd_Authority] CHECK (([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL))');

EXEC(N'CREATE UNIQUE INDEX [IX_LeaveRequests_EmployeeId_SubmissionKey] ON [hr].[LeaveRequests] ([EmployeeId], [SubmissionKey]) WHERE [SubmissionKey] IS NOT NULL');
EXEC(N'CREATE UNIQUE INDEX [IX_LeaveRequests_PreviousRequestId] ON [hr].[LeaveRequests] ([PreviousRequestId]) WHERE [PreviousRequestId] IS NOT NULL');

CREATE INDEX [IX_LeaveRequestDays_Date_LeaveRequestId] ON [hr].[LeaveRequestDays] ([Date], [LeaveRequestId]);

CREATE UNIQUE INDEX [IX_LeaveRequestDays_LeaveRequestId_Date] ON [hr].[LeaveRequestDays] ([LeaveRequestId], [Date]);

CREATE INDEX [IX_LeaveRequestDays_ShiftVersionId] ON [hr].[LeaveRequestDays] ([ShiftVersionId]);

ALTER TABLE [hr].[LeaveRequests] ADD CONSTRAINT [FK_LeaveRequests_LeaveRequests_PreviousRequestId] FOREIGN KEY ([PreviousRequestId]) REFERENCES [hr].[LeaveRequests] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261012125614_AddDirectPoliciesAndAdminLeave', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [attendance].[AttendancePayrollInputRevisions] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [PeriodStart] date NOT NULL,
    [Revision] int NOT NULL,
    [SupersedesRevisionId] uniqueidentifier NULL,
    [CalculationVersion] nvarchar(100) NOT NULL,
    [InputFingerprint] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ProtectedEvidenceReference] nvarchar(500) NOT NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [CreatedAtUtc] datetimeoffset(7) NOT NULL,
    CONSTRAINT [PK_AttendancePayrollInputRevisions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_Id] UNIQUE ([EmployeeId], [PeriodStart], [Id]),
    CONSTRAINT [CK_AttendancePayrollInputRevisions_CreatedAtUtc_Utc] CHECK ([CreatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CreatedAtUtc]) = 0),
    CONSTRAINT [CK_AttendancePayrollInputRevisions_Fingerprint] CHECK (LEN([InputFingerprint]) = 64 AND [InputFingerprint] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_AttendancePayrollInputRevisions_Period] CHECK (DAY([PeriodStart]) = 1 AND YEAR([PeriodStart]) BETWEEN 1900 AND 9998),
    CONSTRAINT [CK_AttendancePayrollInputRevisions_Revision] CHECK (([Revision] = 1 AND [SupersedesRevisionId] IS NULL) OR ([Revision] > 1 AND [SupersedesRevisionId] IS NOT NULL)),
    CONSTRAINT [FK_AttendancePayrollInputRevisions_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_SupersedesRevisionId] FOREIGN KEY ([EmployeeId], [PeriodStart], [SupersedesRevisionId]) REFERENCES [attendance].[AttendancePayrollInputRevisions] ([EmployeeId], [PeriodStart], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendancePayrollInputRevisions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendancePayrollInputRevisions_UserAccounts_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[CompanyProfiles] (
    [Id] uniqueidentifier NOT NULL,
    [SingletonKey] tinyint NOT NULL,
    [CompanyName] nvarchar(200) NOT NULL,
    [BusinessType] nvarchar(300) NOT NULL,
    [HeadOfficeBranchId] uniqueidentifier NOT NULL,
    [HeadOfficeCityId] uniqueidentifier NOT NULL,
    [Address] nvarchar(500) NOT NULL,
    [JurisdictionCode] nvarchar(10) NOT NULL,
    [Latitude] decimal(9,6) NULL,
    [Longitude] decimal(9,6) NULL,
    [Revision] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_CompanyProfiles] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_CompanyProfiles_Coordinates] CHECK (([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)),
    CONSTRAINT [CK_CompanyProfiles_Jurisdiction] CHECK ([JurisdictionCode] IN ('PK-PB','PK-SD','PK-KP','PK-BA','PK-ICT')),
    CONSTRAINT [CK_CompanyProfiles_Revision] CHECK ([Revision] > 0),
    CONSTRAINT [CK_CompanyProfiles_Singleton] CHECK ([SingletonKey] = 1 AND [Id] = '0871aaf1-f14d-4dc6-a9ba-03eeb2c3047f'),
    CONSTRAINT [FK_CompanyProfiles_Branches_HeadOfficeBranchId] FOREIGN KEY ([HeadOfficeBranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CompanyProfiles_Cities_HeadOfficeCityId] FOREIGN KEY ([HeadOfficeCityId]) REFERENCES [hr].[Cities] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[CompanySiteJurisdictionVersions] (
    [Id] uniqueidentifier NOT NULL,
    [CompanyProfileId] uniqueidentifier NOT NULL,
    [SiteId] uniqueidentifier NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [JurisdictionCode] nvarchar(10) NOT NULL,
    [ConfigurationRevision] int NOT NULL,
    [SupersedesVersionId] uniqueidentifier NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CompanySiteJurisdictionVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_CompanySiteJurisdictions_Date] CHECK ([EffectiveFrom] >= '19010101' AND [EffectiveFrom] < '99980101'),
    CONSTRAINT [CK_CompanySiteJurisdictions_Jurisdiction] CHECK ([JurisdictionCode] IN ('PK-PB','PK-SD','PK-KP','PK-BA','PK-ICT')),
    CONSTRAINT [CK_CompanySiteJurisdictions_Revision] CHECK ([ConfigurationRevision] > 1),
    CONSTRAINT [CK_CompanySiteJurisdictions_Supersession] CHECK ([SupersedesVersionId] IS NULL OR [SupersedesVersionId] <> [Id]),
    CONSTRAINT [FK_CompanySiteJurisdictionVersions_CompanyProfiles_CompanyProfileId] FOREIGN KEY ([CompanyProfileId]) REFERENCES [hr].[CompanyProfiles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CompanySiteJurisdictionVersions_CompanySiteJurisdictionVersions_SupersedesVersionId] FOREIGN KEY ([SupersedesVersionId]) REFERENCES [hr].[CompanySiteJurisdictionVersions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CompanySiteJurisdictionVersions_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_AttendancePayrollInputRevisions_ActorUserId] ON [attendance].[AttendancePayrollInputRevisions] ([ActorUserId]);

CREATE UNIQUE INDEX [IX_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_Revision] ON [attendance].[AttendancePayrollInputRevisions] ([EmployeeId], [PeriodStart], [Revision]);

CREATE INDEX [IX_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_SupersedesRevisionId] ON [attendance].[AttendancePayrollInputRevisions] ([EmployeeId], [PeriodStart], [SupersedesRevisionId]);

CREATE INDEX [IX_CompanyProfiles_HeadOfficeBranchId] ON [hr].[CompanyProfiles] ([HeadOfficeBranchId]);

CREATE INDEX [IX_CompanyProfiles_HeadOfficeCityId] ON [hr].[CompanyProfiles] ([HeadOfficeCityId]);

CREATE UNIQUE INDEX [IX_CompanyProfiles_SingletonKey] ON [hr].[CompanyProfiles] ([SingletonKey]);

CREATE UNIQUE INDEX [IX_CompanySiteJurisdictionVersions_CompanyProfileId_ConfigurationRevision] ON [hr].[CompanySiteJurisdictionVersions] ([CompanyProfileId], [ConfigurationRevision]);

CREATE UNIQUE INDEX [IX_CompanySiteJurisdictionVersions_SiteId_EffectiveFrom] ON [hr].[CompanySiteJurisdictionVersions] ([SiteId], [EffectiveFrom]) WHERE [SupersedesVersionId] IS NULL;

CREATE UNIQUE INDEX [IX_CompanySiteJurisdictionVersions_SupersedesVersionId] ON [hr].[CompanySiteJurisdictionVersions] ([SupersedesVersionId]) WHERE [SupersedesVersionId] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261012135725_AddAttendancePayrollInputsAndCompanySettings', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [hr].[PolicyVersions] ADD CONSTRAINT [AK_PolicyVersions_PolicyId_Id] UNIQUE ([PolicyId], [Id]);

CREATE TABLE [attendance].[AttendanceLegalReviewDossiers] (
    [Id] uniqueidentifier NOT NULL,
    [SourcePolicyId] uniqueidentifier NOT NULL,
    [SourcePolicyVersionId] uniqueidentifier NOT NULL,
    [PolicySha256] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [BindingSha256] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Revision] int NOT NULL,
    [SupersedesId] uniqueidentifier NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [RecordedByAccountId] uniqueidentifier NOT NULL,
    [RecordedAtUtc] datetimeoffset(7) NOT NULL,
    [ProtectedEvidenceReference] nvarchar(500) NOT NULL,
    [EvidenceSha256] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [State] varchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [VerificationMode] varchar(48) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CONSTRAINT [PK_AttendanceLegalReviewDossiers] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_AttendanceLegalReviewDossiers_SourcePolicyVersionId_PolicySha256_BindingSha256_Id] UNIQUE ([SourcePolicyVersionId], [PolicySha256], [BindingSha256], [Id]),
    CONSTRAINT [CK_AttendanceLegalReviewDossiers_BindingSha256] CHECK (LEN([BindingSha256]) = 64 AND [BindingSha256] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_AttendanceLegalReviewDossiers_Dates] CHECK (YEAR([EffectiveFrom]) BETWEEN 1901 AND 9997 AND ([EffectiveTo] IS NULL OR (YEAR([EffectiveTo]) BETWEEN 1901 AND 9997 AND [EffectiveTo] > [EffectiveFrom]))),
    CONSTRAINT [CK_AttendanceLegalReviewDossiers_EvidenceSha256] CHECK (LEN([EvidenceSha256]) = 64 AND [EvidenceSha256] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_AttendanceLegalReviewDossiers_PolicySha256] CHECK (LEN([PolicySha256]) = 64 AND [PolicySha256] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_AttendanceLegalReviewDossiers_Predecessor] CHECK ([SupersedesId] IS NULL OR [SupersedesId] <> [Id]),
    CONSTRAINT [CK_AttendanceLegalReviewDossiers_RecordedAtUtc_Utc] CHECK ([RecordedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RecordedAtUtc]) = 0),
    CONSTRAINT [CK_AttendanceLegalReviewDossiers_RecordOnly] CHECK ([State] = 'review_required' AND [VerificationMode] = 'recorded_not_legally_verified'),
    CONSTRAINT [CK_AttendanceLegalReviewDossiers_Revision] CHECK ([Revision] BETWEEN 1 AND 128 AND (([Revision] = 1 AND [SupersedesId] IS NULL) OR ([Revision] > 1 AND [SupersedesId] IS NOT NULL))),
    CONSTRAINT [FK_AttendanceLegalReviewDossiers_AttendanceLegalReviewDossiers_SourcePolicyVersionId_PolicySha256_BindingSha256_SupersedesId] FOREIGN KEY ([SourcePolicyVersionId], [PolicySha256], [BindingSha256], [SupersedesId]) REFERENCES [attendance].[AttendanceLegalReviewDossiers] ([SourcePolicyVersionId], [PolicySha256], [BindingSha256], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceLegalReviewDossiers_PolicyVersions_SourcePolicyId_SourcePolicyVersionId] FOREIGN KEY ([SourcePolicyId], [SourcePolicyVersionId]) REFERENCES [hr].[PolicyVersions] ([PolicyId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendanceLegalReviewDossiers_UserAccounts_RecordedByAccountId] FOREIGN KEY ([RecordedByAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[AttendancePayrollAttestations] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [PeriodStart] date NOT NULL,
    [SourceRevisionId] uniqueidentifier NOT NULL,
    [SourceRevision] int NOT NULL,
    [SourceFingerprint] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [PreviousAttestationId] uniqueidentifier NULL,
    [Status] varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Step] int NOT NULL,
    [LineFingerprint] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ExternalReceiptKey] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [OccurredAtUtc] datetimeoffset(7) NOT NULL,
    [PostedAtUtc] datetimeoffset(7) NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [CreatedAtUtc] datetimeoffset(7) NOT NULL,
    [ProtectedEvidenceReference] nvarchar(500) NOT NULL,
    [EvidenceFingerprint] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CONSTRAINT [PK_AttendancePayrollAttestations] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_AttendancePayrollAttestations_EmployeeId_PeriodStart_SourceRevisionId_Id] UNIQUE ([EmployeeId], [PeriodStart], [SourceRevisionId], [Id]),
    CONSTRAINT [CK_AttendancePayrollAttestations_CreatedAtUtc_Utc] CHECK ([CreatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CreatedAtUtc]) = 0),
    CONSTRAINT [CK_AttendancePayrollAttestations_EvidenceFingerprint] CHECK (LEN([EvidenceFingerprint]) = 64 AND [EvidenceFingerprint] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_AttendancePayrollAttestations_ExternalReceiptKey] CHECK (LEN([ExternalReceiptKey]) = 64 AND [ExternalReceiptKey] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_AttendancePayrollAttestations_LineFingerprint] CHECK (LEN([LineFingerprint]) = 64 AND [LineFingerprint] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_AttendancePayrollAttestations_OccurredAtUtc_Utc] CHECK ([OccurredAtUtc] IS NULL OR DATEPART(TZOFFSET, [OccurredAtUtc]) = 0),
    CONSTRAINT [CK_AttendancePayrollAttestations_Period] CHECK (DAY([PeriodStart]) = 1 AND YEAR([PeriodStart]) BETWEEN 1900 AND 9998),
    CONSTRAINT [CK_AttendancePayrollAttestations_PostedAtUtc_Utc] CHECK ([PostedAtUtc] IS NULL OR DATEPART(TZOFFSET, [PostedAtUtc]) = 0),
    CONSTRAINT [CK_AttendancePayrollAttestations_SourceFingerprint] CHECK (LEN([SourceFingerprint]) = 64 AND [SourceFingerprint] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_AttendancePayrollAttestations_SourceRevision] CHECK ([SourceRevision] >= 1),
    CONSTRAINT [CK_AttendancePayrollAttestations_Stage] CHECK (([Status] = 'handoff' AND [Step] = 1 AND [PreviousAttestationId] IS NULL AND [PostedAtUtc] IS NULL) OR ([Status] = 'posted' AND [Step] = 2 AND [PreviousAttestationId] IS NOT NULL AND [PostedAtUtc] IS NOT NULL AND [PostedAtUtc] = [OccurredAtUtc]) OR ([Status] = 'settled' AND [Step] = 3 AND [PreviousAttestationId] IS NOT NULL AND [PostedAtUtc] IS NOT NULL AND [PostedAtUtc] <= [OccurredAtUtc])),
    CONSTRAINT [CK_AttendancePayrollAttestations_Time] CHECK ([OccurredAtUtc] <= [CreatedAtUtc]),
    CONSTRAINT [FK_AttendancePayrollAttestations_AttendancePayrollAttestations_EmployeeId_PeriodStart_SourceRevisionId_PreviousAttestationId] FOREIGN KEY ([EmployeeId], [PeriodStart], [SourceRevisionId], [PreviousAttestationId]) REFERENCES [attendance].[AttendancePayrollAttestations] ([EmployeeId], [PeriodStart], [SourceRevisionId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendancePayrollAttestations_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_SourceRevisionId] FOREIGN KEY ([EmployeeId], [PeriodStart], [SourceRevisionId]) REFERENCES [attendance].[AttendancePayrollInputRevisions] ([EmployeeId], [PeriodStart], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendancePayrollAttestations_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AttendancePayrollAttestations_UserAccounts_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_AttendanceLegalReviewDossiers_BindingSha256_Revision] ON [attendance].[AttendanceLegalReviewDossiers] ([BindingSha256], [Revision]);

CREATE INDEX [IX_AttendanceLegalReviewDossiers_RecordedByAccountId] ON [attendance].[AttendanceLegalReviewDossiers] ([RecordedByAccountId]);

CREATE INDEX [IX_AttendanceLegalReviewDossiers_SourcePolicyId_SourcePolicyVersionId] ON [attendance].[AttendanceLegalReviewDossiers] ([SourcePolicyId], [SourcePolicyVersionId]);

CREATE INDEX [IX_AttendanceLegalReviewDossiers_SourcePolicyVersionId_PolicySha256_BindingSha256_SupersedesId] ON [attendance].[AttendanceLegalReviewDossiers] ([SourcePolicyVersionId], [PolicySha256], [BindingSha256], [SupersedesId]);

CREATE UNIQUE INDEX [IX_AttendanceLegalReviewDossiers_SupersedesId] ON [attendance].[AttendanceLegalReviewDossiers] ([SupersedesId]) WHERE [SupersedesId] IS NOT NULL;

CREATE INDEX [IX_AttendancePayrollAttestations_ActorUserId] ON [attendance].[AttendancePayrollAttestations] ([ActorUserId]);

CREATE INDEX [IX_AttendancePayrollAttestations_EmployeeId_PeriodStart_SourceRevisionId_PreviousAttestationId] ON [attendance].[AttendancePayrollAttestations] ([EmployeeId], [PeriodStart], [SourceRevisionId], [PreviousAttestationId]);

CREATE UNIQUE INDEX [IX_AttendancePayrollAttestations_EmployeeId_PeriodStart_Step] ON [attendance].[AttendancePayrollAttestations] ([EmployeeId], [PeriodStart], [Step]);

CREATE UNIQUE INDEX [IX_AttendancePayrollAttestations_ExternalReceiptKey_Step] ON [attendance].[AttendancePayrollAttestations] ([ExternalReceiptKey], [Step]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [attendance].[OfficeRosterAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [BranchId] uniqueidentifier NOT NULL,
    [ShiftId] uniqueidentifier NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [HolidayCalendarId] uniqueidentifier NULL,
    [RecordedBy] nvarchar(200) NOT NULL,
    [RecordedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_OfficeRosterAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_OfficeRosterAssignments_Dates] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
    CONSTRAINT [FK_OfficeRosterAssignments_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OfficeRosterAssignments_HolidayCalendars_HolidayCalendarId] FOREIGN KEY ([HolidayCalendarId]) REFERENCES [hr].[HolidayCalendars] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OfficeRosterAssignments_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [attendance].[Shifts] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[OfficeRosterAssignmentEnds] (
    [Id] uniqueidentifier NOT NULL,
    [OfficeRosterAssignmentId] uniqueidentifier NOT NULL,
    [EffectiveTo] date NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [RecordedBy] nvarchar(200) NOT NULL,
    [RecordedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_OfficeRosterAssignmentEnds] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_OfficeRosterAssignmentEnds_Reason] CHECK (LEN(LTRIM(RTRIM([Reason]))) > 0),
    CONSTRAINT [FK_OfficeRosterAssignmentEnds_OfficeRosterAssignments_OfficeRosterAssignmentId] FOREIGN KEY ([OfficeRosterAssignmentId]) REFERENCES [attendance].[OfficeRosterAssignments] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_OfficeRosterAssignmentEnds_OfficeRosterAssignmentId] ON [attendance].[OfficeRosterAssignmentEnds] ([OfficeRosterAssignmentId]);

CREATE UNIQUE INDEX [IX_OfficeRosterAssignments_BranchId_EffectiveFrom] ON [attendance].[OfficeRosterAssignments] ([BranchId], [EffectiveFrom]);

CREATE INDEX [IX_OfficeRosterAssignments_HolidayCalendarId] ON [attendance].[OfficeRosterAssignments] ([HolidayCalendarId]);

CREATE INDEX [IX_OfficeRosterAssignments_ShiftId] ON [attendance].[OfficeRosterAssignments] ([ShiftId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261013101327_AddOfficeRosterHistory', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [hr].[EmployeeStatusChanges] ADD [LifecyclePolicyVersion] nvarchar(100) NULL;

ALTER TABLE [hr].[EmployeeSimAssignments] ADD [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [hr].[EmployeeSimAssignments] ADD [EffectiveOn] date NOT NULL DEFAULT '0001-01-01';

ALTER TABLE [hr].[EmployeeSimAssignments] ADD [IssueDocumentId] uniqueidentifier NULL;

ALTER TABLE [hr].[EmployeeSimAssignments] ADD [Reason] nvarchar(1000) NULL;

ALTER TABLE [hr].[EmployeeSimAssignments] ADD [ReturnDocumentId] uniqueidentifier NULL;

ALTER TABLE [hr].[EmployeeSimAssignments] ADD [ReturnedToAccountId] uniqueidentifier NULL;

ALTER TABLE [hr].[EmployeeSimAssignments] ADD [TransferredFromAssignmentId] uniqueidentifier NULL;

ALTER TABLE [hr].[Employees] ADD [ArchivedAt] datetimeoffset NULL;

ALTER TABLE [hr].[Employees] ADD [IsArchived] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [hr].[Employees] ADD [LastAppliedStatusChangeId] uniqueidentifier NULL;

ALTER TABLE [hr].[EmployeeDocuments] ADD [ActiveScanAttemptId] uniqueidentifier NULL;

ALTER TABLE [hr].[EmployeeDocuments] ADD [HistoryRevision] int NOT NULL DEFAULT 0;

ALTER TABLE [hr].[EmployeeDocuments] ADD [IsScanInProgress] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [hr].[EmployeeDocuments] ADD [LifecycleChangedAt] datetimeoffset NULL;

ALTER TABLE [hr].[EmployeeDocuments] ADD [LifecycleChangedByUserId] nvarchar(200) NULL;

ALTER TABLE [hr].[EmployeeDocuments] ADD [LifecycleReason] nvarchar(1000) NULL;

ALTER TABLE [hr].[EmployeeDocuments] ADD [LifecycleStatus] int NOT NULL DEFAULT 0;

ALTER TABLE [hr].[EmployeeDocuments] ADD [ScanStartedAt] datetimeoffset NULL;

ALTER TABLE [hr].[EmployeeCertifications] ADD [ActionDocumentId] uniqueidentifier NULL;

ALTER TABLE [hr].[EmployeeCertifications] ADD [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [hr].[EmployeeCertifications] ADD [EffectiveOn] date NOT NULL DEFAULT '0001-01-01';

ALTER TABLE [hr].[EmployeeCertifications] ADD [Reason] nvarchar(1000) NULL;

ALTER TABLE [hr].[EmployeeCertifications] ADD [Revision] int NOT NULL DEFAULT 1;

ALTER TABLE [hr].[EmployeeCertifications] ADD [Status] int NOT NULL DEFAULT 1;

ALTER TABLE [hr].[EmployeeCertifications] ADD [SupersedesEmployeeCertificationId] uniqueidentifier NULL;

ALTER TABLE [workflow].[ApprovalDelegations] ADD [Permission] nvarchar(150) NULL;

ALTER TABLE [workflow].[ApprovalDelegations] ADD [RevocationReason] nvarchar(1000) NULL;

ALTER TABLE [workflow].[ApprovalDelegations] ADD [ScopeKind] int NULL;

ALTER TABLE [workflow].[ApprovalDelegations] ADD [TargetId] uniqueidentifier NULL;

EXEC(N'UPDATE [hr].[EmployeeCertifications] SET [EffectiveOn] = [IssuedOn];
UPDATE [hr].[EmployeeSimAssignments] SET [EffectiveOn] = COALESCE([ReturnedOn], [IssuedOn]);

-- Only verified inactive history whose access projections are already fully
-- reconciled may receive a marker. Residual legacy access remains unmarked
-- for explicit upgrade review; this migration fabricates no revocation evidence.
-- Active releases are deliberately excluded: matching cached Active alone does
-- not prove that old grants/devices/face evidence were reconciled.
UPDATE employee SET [LastAppliedStatusChangeId] = change.[Id]
FROM [hr].[Employees] AS employee
CROSS APPLY (
    SELECT TOP (1) s.* FROM [hr].[EmployeeStatusChanges] AS s
    WHERE s.[EmployeeId] = employee.[Id] AND s.[EffectiveAt] <= SYSUTCDATETIME()
    ORDER BY s.[EffectiveAt] DESC, s.[RecordedAt] DESC, s.[Id]
) AS change
INNER JOIN [workflow].[ApprovalRequests] AS request ON request.[Id] = change.[ApprovalRequestId]
INNER JOIN [hr].[EmployeeOnboardingDecisions] AS onboarding ON onboarding.[Id] = employee.[ApprovedOnboardingDecisionId]
    AND onboarding.[EmployeeId] = employee.[Id] AND onboarding.[Decision] = 1
INNER JOIN [hr].[EmployeeSubmissions] AS submission ON submission.[Id] = onboarding.[SubmissionId]
    AND submission.[EmployeeId] = employee.[Id] AND submission.[Version] = employee.[SubmittedVersion]
    AND submission.[Provenance] = onboarding.[Provenance]
WHERE employee.[LastAppliedStatusChangeId] IS NULL AND employee.[OnboardingStatus] = 2
    AND employee.[Status] = change.[NewStatus] AND change.[NewStatus] IN (3,4,5,6)
    AND request.[RequestType] = N''EmployeeStatusChange'' AND request.[SubjectId] = employee.[Id]
    AND request.[State] = 1 AND request.[ClosedAt] = change.[RecordedAt]
    AND NOT EXISTS (SELECT 1 FROM [hr].[EmployeeStatusChanges] AS conflict
        WHERE conflict.[EmployeeId] = employee.[Id] AND conflict.[EffectiveAt] = change.[EffectiveAt]
            AND conflict.[Id] <> change.[Id])
    AND (SELECT COUNT(*) FROM [workflow].[ApprovalDecisions] AS decision
        WHERE decision.[ApprovalRequestId] = request.[Id]) = 1
    AND EXISTS (SELECT 1 FROM [workflow].[ApprovalDecisions] AS decision
        WHERE decision.[ApprovalRequestId] = request.[Id] AND decision.[Decision] = 1
            AND decision.[ActorUserId] = change.[ChangedByUserId]
            AND decision.[DecidedAt] = change.[RecordedAt])
    AND NOT EXISTS (SELECT 1 FROM [attendance].[FaceEnrollments] AS face
        WHERE face.[EmployeeId] = employee.[Id] AND face.[IsActive] = 1)
    AND NOT EXISTS (SELECT 1 FROM [auth].[UserAccounts] AS account
        WHERE account.[EmployeeId] = employee.[Id] AND (
            account.[Status] NOT IN (4,5) OR account.[MobileAccessEnabled] = 1 OR account.[WebAccessEnabled] = 1
            OR EXISTS (SELECT 1 FROM [auth].[RegisteredDevices] AS device
                WHERE device.[UserAccountId] = account.[Id] AND device.[RevokedAt] IS NULL)
            OR EXISTS (SELECT 1 FROM [auth].[UserRoleAssignments] AS assignment
                WHERE assignment.[UserAccountId] = account.[Id] AND assignment.[RevokedAt] IS NULL)
            OR EXISTS (SELECT 1 FROM [auth].[UserSessions] AS session
                WHERE session.[UserAccountId] = account.[Id] AND session.[RevokedAt] IS NULL)
            OR EXISTS (SELECT 1 FROM [auth].[MobileOtpChallenges] AS challenge
                WHERE challenge.[UserAccountId] = account.[Id] AND challenge.[RevokedAt] IS NULL
                    AND challenge.[ConsumedAt] IS NULL)));');

ALTER TABLE [hr].[EmployeeStatusChanges] ADD CONSTRAINT [AK_EmployeeStatusChanges_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]);

ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [AK_EmployeeCertifications_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]);

ALTER TABLE [hr].[EmployeeAssetCustodies] ADD CONSTRAINT [AK_EmployeeAssetCustodies_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]);

CREATE TABLE [hr].[EmployeeArchiveChanges] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [PreviousArchived] bit NOT NULL,
    [IsArchived] bit NOT NULL,
    [EmploymentStatus] int NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [ActorUserId] nvarchar(200) NOT NULL,
    [ActorRoleCode] nvarchar(100) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeArchiveChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmployeeArchiveChanges_RecordedUtc] CHECK (DATEPART(TZOFFSET, [RecordedAt]) = 0),
    CONSTRAINT [CK_EmployeeArchiveChanges_State] CHECK ([PreviousArchived] <> [IsArchived] AND [EmploymentStatus] IN (4, 5, 6)),
    CONSTRAINT [FK_EmployeeArchiveChanges_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeAssetHistory] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EmployeeAssetCustodyId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [Action] int NOT NULL,
    [EvidenceDocumentId] uniqueidentifier NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [Condition] nvarchar(1000) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [ActorUserId] nvarchar(200) NOT NULL,
    [EffectiveAt] datetimeoffset NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [ReceiverAccountId] uniqueidentifier NULL,
    [TransferredFromCustodyId] uniqueidentifier NULL,
    CONSTRAINT [PK_EmployeeAssetHistory] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmployeeAssetHistory_Action] CHECK (([Action] = 1 AND [Revision] = 1 AND [ReceiverAccountId] IS NULL) OR ([Action] = 2 AND [Revision] = 2 AND [ReceiverAccountId] IS NOT NULL AND [TransferredFromCustodyId] IS NULL)),
    CONSTRAINT [CK_EmployeeAssetHistory_Time] CHECK ([RecordedAt] >= [EffectiveAt]),
    CONSTRAINT [FK_EmployeeAssetHistory_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeAssetHistory_EmployeeAssetCustodies_EmployeeAssetCustodyId_EmployeeId] FOREIGN KEY ([EmployeeAssetCustodyId], [EmployeeId]) REFERENCES [hr].[EmployeeAssetCustodies] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeAssetHistory_EmployeeAssetCustodies_TransferredFromCustodyId] FOREIGN KEY ([TransferredFromCustodyId]) REFERENCES [hr].[EmployeeAssetCustodies] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeAssetHistory_EmployeeDocuments_EvidenceDocumentId_EmployeeId] FOREIGN KEY ([EvidenceDocumentId], [EmployeeId]) REFERENCES [hr].[EmployeeDocuments] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeAssetHistory_UserAccounts_ReceiverAccountId] FOREIGN KEY ([ReceiverAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeBankVerifications] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EmployeeBankDetailsId] uniqueidentifier NOT NULL,
    [Version] int NOT NULL,
    [PreviousVerificationId] uniqueidentifier NULL,
    [IsVerified] bit NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [PrivateEvidenceReference] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_EmployeeBankVerifications] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_EmployeeBankVerifications_Id_EmployeeId_EmployeeBankDetailsId] UNIQUE ([Id], [EmployeeId], [EmployeeBankDetailsId]),
    CONSTRAINT [CK_EmployeeBankVerifications_History] CHECK (([Version] = 1 AND [PreviousVerificationId] IS NULL) OR ([Version] > 1 AND [PreviousVerificationId] IS NOT NULL)),
    CONSTRAINT [CK_EmployeeBankVerifications_Predecessor] CHECK ([PreviousVerificationId] IS NULL OR [PreviousVerificationId] <> [Id]),
    CONSTRAINT [CK_EmployeeBankVerifications_Version] CHECK ([Version] > 0),
    CONSTRAINT [FK_EmployeeBankVerifications_EmployeeBankDetails_EmployeeBankDetailsId_EmployeeId] FOREIGN KEY ([EmployeeBankDetailsId], [EmployeeId]) REFERENCES [hr].[EmployeeBankDetails] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeBankVerifications_EmployeeBankVerifications_PreviousVerificationId_EmployeeId_EmployeeBankDetailsId] FOREIGN KEY ([PreviousVerificationId], [EmployeeId], [EmployeeBankDetailsId]) REFERENCES [hr].[EmployeeBankVerifications] ([Id], [EmployeeId], [EmployeeBankDetailsId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeBankVerifications_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [hr].[EmployeeDocumentHistory] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EmployeeDocumentId] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [Action] int NOT NULL,
    [ActorUserId] nvarchar(200) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [ScanAttemptId] uniqueidentifier NULL,
    [BeforeState] nvarchar(max) NOT NULL,
    [AfterState] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_EmployeeDocumentHistory] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmployeeDocumentHistory_Action] CHECK ([Action] BETWEEN 1 AND 9),
    CONSTRAINT [CK_EmployeeDocumentHistory_Revision] CHECK ([Revision] > 0),
    CONSTRAINT [FK_EmployeeDocumentHistory_EmployeeDocuments_EmployeeDocumentId_EmployeeId] FOREIGN KEY ([EmployeeDocumentId], [EmployeeId]) REFERENCES [hr].[EmployeeDocuments] ([Id], [EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeDocumentHistory_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_EmployeeSimAssignments_ApprovalRequestId] ON [hr].[EmployeeSimAssignments] ([ApprovalRequestId]);

CREATE INDEX [IX_EmployeeSimAssignments_IssueDocumentId_EmployeeId] ON [hr].[EmployeeSimAssignments] ([IssueDocumentId], [EmployeeId]);

CREATE INDEX [IX_EmployeeSimAssignments_PhoneNumber_EffectiveOn] ON [hr].[EmployeeSimAssignments] ([PhoneNumber], [EffectiveOn]);

CREATE INDEX [IX_EmployeeSimAssignments_ReturnDocumentId_EmployeeId] ON [hr].[EmployeeSimAssignments] ([ReturnDocumentId], [EmployeeId]);

CREATE INDEX [IX_EmployeeSimAssignments_ReturnedToAccountId] ON [hr].[EmployeeSimAssignments] ([ReturnedToAccountId]);

CREATE INDEX [IX_EmployeeSimAssignments_SimReference_EffectiveOn] ON [hr].[EmployeeSimAssignments] ([SimReference], [EffectiveOn]);

CREATE INDEX [IX_EmployeeSimAssignments_TransferredFromAssignmentId] ON [hr].[EmployeeSimAssignments] ([TransferredFromAssignmentId]);

ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [CK_EmployeeSimAssignments_Effective] CHECK ([EffectiveOn] >= [IssuedOn] AND ([ReturnedOn] IS NULL OR [EffectiveOn] = [ReturnedOn]));

ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [CK_EmployeeSimAssignments_ReturnEvidence] CHECK (([ReturnDocumentId] IS NULL AND [ReturnedToAccountId] IS NULL) OR ([Status] = 3 AND [ReturnDocumentId] IS NOT NULL AND [ReturnedToAccountId] IS NOT NULL));

CREATE INDEX [IX_Employees_IsArchived_CreatedAt_Id] ON [hr].[Employees] ([IsArchived], [CreatedAt], [Id]);

CREATE INDEX [IX_Employees_LastAppliedStatusChangeId_Id] ON [hr].[Employees] ([LastAppliedStatusChangeId], [Id]);

ALTER TABLE [hr].[Employees] ADD CONSTRAINT [CK_Employees_Archive] CHECK (([IsArchived] = 0 AND [ArchivedAt] IS NULL) OR ([IsArchived] = 1 AND [ArchivedAt] IS NOT NULL AND [Status] IN (4, 5, 6)));

ALTER TABLE [hr].[Employees] ADD CONSTRAINT [CK_Employees_ArchiveUtc] CHECK ([ArchivedAt] IS NULL OR DATEPART(TZOFFSET, [ArchivedAt]) = 0);

ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_HistoryRevision] CHECK ([HistoryRevision] >= 0);

ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_Lifecycle] CHECK (([LifecycleStatus] = 0 AND [LifecycleChangedAt] IS NULL AND [LifecycleChangedByUserId] IS NULL AND [LifecycleReason] IS NULL) OR ([LifecycleStatus] BETWEEN 1 AND 3 AND [LifecycleChangedAt] IS NOT NULL AND [LifecycleChangedAt] >= [UploadedAt] AND [LifecycleChangedByUserId] IS NOT NULL AND [LifecycleReason] IS NOT NULL));

ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_ScanAttempt] CHECK (([ActiveScanAttemptId] IS NULL AND [ScanStartedAt] IS NULL AND [IsScanInProgress] = 0) OR ([ActiveScanAttemptId] IS NOT NULL AND [ScanStartedAt] IS NOT NULL AND [ScanStartedAt] >= [UploadedAt]));

CREATE INDEX [IX_EmployeeCertifications_ActionDocumentId_EmployeeId] ON [hr].[EmployeeCertifications] ([ActionDocumentId], [EmployeeId]);

CREATE INDEX [IX_EmployeeCertifications_ApprovalRequestId] ON [hr].[EmployeeCertifications] ([ApprovalRequestId]);

CREATE UNIQUE INDEX [IX_EmployeeCertifications_SupersedesEmployeeCertificationId] ON [hr].[EmployeeCertifications] ([SupersedesEmployeeCertificationId]) WHERE [SupersedesEmployeeCertificationId] IS NOT NULL;

CREATE INDEX [IX_EmployeeCertifications_SupersedesEmployeeCertificationId_EmployeeId] ON [hr].[EmployeeCertifications] ([SupersedesEmployeeCertificationId], [EmployeeId]);

ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [CK_EmployeeCertifications_Revision] CHECK ([Revision] > 0 AND (([Revision] = 1 AND [SupersedesEmployeeCertificationId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeCertificationId] IS NOT NULL)));

ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [CK_EmployeeCertifications_Status] CHECK ([Status] BETWEEN 1 AND 3 AND [EffectiveOn] >= [IssuedOn]);

ALTER TABLE [workflow].[ApprovalDelegations] ADD CONSTRAINT [CK_ApprovalDelegations_RevokeReason] CHECK ([Permission] IS NULL OR [RevokedAt] IS NULL OR ([RevocationReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RevocationReason]))) > 0));

ALTER TABLE [workflow].[ApprovalDelegations] ADD CONSTRAINT [CK_ApprovalDelegations_Scope] CHECK (([Permission] IS NULL AND [ScopeKind] IS NULL AND [TargetId] IS NULL) OR ([Permission] IS NOT NULL AND [ScopeKind] IS NOT NULL AND (([ScopeKind] = 7 AND [TargetId] IS NULL) OR ([ScopeKind] BETWEEN 2 AND 6 AND [TargetId] IS NOT NULL))));

CREATE INDEX [IX_EmployeeArchiveChanges_EmployeeId_RecordedAt_Id] ON [hr].[EmployeeArchiveChanges] ([EmployeeId], [RecordedAt], [Id]);

CREATE INDEX [IX_EmployeeAssetHistory_ApprovalRequestId] ON [hr].[EmployeeAssetHistory] ([ApprovalRequestId]);

CREATE INDEX [IX_EmployeeAssetHistory_EmployeeAssetCustodyId_EmployeeId] ON [hr].[EmployeeAssetHistory] ([EmployeeAssetCustodyId], [EmployeeId]);

CREATE UNIQUE INDEX [IX_EmployeeAssetHistory_EmployeeAssetCustodyId_Revision] ON [hr].[EmployeeAssetHistory] ([EmployeeAssetCustodyId], [Revision]);

CREATE INDEX [IX_EmployeeAssetHistory_EmployeeId_RecordedAt] ON [hr].[EmployeeAssetHistory] ([EmployeeId], [RecordedAt]);

CREATE INDEX [IX_EmployeeAssetHistory_EvidenceDocumentId_EmployeeId] ON [hr].[EmployeeAssetHistory] ([EvidenceDocumentId], [EmployeeId]);

CREATE INDEX [IX_EmployeeAssetHistory_ReceiverAccountId] ON [hr].[EmployeeAssetHistory] ([ReceiverAccountId]);

CREATE INDEX [IX_EmployeeAssetHistory_TransferredFromCustodyId] ON [hr].[EmployeeAssetHistory] ([TransferredFromCustodyId]);

CREATE INDEX [IX_EmployeeBankVerifications_EmployeeBankDetailsId_EmployeeId] ON [hr].[EmployeeBankVerifications] ([EmployeeBankDetailsId], [EmployeeId]);

CREATE UNIQUE INDEX [IX_EmployeeBankVerifications_EmployeeBankDetailsId_Version] ON [hr].[EmployeeBankVerifications] ([EmployeeBankDetailsId], [Version]);

CREATE INDEX [IX_EmployeeBankVerifications_EmployeeId] ON [hr].[EmployeeBankVerifications] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_EmployeeBankVerifications_PreviousVerificationId] ON [hr].[EmployeeBankVerifications] ([PreviousVerificationId]) WHERE [PreviousVerificationId] IS NOT NULL;

CREATE INDEX [IX_EmployeeBankVerifications_PreviousVerificationId_EmployeeId_EmployeeBankDetailsId] ON [hr].[EmployeeBankVerifications] ([PreviousVerificationId], [EmployeeId], [EmployeeBankDetailsId]);

CREATE INDEX [IX_EmployeeDocumentHistory_EmployeeDocumentId_EmployeeId] ON [hr].[EmployeeDocumentHistory] ([EmployeeDocumentId], [EmployeeId]);

CREATE UNIQUE INDEX [IX_EmployeeDocumentHistory_EmployeeDocumentId_Revision] ON [hr].[EmployeeDocumentHistory] ([EmployeeDocumentId], [Revision]);

CREATE INDEX [IX_EmployeeDocumentHistory_EmployeeId_RecordedAt] ON [hr].[EmployeeDocumentHistory] ([EmployeeId], [RecordedAt]);

ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [FK_EmployeeCertifications_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [FK_EmployeeCertifications_EmployeeCertifications_SupersedesEmployeeCertificationId_EmployeeId] FOREIGN KEY ([SupersedesEmployeeCertificationId], [EmployeeId]) REFERENCES [hr].[EmployeeCertifications] ([Id], [EmployeeId]) ON DELETE NO ACTION;

ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [FK_EmployeeCertifications_EmployeeDocuments_ActionDocumentId_EmployeeId] FOREIGN KEY ([ActionDocumentId], [EmployeeId]) REFERENCES [hr].[EmployeeDocuments] ([Id], [EmployeeId]) ON DELETE NO ACTION;

ALTER TABLE [hr].[Employees] ADD CONSTRAINT [FK_Employees_EmployeeStatusChanges_LastAppliedStatusChangeId_Id] FOREIGN KEY ([LastAppliedStatusChangeId], [Id]) REFERENCES [hr].[EmployeeStatusChanges] ([Id], [EmployeeId]) ON DELETE NO ACTION;

ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [FK_EmployeeSimAssignments_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [FK_EmployeeSimAssignments_EmployeeDocuments_IssueDocumentId_EmployeeId] FOREIGN KEY ([IssueDocumentId], [EmployeeId]) REFERENCES [hr].[EmployeeDocuments] ([Id], [EmployeeId]) ON DELETE NO ACTION;

ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [FK_EmployeeSimAssignments_EmployeeDocuments_ReturnDocumentId_EmployeeId] FOREIGN KEY ([ReturnDocumentId], [EmployeeId]) REFERENCES [hr].[EmployeeDocuments] ([Id], [EmployeeId]) ON DELETE NO ACTION;

ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [FK_EmployeeSimAssignments_EmployeeSimAssignments_TransferredFromAssignmentId] FOREIGN KEY ([TransferredFromAssignmentId]) REFERENCES [hr].[EmployeeSimAssignments] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [FK_EmployeeSimAssignments_UserAccounts_ReturnedToAccountId] FOREIGN KEY ([ReturnedToAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261013121357_CompleteHrFoundationPartA', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [attendance].[ZoneEvents] DROP CONSTRAINT [CK_ZoneEvents_LocationRetention];

ALTER TABLE [attendance].[ZoneEvents] ADD [WrittenClaimExpiresAtUtc] datetimeoffset(7) NULL;

ALTER TABLE [attendance].[ZoneEvents] ADD [WrittenClaimPrivateReference] nvarchar(500) NULL;

ALTER TABLE [attendance].[WorkSessions] ADD [BranchId] uniqueidentifier NULL;

ALTER TABLE [attendance].[WorkSessions] ADD [EmployeeAssignmentId] uniqueidentifier NULL;

ALTER TABLE [attendance].[WorkSessions] ADD [HolidayCalendarId] uniqueidentifier NULL;

ALTER TABLE [attendance].[WorkSessions] ADD [OfficeGeofenceVersionId] uniqueidentifier NULL;

ALTER TABLE [attendance].[WorkSessions] ADD [OfficeRosterAssignmentId] uniqueidentifier NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [BranchId] uniqueidentifier NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [OfficeGeofenceVersionId] uniqueidentifier NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [OfficeRosterAssignmentId] uniqueidentifier NULL;

ALTER TABLE [attendance].[OfficeRosterAssignments] ADD CONSTRAINT [AK_OfficeRosterAssignments_BranchId_Id] UNIQUE ([BranchId], [Id]);

CREATE TABLE [attendance].[CaptureValidationPolicyVersions] (
    [Id] uniqueidentifier NOT NULL,
    [Revision] int NOT NULL,
    [SupersedesVersionId] uniqueidentifier NULL,
    [EffectiveFromUtc] datetimeoffset(7) NOT NULL,
    [PublishedAtUtc] datetimeoffset(7) NOT NULL,
    [PublishedBy] nvarchar(200) NOT NULL,
    [ApprovalReference] nvarchar(200) NOT NULL,
    [SettingsJson] nvarchar(max) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [SourceFingerprint] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ProtectedEvidenceReference] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_CaptureValidationPolicyVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_CaptureValidationPolicyVersions_Id_Revision_SourceFingerprint] UNIQUE ([Id], [Revision], [SourceFingerprint]),
    CONSTRAINT [CK_CaptureValidationPolicyVersions_EffectiveFromUtc_Utc] CHECK ([EffectiveFromUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveFromUtc]) = 0),
    CONSTRAINT [CK_CaptureValidationPolicyVersions_Hash] CHECK (LEN([SourceFingerprint]) = 64 AND [SourceFingerprint] NOT LIKE '%[^0-9A-F]%'),
    CONSTRAINT [CK_CaptureValidationPolicyVersions_PublishedAtUtc_Utc] CHECK ([PublishedAtUtc] IS NULL OR DATEPART(TZOFFSET, [PublishedAtUtc]) = 0),
    CONSTRAINT [CK_CaptureValidationPolicyVersions_Revision] CHECK (([Revision] = 1 AND [SupersedesVersionId] IS NULL) OR ([Revision] > 1 AND [SupersedesVersionId] IS NOT NULL)),
    CONSTRAINT [CK_CaptureValidationPolicyVersions_Settings] CHECK (ISJSON([SettingsJson]) = 1 AND LEN([SettingsJson]) <= 32768),
    CONSTRAINT [CK_CaptureValidationPolicyVersions_Window] CHECK ([EffectiveFromUtc] >= [PublishedAtUtc]),
    CONSTRAINT [FK_CaptureValidationPolicyVersions_CaptureValidationPolicyVersions_SupersedesVersionId] FOREIGN KEY ([SupersedesVersionId]) REFERENCES [attendance].[CaptureValidationPolicyVersions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[OfficeGeofenceVersions] (
    [Id] uniqueidentifier NOT NULL,
    [BranchId] uniqueidentifier NOT NULL,
    [VersionNumber] int NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [Latitude] decimal(10,7) NOT NULL,
    [Longitude] decimal(10,7) NOT NULL,
    [RadiusMeters] decimal(10,2) NOT NULL,
    [MaximumAccuracyMeters] decimal(10,2) NOT NULL,
    [JurisdictionCode] varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ApprovalReference] nvarchar(500) NOT NULL,
    [RecordedBy] nvarchar(200) NOT NULL,
    [RecordedAtUtc] datetimeoffset(7) NOT NULL,
    CONSTRAINT [PK_OfficeGeofenceVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_OfficeGeofenceVersions_BranchId_Id] UNIQUE ([BranchId], [Id]),
    CONSTRAINT [CK_OfficeGeofenceVersions_Geometry] CHECK ([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND [RadiusMeters] > 0 AND [MaximumAccuracyMeters] > 0),
    CONSTRAINT [CK_OfficeGeofenceVersions_RecordedUtc] CHECK (DATEPART(TZOFFSET,[RecordedAtUtc]) = 0),
    CONSTRAINT [CK_OfficeGeofenceVersions_Version] CHECK ([VersionNumber] > 0),
    CONSTRAINT [FK_OfficeGeofenceVersions_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [attendance].[CaptureValidationFinalEvidence] (
    [Id] uniqueidentifier NOT NULL,
    [RawPunchEventId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Action] int NOT NULL,
    [IsOffline] bit NOT NULL,
    [RegisteredDeviceId] nvarchar(200) NOT NULL,
    [RequestSha256] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ReceivedAtUtc] datetimeoffset(7) NOT NULL,
    [EvaluatedAtUtc] datetimeoffset(7) NOT NULL,
    [AssessedAtUtc] datetimeoffset(7) NOT NULL,
    [CandidateOutcome] int NOT NULL,
    [CandidateReasonCode] nvarchar(100) NOT NULL,
    [FinalOutcome] int NOT NULL,
    [FinalReasonCode] nvarchar(100) NOT NULL,
    [PolicyVersionId] uniqueidentifier NULL,
    [PolicyRevision] int NULL,
    [SourceFingerprint] char(64) COLLATE Latin1_General_100_BIN2 NULL,
    [CodesJson] nvarchar(max) NOT NULL,
    [RelatedRawPunchIdsJson] nvarchar(max) NOT NULL,
    [DeviceObservationCode] nvarchar(100) NULL,
    CONSTRAINT [PK_CaptureValidationFinalEvidence] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_CaptureValidationFinalEvidence_Acceptance] CHECK ([FinalOutcome] NOT IN (2, 3) OR ([CandidateOutcome] IN (2, 3) AND [PolicyVersionId] IS NOT NULL)),
    CONSTRAINT [CK_CaptureValidationFinalEvidence_Action] CHECK ([Action] IN (1, 2)),
    CONSTRAINT [CK_CaptureValidationFinalEvidence_AssessedAtUtc_Utc] CHECK ([AssessedAtUtc] IS NULL OR DATEPART(TZOFFSET, [AssessedAtUtc]) = 0),
    CONSTRAINT [CK_CaptureValidationFinalEvidence_Clock] CHECK ([ReceivedAtUtc] <= [EvaluatedAtUtc] AND [EvaluatedAtUtc] <= [AssessedAtUtc]),
    CONSTRAINT [CK_CaptureValidationFinalEvidence_EvaluatedAtUtc_Utc] CHECK ([EvaluatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [EvaluatedAtUtc]) = 0),
    CONSTRAINT [CK_CaptureValidationFinalEvidence_Json] CHECK (ISJSON([CodesJson]) = 1 AND ISJSON([RelatedRawPunchIdsJson]) = 1),
    CONSTRAINT [CK_CaptureValidationFinalEvidence_Offline] CHECK ([IsOffline] = 0 OR ([CandidateOutcome] = 1 AND [FinalOutcome] = 1)),
    CONSTRAINT [CK_CaptureValidationFinalEvidence_Outcome] CHECK ([CandidateOutcome] BETWEEN 1 AND 5 AND [FinalOutcome] BETWEEN 1 AND 5),
    CONSTRAINT [CK_CaptureValidationFinalEvidence_Policy] CHECK (([PolicyVersionId] IS NULL AND [PolicyRevision] IS NULL AND [SourceFingerprint] IS NULL) OR ([PolicyVersionId] IS NOT NULL AND [PolicyRevision] IS NOT NULL AND [SourceFingerprint] IS NOT NULL AND [PolicyRevision] > 0 AND LEN([SourceFingerprint]) = 64)),
    CONSTRAINT [CK_CaptureValidationFinalEvidence_ReceivedAtUtc_Utc] CHECK ([ReceivedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ReceivedAtUtc]) = 0),
    CONSTRAINT [FK_CaptureValidationFinalEvidence_CaptureValidationPolicyVersions_PolicyVersionId_PolicyRevision_SourceFingerprint] FOREIGN KEY ([PolicyVersionId], [PolicyRevision], [SourceFingerprint]) REFERENCES [attendance].[CaptureValidationPolicyVersions] ([Id], [Revision], [SourceFingerprint]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CaptureValidationFinalEvidence_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CaptureValidationFinalEvidence_RawPunchEvents_EmployeeId_RawPunchEventId] FOREIGN KEY ([EmployeeId], [RawPunchEventId]) REFERENCES [attendance].[RawPunchEvents] ([EmployeeId], [Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ZoneEvents_WrittenClaimExpiresAtUtc_Id] ON [attendance].[ZoneEvents] ([WrittenClaimExpiresAtUtc], [Id]);

ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_LocationRetention] CHECK (([LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [AccuracyMeters] IS NOT NULL) OR ([LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LocationExpiresAtUtc] > [ReceivedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL) OR ([WrittenClaimPrivateReference] IS NOT NULL AND [WrittenClaimExpiresAtUtc] IS NOT NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL));

ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_WrittenClaim] CHECK (([WrittenClaimPrivateReference] IS NULL AND [WrittenClaimExpiresAtUtc] IS NULL) OR ([WrittenClaimPrivateReference] IS NOT NULL AND [WrittenClaimExpiresAtUtc] IS NOT NULL AND [Source] = 3 AND [IsOffline] = 1 AND [DeviceCapturedAtUtc] IS NOT NULL AND [DeviceCapturedAtUtc] <= [ReceivedAtUtc] AND [WrittenClaimExpiresAtUtc] > [ReceivedAtUtc] AND [WrittenClaimExpiresAtUtc] > [DeviceCapturedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL));

ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_WrittenClaimExpiresAtUtc_Utc] CHECK ([WrittenClaimExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [WrittenClaimExpiresAtUtc]) = 0);

CREATE INDEX [IX_WorkSessions_BranchId_OfficeGeofenceVersionId] ON [attendance].[WorkSessions] ([BranchId], [OfficeGeofenceVersionId]);

CREATE INDEX [IX_WorkSessions_BranchId_OfficeRosterAssignmentId] ON [attendance].[WorkSessions] ([BranchId], [OfficeRosterAssignmentId]);

CREATE INDEX [IX_WorkSessions_EmployeeId_EmployeeAssignmentId] ON [attendance].[WorkSessions] ([EmployeeId], [EmployeeAssignmentId]);

CREATE INDEX [IX_WorkSessions_HolidayCalendarId] ON [attendance].[WorkSessions] ([HolidayCalendarId]);

ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [CK_WorkSessions_OfficeContext] CHECK (([BranchId] IS NULL AND [OfficeRosterAssignmentId] IS NULL AND [OfficeGeofenceVersionId] IS NULL) OR ([BranchId] IS NOT NULL AND [SiteId] IS NULL AND [OfficeRosterAssignmentId] IS NOT NULL AND [EmployeeAssignmentId] IS NOT NULL AND [OfficeGeofenceVersionId] IS NOT NULL));

CREATE INDEX [IX_RawPunchEvents_BranchId_OfficeGeofenceVersionId] ON [attendance].[RawPunchEvents] ([BranchId], [OfficeGeofenceVersionId]);

CREATE INDEX [IX_RawPunchEvents_BranchId_OfficeRosterAssignmentId] ON [attendance].[RawPunchEvents] ([BranchId], [OfficeRosterAssignmentId]);

ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [CK_RawPunchEvents_OfficeContext] CHECK (([BranchId] IS NULL AND [OfficeRosterAssignmentId] IS NULL AND [OfficeGeofenceVersionId] IS NULL) OR ([BranchId] IS NOT NULL AND [BusinessDate] IS NOT NULL AND [SiteId] IS NULL AND [GeofenceVersionId] IS NULL AND [SiteRosterAssignmentId] IS NULL AND [TemporaryShiftChangeId] IS NULL));

CREATE INDEX [IX_CaptureValidationFinalEvidence_EmployeeId_RawPunchEventId] ON [attendance].[CaptureValidationFinalEvidence] ([EmployeeId], [RawPunchEventId]);

CREATE INDEX [IX_CaptureValidationFinalEvidence_EmployeeId_ReceivedAtUtc] ON [attendance].[CaptureValidationFinalEvidence] ([EmployeeId], [ReceivedAtUtc]);

CREATE INDEX [IX_CaptureValidationFinalEvidence_PolicyVersionId_PolicyRevision_SourceFingerprint] ON [attendance].[CaptureValidationFinalEvidence] ([PolicyVersionId], [PolicyRevision], [SourceFingerprint]);

CREATE UNIQUE INDEX [IX_CaptureValidationFinalEvidence_RawPunchEventId] ON [attendance].[CaptureValidationFinalEvidence] ([RawPunchEventId]);

CREATE INDEX [IX_CaptureValidationPolicyVersions_EffectiveFromUtc_Revision] ON [attendance].[CaptureValidationPolicyVersions] ([EffectiveFromUtc], [Revision]);

CREATE UNIQUE INDEX [IX_CaptureValidationPolicyVersions_Revision] ON [attendance].[CaptureValidationPolicyVersions] ([Revision]);

CREATE UNIQUE INDEX [IX_CaptureValidationPolicyVersions_SupersedesVersionId] ON [attendance].[CaptureValidationPolicyVersions] ([SupersedesVersionId]) WHERE [SupersedesVersionId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_OfficeGeofenceVersions_BranchId_EffectiveFrom] ON [attendance].[OfficeGeofenceVersions] ([BranchId], [EffectiveFrom]);

CREATE UNIQUE INDEX [IX_OfficeGeofenceVersions_BranchId_VersionNumber] ON [attendance].[OfficeGeofenceVersions] ([BranchId], [VersionNumber]);

ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_OfficeGeofenceVersions_BranchId_OfficeGeofenceVersionId] FOREIGN KEY ([BranchId], [OfficeGeofenceVersionId]) REFERENCES [attendance].[OfficeGeofenceVersions] ([BranchId], [Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_OfficeRosterAssignments_BranchId_OfficeRosterAssignmentId] FOREIGN KEY ([BranchId], [OfficeRosterAssignmentId]) REFERENCES [attendance].[OfficeRosterAssignments] ([BranchId], [Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [FK_WorkSessions_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [FK_WorkSessions_EmployeeAssignments_EmployeeId_EmployeeAssignmentId] FOREIGN KEY ([EmployeeId], [EmployeeAssignmentId]) REFERENCES [hr].[EmployeeAssignments] ([EmployeeId], [Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [FK_WorkSessions_HolidayCalendars_HolidayCalendarId] FOREIGN KEY ([HolidayCalendarId]) REFERENCES [hr].[HolidayCalendars] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [FK_WorkSessions_OfficeGeofenceVersions_BranchId_OfficeGeofenceVersionId] FOREIGN KEY ([BranchId], [OfficeGeofenceVersionId]) REFERENCES [attendance].[OfficeGeofenceVersions] ([BranchId], [Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [FK_WorkSessions_OfficeRosterAssignments_BranchId_OfficeRosterAssignmentId] FOREIGN KEY ([BranchId], [OfficeRosterAssignmentId]) REFERENCES [attendance].[OfficeRosterAssignments] ([BranchId], [Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims', N'10.0.12');

COMMIT;
GO

