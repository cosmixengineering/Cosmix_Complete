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

CREATE UNIQUE INDEX [IX_LeaveRequests_EmployeeId_SubmissionKey] ON [hr].[LeaveRequests] ([EmployeeId], [SubmissionKey]) WHERE [SubmissionKey] IS NOT NULL;

CREATE UNIQUE INDEX [IX_LeaveRequests_PreviousRequestId] ON [hr].[LeaveRequests] ([PreviousRequestId]) WHERE [PreviousRequestId] IS NOT NULL;

CREATE INDEX [IX_LeaveRequestDays_Date_LeaveRequestId] ON [hr].[LeaveRequestDays] ([Date], [LeaveRequestId]);

CREATE UNIQUE INDEX [IX_LeaveRequestDays_LeaveRequestId_Date] ON [hr].[LeaveRequestDays] ([LeaveRequestId], [Date]);

CREATE INDEX [IX_LeaveRequestDays_ShiftVersionId] ON [hr].[LeaveRequestDays] ([ShiftVersionId]);

ALTER TABLE [hr].[LeaveRequests] ADD CONSTRAINT [FK_LeaveRequests_LeaveRequests_PreviousRequestId] FOREIGN KEY ([PreviousRequestId]) REFERENCES [hr].[LeaveRequests] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261012125614_AddDirectPoliciesAndAdminLeave', N'10.0.12');

COMMIT;
GO

