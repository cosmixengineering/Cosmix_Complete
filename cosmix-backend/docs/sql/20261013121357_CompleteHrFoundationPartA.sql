BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeStatusChanges] ADD [LifecyclePolicyVersion] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD [ApprovalRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD [EffectiveOn] date NOT NULL DEFAULT '0001-01-01';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD [IssueDocumentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD [Reason] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD [ReturnDocumentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD [ReturnedToAccountId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD [TransferredFromAssignmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[Employees] ADD [ArchivedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[Employees] ADD [IsArchived] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[Employees] ADD [LastAppliedStatusChangeId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeDocuments] ADD [ActiveScanAttemptId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeDocuments] ADD [HistoryRevision] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeDocuments] ADD [IsScanInProgress] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeDocuments] ADD [LifecycleChangedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeDocuments] ADD [LifecycleChangedByUserId] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeDocuments] ADD [LifecycleReason] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeDocuments] ADD [LifecycleStatus] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeDocuments] ADD [ScanStartedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeCertifications] ADD [ActionDocumentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeCertifications] ADD [ApprovalRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeCertifications] ADD [EffectiveOn] date NOT NULL DEFAULT '0001-01-01';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeCertifications] ADD [Reason] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeCertifications] ADD [Revision] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeCertifications] ADD [Status] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeCertifications] ADD [SupersedesEmployeeCertificationId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [workflow].[ApprovalDelegations] ADD [Permission] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [workflow].[ApprovalDelegations] ADD [RevocationReason] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [workflow].[ApprovalDelegations] ADD [ScopeKind] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [workflow].[ApprovalDelegations] ADD [TargetId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeStatusChanges] ADD CONSTRAINT [AK_EmployeeStatusChanges_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [AK_EmployeeCertifications_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeAssetCustodies] ADD CONSTRAINT [AK_EmployeeAssetCustodies_Id_EmployeeId] UNIQUE ([Id], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeSimAssignments_ApprovalRequestId] ON [hr].[EmployeeSimAssignments] ([ApprovalRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeSimAssignments_IssueDocumentId_EmployeeId] ON [hr].[EmployeeSimAssignments] ([IssueDocumentId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeSimAssignments_PhoneNumber_EffectiveOn] ON [hr].[EmployeeSimAssignments] ([PhoneNumber], [EffectiveOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeSimAssignments_ReturnDocumentId_EmployeeId] ON [hr].[EmployeeSimAssignments] ([ReturnDocumentId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeSimAssignments_ReturnedToAccountId] ON [hr].[EmployeeSimAssignments] ([ReturnedToAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeSimAssignments_SimReference_EffectiveOn] ON [hr].[EmployeeSimAssignments] ([SimReference], [EffectiveOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeSimAssignments_TransferredFromAssignmentId] ON [hr].[EmployeeSimAssignments] ([TransferredFromAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [CK_EmployeeSimAssignments_Effective] CHECK ([EffectiveOn] >= [IssuedOn] AND ([ReturnedOn] IS NULL OR [EffectiveOn] = [ReturnedOn]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [CK_EmployeeSimAssignments_ReturnEvidence] CHECK (([ReturnDocumentId] IS NULL AND [ReturnedToAccountId] IS NULL) OR ([Status] = 3 AND [ReturnDocumentId] IS NOT NULL AND [ReturnedToAccountId] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_Employees_IsArchived_CreatedAt_Id] ON [hr].[Employees] ([IsArchived], [CreatedAt], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_Employees_LastAppliedStatusChangeId_Id] ON [hr].[Employees] ([LastAppliedStatusChangeId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'ALTER TABLE [hr].[Employees] ADD CONSTRAINT [CK_Employees_Archive] CHECK (([IsArchived] = 0 AND [ArchivedAt] IS NULL) OR ([IsArchived] = 1 AND [ArchivedAt] IS NOT NULL AND [Status] IN (4, 5, 6)))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'ALTER TABLE [hr].[Employees] ADD CONSTRAINT [CK_Employees_ArchiveUtc] CHECK ([ArchivedAt] IS NULL OR DATEPART(TZOFFSET, [ArchivedAt]) = 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_HistoryRevision] CHECK ([HistoryRevision] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_Lifecycle] CHECK (([LifecycleStatus] = 0 AND [LifecycleChangedAt] IS NULL AND [LifecycleChangedByUserId] IS NULL AND [LifecycleReason] IS NULL) OR ([LifecycleStatus] BETWEEN 1 AND 3 AND [LifecycleChangedAt] IS NOT NULL AND [LifecycleChangedAt] >= [UploadedAt] AND [LifecycleChangedByUserId] IS NOT NULL AND [LifecycleReason] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_ScanAttempt] CHECK (([ActiveScanAttemptId] IS NULL AND [ScanStartedAt] IS NULL AND [IsScanInProgress] = 0) OR ([ActiveScanAttemptId] IS NOT NULL AND [ScanStartedAt] IS NOT NULL AND [ScanStartedAt] >= [UploadedAt]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeCertifications_ActionDocumentId_EmployeeId] ON [hr].[EmployeeCertifications] ([ActionDocumentId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeCertifications_ApprovalRequestId] ON [hr].[EmployeeCertifications] ([ApprovalRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EmployeeCertifications_SupersedesEmployeeCertificationId] ON [hr].[EmployeeCertifications] ([SupersedesEmployeeCertificationId]) WHERE [SupersedesEmployeeCertificationId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeCertifications_SupersedesEmployeeCertificationId_EmployeeId] ON [hr].[EmployeeCertifications] ([SupersedesEmployeeCertificationId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [CK_EmployeeCertifications_Revision] CHECK ([Revision] > 0 AND (([Revision] = 1 AND [SupersedesEmployeeCertificationId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeCertificationId] IS NOT NULL)))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [CK_EmployeeCertifications_Status] CHECK ([Status] BETWEEN 1 AND 3 AND [EffectiveOn] >= [IssuedOn])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'ALTER TABLE [workflow].[ApprovalDelegations] ADD CONSTRAINT [CK_ApprovalDelegations_RevokeReason] CHECK ([Permission] IS NULL OR [RevokedAt] IS NULL OR ([RevocationReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RevocationReason]))) > 0))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'ALTER TABLE [workflow].[ApprovalDelegations] ADD CONSTRAINT [CK_ApprovalDelegations_Scope] CHECK (([Permission] IS NULL AND [ScopeKind] IS NULL AND [TargetId] IS NULL) OR ([Permission] IS NOT NULL AND [ScopeKind] IS NOT NULL AND (([ScopeKind] = 7 AND [TargetId] IS NULL) OR ([ScopeKind] BETWEEN 2 AND 6 AND [TargetId] IS NOT NULL))))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeArchiveChanges_EmployeeId_RecordedAt_Id] ON [hr].[EmployeeArchiveChanges] ([EmployeeId], [RecordedAt], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeAssetHistory_ApprovalRequestId] ON [hr].[EmployeeAssetHistory] ([ApprovalRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeAssetHistory_EmployeeAssetCustodyId_EmployeeId] ON [hr].[EmployeeAssetHistory] ([EmployeeAssetCustodyId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmployeeAssetHistory_EmployeeAssetCustodyId_Revision] ON [hr].[EmployeeAssetHistory] ([EmployeeAssetCustodyId], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeAssetHistory_EmployeeId_RecordedAt] ON [hr].[EmployeeAssetHistory] ([EmployeeId], [RecordedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeAssetHistory_EvidenceDocumentId_EmployeeId] ON [hr].[EmployeeAssetHistory] ([EvidenceDocumentId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeAssetHistory_ReceiverAccountId] ON [hr].[EmployeeAssetHistory] ([ReceiverAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeAssetHistory_TransferredFromCustodyId] ON [hr].[EmployeeAssetHistory] ([TransferredFromCustodyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeBankVerifications_EmployeeBankDetailsId_EmployeeId] ON [hr].[EmployeeBankVerifications] ([EmployeeBankDetailsId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmployeeBankVerifications_EmployeeBankDetailsId_Version] ON [hr].[EmployeeBankVerifications] ([EmployeeBankDetailsId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeBankVerifications_EmployeeId] ON [hr].[EmployeeBankVerifications] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EmployeeBankVerifications_PreviousVerificationId] ON [hr].[EmployeeBankVerifications] ([PreviousVerificationId]) WHERE [PreviousVerificationId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeBankVerifications_PreviousVerificationId_EmployeeId_EmployeeBankDetailsId] ON [hr].[EmployeeBankVerifications] ([PreviousVerificationId], [EmployeeId], [EmployeeBankDetailsId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeDocumentHistory_EmployeeDocumentId_EmployeeId] ON [hr].[EmployeeDocumentHistory] ([EmployeeDocumentId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmployeeDocumentHistory_EmployeeDocumentId_Revision] ON [hr].[EmployeeDocumentHistory] ([EmployeeDocumentId], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    CREATE INDEX [IX_EmployeeDocumentHistory_EmployeeId_RecordedAt] ON [hr].[EmployeeDocumentHistory] ([EmployeeId], [RecordedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [FK_EmployeeCertifications_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [FK_EmployeeCertifications_EmployeeCertifications_SupersedesEmployeeCertificationId_EmployeeId] FOREIGN KEY ([SupersedesEmployeeCertificationId], [EmployeeId]) REFERENCES [hr].[EmployeeCertifications] ([Id], [EmployeeId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeCertifications] ADD CONSTRAINT [FK_EmployeeCertifications_EmployeeDocuments_ActionDocumentId_EmployeeId] FOREIGN KEY ([ActionDocumentId], [EmployeeId]) REFERENCES [hr].[EmployeeDocuments] ([Id], [EmployeeId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[Employees] ADD CONSTRAINT [FK_Employees_EmployeeStatusChanges_LastAppliedStatusChangeId_Id] FOREIGN KEY ([LastAppliedStatusChangeId], [Id]) REFERENCES [hr].[EmployeeStatusChanges] ([Id], [EmployeeId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [FK_EmployeeSimAssignments_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [FK_EmployeeSimAssignments_EmployeeDocuments_IssueDocumentId_EmployeeId] FOREIGN KEY ([IssueDocumentId], [EmployeeId]) REFERENCES [hr].[EmployeeDocuments] ([Id], [EmployeeId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [FK_EmployeeSimAssignments_EmployeeDocuments_ReturnDocumentId_EmployeeId] FOREIGN KEY ([ReturnDocumentId], [EmployeeId]) REFERENCES [hr].[EmployeeDocuments] ([Id], [EmployeeId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [FK_EmployeeSimAssignments_EmployeeSimAssignments_TransferredFromAssignmentId] FOREIGN KEY ([TransferredFromAssignmentId]) REFERENCES [hr].[EmployeeSimAssignments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    ALTER TABLE [hr].[EmployeeSimAssignments] ADD CONSTRAINT [FK_EmployeeSimAssignments_UserAccounts_ReturnedToAccountId] FOREIGN KEY ([ReturnedToAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013121357_CompleteHrFoundationPartA', N'10.0.12');
END;

COMMIT;
GO

