BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[PolicyVersions]') AND [c].[name] = N'ApprovalRequestId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [hr].[PolicyVersions] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [hr].[PolicyVersions] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [hr].[PolicyVersions] ADD [Authority] int NOT NULL DEFAULT 1;

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[PolicyAssignments]') AND [c].[name] = N'ApprovalRequestId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [hr].[PolicyAssignments] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [hr].[PolicyAssignments] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [hr].[PolicyAssignments] ADD [Authority] int NOT NULL DEFAULT 1;

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[PolicyAssignmentEnds]') AND [c].[name] = N'ApprovalRequestId');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [hr].[PolicyAssignmentEnds] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [hr].[PolicyAssignmentEnds] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [hr].[PolicyAssignmentEnds] ADD [Authority] int NOT NULL DEFAULT 1;

ALTER TABLE [hr].[Policies] ADD [Revision] int NOT NULL DEFAULT 0;

ALTER TABLE [hr].[LeaveTypes] ADD [Kind] int NOT NULL DEFAULT 0;

DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[LeaveRequests]') AND [c].[name] = N'PolicyVersionId');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [hr].[LeaveRequests] DROP CONSTRAINT ' + @var3 + ';');
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

