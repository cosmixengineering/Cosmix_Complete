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

