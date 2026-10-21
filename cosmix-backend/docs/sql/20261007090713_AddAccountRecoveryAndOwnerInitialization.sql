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
