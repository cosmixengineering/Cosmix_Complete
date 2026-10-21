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

BEGIN TRANSACTION;
DECLARE @var7 nvarchar(max);
SELECT @var7 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[AttendancePeriodLockEvents]') AND [c].[name] = N'ApprovalRequestId');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[AttendancePeriodLockEvents] DROP CONSTRAINT ' + @var7 + ';');
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
