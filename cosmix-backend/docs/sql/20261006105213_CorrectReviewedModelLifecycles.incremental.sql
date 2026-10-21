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

