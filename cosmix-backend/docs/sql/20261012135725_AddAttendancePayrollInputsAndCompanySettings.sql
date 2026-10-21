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

