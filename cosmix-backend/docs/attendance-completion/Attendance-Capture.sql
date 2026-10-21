BEGIN TRANSACTION;
ALTER TABLE [attendance].[FaceVerificationAttempts] DROP CONSTRAINT [CK_FaceVerificationAttempts_Completion];

ALTER TABLE [attendance].[FaceVerificationAttempts] DROP CONSTRAINT [CK_FaceVerificationAttempts_Outcome];

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[WorkSessions]') AND [c].[name] = N'PolicyVersionId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [attendance].[WorkSessions] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [attendance].[WorkSessions] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [BusinessDate] date NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [HolidayCalendarId] uniqueidentifier NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [SiteRosterAssignmentId] uniqueidentifier NULL;

ALTER TABLE [attendance].[RawPunchEvents] ADD [TemporaryShiftChangeId] uniqueidentifier NULL;

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[PunchAssessments]') AND [c].[name] = N'PolicyVersionId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[PunchAssessments] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [attendance].[PunchAssessments] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'ShiftVersionId');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [ShiftVersionId] uniqueidentifier NULL;

DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'PolicyVersionId');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var3 + ';');
ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;

DECLARE @var4 nvarchar(max);
SELECT @var4 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'PayableMinutes');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var4 + ';');
ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [PayableMinutes] int NULL;

DECLARE @var5 nvarchar(max);
SELECT @var5 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'OvertimeMinutes');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var5 + ';');
ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [OvertimeMinutes] int NULL;

DECLARE @var6 nvarchar(max);
SELECT @var6 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'LateMinutes');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var6 + ';');
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

