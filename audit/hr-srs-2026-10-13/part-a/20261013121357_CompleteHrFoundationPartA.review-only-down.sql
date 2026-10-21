BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM [hr].[EmployeeArchiveChanges])
    OR EXISTS (SELECT 1 FROM [hr].[EmployeeAssetHistory])
    OR EXISTS (SELECT 1 FROM [hr].[EmployeeBankVerifications])
    OR EXISTS (SELECT 1 FROM [hr].[EmployeeDocumentHistory])
    OR EXISTS (SELECT 1 FROM [hr].[Employees] WHERE [IsArchived] = 1)
    OR EXISTS (SELECT 1 FROM [hr].[EmployeeDocuments] WHERE [HistoryRevision] > 0 OR [LifecycleStatus] <> 0 OR [IsScanInProgress] = 1)
    OR EXISTS (SELECT 1 FROM [hr].[EmployeeStatusChanges] WHERE [LifecyclePolicyVersion] IS NOT NULL)
    OR EXISTS (SELECT 1 FROM [hr].[EmployeeCertifications] WHERE [ApprovalRequestId] IS NOT NULL OR [Reason] IS NOT NULL
        OR [Revision] <> 1 OR [Status] <> 1 OR [SupersedesEmployeeCertificationId] IS NOT NULL OR [EffectiveOn] <> [IssuedOn])
    OR EXISTS (SELECT 1 FROM [hr].[EmployeeSimAssignments] WHERE [ApprovalRequestId] IS NOT NULL OR [Reason] IS NOT NULL
        OR [IssueDocumentId] IS NOT NULL OR [ReturnDocumentId] IS NOT NULL OR [TransferredFromAssignmentId] IS NOT NULL
        OR [EffectiveOn] <> COALESCE([ReturnedOn], [IssuedOn]))
    OR EXISTS (SELECT 1 FROM [workflow].[ApprovalDelegations] WHERE [Permission] IS NOT NULL OR [ScopeKind] IS NOT NULL)
    THROW 51023, 'Retained Part A evidence requires a reviewed preservation plan; downgrade refused.', 1;

ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT [FK_EmployeeCertifications_ApprovalRequests_ApprovalRequestId];

ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT [FK_EmployeeCertifications_EmployeeCertifications_SupersedesEmployeeCertificationId_EmployeeId];

ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT [FK_EmployeeCertifications_EmployeeDocuments_ActionDocumentId_EmployeeId];

ALTER TABLE [hr].[Employees] DROP CONSTRAINT [FK_Employees_EmployeeStatusChanges_LastAppliedStatusChangeId_Id];

ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT [FK_EmployeeSimAssignments_ApprovalRequests_ApprovalRequestId];

ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT [FK_EmployeeSimAssignments_EmployeeDocuments_IssueDocumentId_EmployeeId];

ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT [FK_EmployeeSimAssignments_EmployeeDocuments_ReturnDocumentId_EmployeeId];

ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT [FK_EmployeeSimAssignments_EmployeeSimAssignments_TransferredFromAssignmentId];

ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT [FK_EmployeeSimAssignments_UserAccounts_ReturnedToAccountId];

DROP TABLE [hr].[EmployeeArchiveChanges];

DROP TABLE [hr].[EmployeeAssetHistory];

DROP TABLE [hr].[EmployeeBankVerifications];

DROP TABLE [hr].[EmployeeDocumentHistory];

ALTER TABLE [hr].[EmployeeStatusChanges] DROP CONSTRAINT [AK_EmployeeStatusChanges_Id_EmployeeId];

DROP INDEX [IX_EmployeeSimAssignments_ApprovalRequestId] ON [hr].[EmployeeSimAssignments];

DROP INDEX [IX_EmployeeSimAssignments_IssueDocumentId_EmployeeId] ON [hr].[EmployeeSimAssignments];

DROP INDEX [IX_EmployeeSimAssignments_PhoneNumber_EffectiveOn] ON [hr].[EmployeeSimAssignments];

DROP INDEX [IX_EmployeeSimAssignments_ReturnDocumentId_EmployeeId] ON [hr].[EmployeeSimAssignments];

DROP INDEX [IX_EmployeeSimAssignments_ReturnedToAccountId] ON [hr].[EmployeeSimAssignments];

DROP INDEX [IX_EmployeeSimAssignments_SimReference_EffectiveOn] ON [hr].[EmployeeSimAssignments];

DROP INDEX [IX_EmployeeSimAssignments_TransferredFromAssignmentId] ON [hr].[EmployeeSimAssignments];

ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT [CK_EmployeeSimAssignments_Effective];

ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT [CK_EmployeeSimAssignments_ReturnEvidence];

DROP INDEX [IX_Employees_IsArchived_CreatedAt_Id] ON [hr].[Employees];

DROP INDEX [IX_Employees_LastAppliedStatusChangeId_Id] ON [hr].[Employees];

ALTER TABLE [hr].[Employees] DROP CONSTRAINT [CK_Employees_Archive];

ALTER TABLE [hr].[Employees] DROP CONSTRAINT [CK_Employees_ArchiveUtc];

ALTER TABLE [hr].[EmployeeDocuments] DROP CONSTRAINT [CK_EmployeeDocuments_HistoryRevision];

ALTER TABLE [hr].[EmployeeDocuments] DROP CONSTRAINT [CK_EmployeeDocuments_Lifecycle];

ALTER TABLE [hr].[EmployeeDocuments] DROP CONSTRAINT [CK_EmployeeDocuments_ScanAttempt];

ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT [AK_EmployeeCertifications_Id_EmployeeId];

DROP INDEX [IX_EmployeeCertifications_ActionDocumentId_EmployeeId] ON [hr].[EmployeeCertifications];

DROP INDEX [IX_EmployeeCertifications_ApprovalRequestId] ON [hr].[EmployeeCertifications];

DROP INDEX [IX_EmployeeCertifications_SupersedesEmployeeCertificationId] ON [hr].[EmployeeCertifications];

DROP INDEX [IX_EmployeeCertifications_SupersedesEmployeeCertificationId_EmployeeId] ON [hr].[EmployeeCertifications];

ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT [CK_EmployeeCertifications_Revision];

ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT [CK_EmployeeCertifications_Status];

ALTER TABLE [hr].[EmployeeAssetCustodies] DROP CONSTRAINT [AK_EmployeeAssetCustodies_Id_EmployeeId];

ALTER TABLE [workflow].[ApprovalDelegations] DROP CONSTRAINT [CK_ApprovalDelegations_RevokeReason];

ALTER TABLE [workflow].[ApprovalDelegations] DROP CONSTRAINT [CK_ApprovalDelegations_Scope];

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeStatusChanges]') AND [c].[name] = N'LifecyclePolicyVersion');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeStatusChanges] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [hr].[EmployeeStatusChanges] DROP COLUMN [LifecyclePolicyVersion];

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeSimAssignments]') AND [c].[name] = N'ApprovalRequestId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [hr].[EmployeeSimAssignments] DROP COLUMN [ApprovalRequestId];

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeSimAssignments]') AND [c].[name] = N'EffectiveOn');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [hr].[EmployeeSimAssignments] DROP COLUMN [EffectiveOn];

DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeSimAssignments]') AND [c].[name] = N'IssueDocumentId');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT ' + @var3 + ';');
ALTER TABLE [hr].[EmployeeSimAssignments] DROP COLUMN [IssueDocumentId];

DECLARE @var4 nvarchar(max);
SELECT @var4 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeSimAssignments]') AND [c].[name] = N'Reason');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT ' + @var4 + ';');
ALTER TABLE [hr].[EmployeeSimAssignments] DROP COLUMN [Reason];

DECLARE @var5 nvarchar(max);
SELECT @var5 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeSimAssignments]') AND [c].[name] = N'ReturnDocumentId');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT ' + @var5 + ';');
ALTER TABLE [hr].[EmployeeSimAssignments] DROP COLUMN [ReturnDocumentId];

DECLARE @var6 nvarchar(max);
SELECT @var6 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeSimAssignments]') AND [c].[name] = N'ReturnedToAccountId');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT ' + @var6 + ';');
ALTER TABLE [hr].[EmployeeSimAssignments] DROP COLUMN [ReturnedToAccountId];

DECLARE @var7 nvarchar(max);
SELECT @var7 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeSimAssignments]') AND [c].[name] = N'TransferredFromAssignmentId');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeSimAssignments] DROP CONSTRAINT ' + @var7 + ';');
ALTER TABLE [hr].[EmployeeSimAssignments] DROP COLUMN [TransferredFromAssignmentId];

DECLARE @var8 nvarchar(max);
SELECT @var8 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[Employees]') AND [c].[name] = N'ArchivedAt');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [hr].[Employees] DROP CONSTRAINT ' + @var8 + ';');
ALTER TABLE [hr].[Employees] DROP COLUMN [ArchivedAt];

DECLARE @var9 nvarchar(max);
SELECT @var9 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[Employees]') AND [c].[name] = N'IsArchived');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [hr].[Employees] DROP CONSTRAINT ' + @var9 + ';');
ALTER TABLE [hr].[Employees] DROP COLUMN [IsArchived];

DECLARE @var10 nvarchar(max);
SELECT @var10 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[Employees]') AND [c].[name] = N'LastAppliedStatusChangeId');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [hr].[Employees] DROP CONSTRAINT ' + @var10 + ';');
ALTER TABLE [hr].[Employees] DROP COLUMN [LastAppliedStatusChangeId];

DECLARE @var11 nvarchar(max);
SELECT @var11 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeDocuments]') AND [c].[name] = N'ActiveScanAttemptId');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeDocuments] DROP CONSTRAINT ' + @var11 + ';');
ALTER TABLE [hr].[EmployeeDocuments] DROP COLUMN [ActiveScanAttemptId];

DECLARE @var12 nvarchar(max);
SELECT @var12 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeDocuments]') AND [c].[name] = N'HistoryRevision');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeDocuments] DROP CONSTRAINT ' + @var12 + ';');
ALTER TABLE [hr].[EmployeeDocuments] DROP COLUMN [HistoryRevision];

DECLARE @var13 nvarchar(max);
SELECT @var13 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeDocuments]') AND [c].[name] = N'IsScanInProgress');
IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeDocuments] DROP CONSTRAINT ' + @var13 + ';');
ALTER TABLE [hr].[EmployeeDocuments] DROP COLUMN [IsScanInProgress];

DECLARE @var14 nvarchar(max);
SELECT @var14 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeDocuments]') AND [c].[name] = N'LifecycleChangedAt');
IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeDocuments] DROP CONSTRAINT ' + @var14 + ';');
ALTER TABLE [hr].[EmployeeDocuments] DROP COLUMN [LifecycleChangedAt];

DECLARE @var15 nvarchar(max);
SELECT @var15 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeDocuments]') AND [c].[name] = N'LifecycleChangedByUserId');
IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeDocuments] DROP CONSTRAINT ' + @var15 + ';');
ALTER TABLE [hr].[EmployeeDocuments] DROP COLUMN [LifecycleChangedByUserId];

DECLARE @var16 nvarchar(max);
SELECT @var16 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeDocuments]') AND [c].[name] = N'LifecycleReason');
IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeDocuments] DROP CONSTRAINT ' + @var16 + ';');
ALTER TABLE [hr].[EmployeeDocuments] DROP COLUMN [LifecycleReason];

DECLARE @var17 nvarchar(max);
SELECT @var17 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeDocuments]') AND [c].[name] = N'LifecycleStatus');
IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeDocuments] DROP CONSTRAINT ' + @var17 + ';');
ALTER TABLE [hr].[EmployeeDocuments] DROP COLUMN [LifecycleStatus];

DECLARE @var18 nvarchar(max);
SELECT @var18 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeDocuments]') AND [c].[name] = N'ScanStartedAt');
IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeDocuments] DROP CONSTRAINT ' + @var18 + ';');
ALTER TABLE [hr].[EmployeeDocuments] DROP COLUMN [ScanStartedAt];

DECLARE @var19 nvarchar(max);
SELECT @var19 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeCertifications]') AND [c].[name] = N'ActionDocumentId');
IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT ' + @var19 + ';');
ALTER TABLE [hr].[EmployeeCertifications] DROP COLUMN [ActionDocumentId];

DECLARE @var20 nvarchar(max);
SELECT @var20 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeCertifications]') AND [c].[name] = N'ApprovalRequestId');
IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT ' + @var20 + ';');
ALTER TABLE [hr].[EmployeeCertifications] DROP COLUMN [ApprovalRequestId];

DECLARE @var21 nvarchar(max);
SELECT @var21 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeCertifications]') AND [c].[name] = N'EffectiveOn');
IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT ' + @var21 + ';');
ALTER TABLE [hr].[EmployeeCertifications] DROP COLUMN [EffectiveOn];

DECLARE @var22 nvarchar(max);
SELECT @var22 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeCertifications]') AND [c].[name] = N'Reason');
IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT ' + @var22 + ';');
ALTER TABLE [hr].[EmployeeCertifications] DROP COLUMN [Reason];

DECLARE @var23 nvarchar(max);
SELECT @var23 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeCertifications]') AND [c].[name] = N'Revision');
IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT ' + @var23 + ';');
ALTER TABLE [hr].[EmployeeCertifications] DROP COLUMN [Revision];

DECLARE @var24 nvarchar(max);
SELECT @var24 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeCertifications]') AND [c].[name] = N'Status');
IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT ' + @var24 + ';');
ALTER TABLE [hr].[EmployeeCertifications] DROP COLUMN [Status];

DECLARE @var25 nvarchar(max);
SELECT @var25 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeCertifications]') AND [c].[name] = N'SupersedesEmployeeCertificationId');
IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeCertifications] DROP CONSTRAINT ' + @var25 + ';');
ALTER TABLE [hr].[EmployeeCertifications] DROP COLUMN [SupersedesEmployeeCertificationId];

DECLARE @var26 nvarchar(max);
SELECT @var26 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[workflow].[ApprovalDelegations]') AND [c].[name] = N'Permission');
IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [workflow].[ApprovalDelegations] DROP CONSTRAINT ' + @var26 + ';');
ALTER TABLE [workflow].[ApprovalDelegations] DROP COLUMN [Permission];

DECLARE @var27 nvarchar(max);
SELECT @var27 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[workflow].[ApprovalDelegations]') AND [c].[name] = N'RevocationReason');
IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [workflow].[ApprovalDelegations] DROP CONSTRAINT ' + @var27 + ';');
ALTER TABLE [workflow].[ApprovalDelegations] DROP COLUMN [RevocationReason];

DECLARE @var28 nvarchar(max);
SELECT @var28 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[workflow].[ApprovalDelegations]') AND [c].[name] = N'ScopeKind');
IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [workflow].[ApprovalDelegations] DROP CONSTRAINT ' + @var28 + ';');
ALTER TABLE [workflow].[ApprovalDelegations] DROP COLUMN [ScopeKind];

DECLARE @var29 nvarchar(max);
SELECT @var29 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[workflow].[ApprovalDelegations]') AND [c].[name] = N'TargetId');
IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [workflow].[ApprovalDelegations] DROP CONSTRAINT ' + @var29 + ';');
ALTER TABLE [workflow].[ApprovalDelegations] DROP COLUMN [TargetId];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261013121357_CompleteHrFoundationPartA';

COMMIT;
GO

