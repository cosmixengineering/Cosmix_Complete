BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[TemporaryShiftChanges]') AND [c].[name] = N'ApprovalRequestId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [attendance].[TemporaryShiftChanges] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [attendance].[TemporaryShiftChanges] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ShiftPopulationAssignments]') AND [c].[name] = N'ApprovalRequestId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ShiftPopulationAssignments] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [attendance].[ShiftPopulationAssignments] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments'
)
BEGIN
    ALTER TABLE [attendance].[ShiftPopulationAssignments] ADD [HolidayCalendarId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ShiftPopulationAssignmentEnds]') AND [c].[name] = N'ApprovalRequestId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ShiftPopulationAssignmentEnds] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [attendance].[ShiftPopulationAssignmentEnds] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments'
)
BEGIN
    CREATE TABLE [attendance].[TemporaryShiftChangeAmendments] (
        [Id] uniqueidentifier NOT NULL,
        [OriginalChangeId] uniqueidentifier NOT NULL,
        [ReplacementChangeId] uniqueidentifier NULL,
        [RecordedBy] nvarchar(200) NOT NULL,
        [RecordedAtUtc] datetimeoffset(7) NOT NULL,
        [WrittenEvidenceReference] nvarchar(500) NOT NULL,
        [Reason] nvarchar(2000) NOT NULL,
        CONSTRAINT [PK_TemporaryShiftChangeAmendments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TemporaryShiftChangeAmendments_DifferentChanges] CHECK ([ReplacementChangeId] IS NULL OR [OriginalChangeId] <> [ReplacementChangeId]),
        CONSTRAINT [CK_TemporaryShiftChangeAmendments_Evidence] CHECK (LEN(LTRIM(RTRIM([RecordedBy]))) > 0 AND LEN(LTRIM(RTRIM([WrittenEvidenceReference]))) > 0 AND LEN(LTRIM(RTRIM([Reason]))) > 0),
        CONSTRAINT [CK_TemporaryShiftChangeAmendments_RecordedAtUtc_Utc] CHECK ([RecordedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RecordedAtUtc]) = 0),
        CONSTRAINT [FK_TemporaryShiftChangeAmendments_TemporaryShiftChanges_OriginalChangeId] FOREIGN KEY ([OriginalChangeId]) REFERENCES [attendance].[TemporaryShiftChanges] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TemporaryShiftChangeAmendments_TemporaryShiftChanges_ReplacementChangeId] FOREIGN KEY ([ReplacementChangeId]) REFERENCES [attendance].[TemporaryShiftChanges] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments'
)
BEGIN
    CREATE INDEX [IX_ShiftPopulationAssignments_HolidayCalendarId] ON [attendance].[ShiftPopulationAssignments] ([HolidayCalendarId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TemporaryShiftChangeAmendments_OriginalChangeId] ON [attendance].[TemporaryShiftChangeAmendments] ([OriginalChangeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TemporaryShiftChangeAmendments_ReplacementChangeId] ON [attendance].[TemporaryShiftChangeAmendments] ([ReplacementChangeId]) WHERE [ReplacementChangeId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments'
)
BEGIN
    ALTER TABLE [attendance].[ShiftPopulationAssignments] ADD CONSTRAINT [FK_ShiftPopulationAssignments_HolidayCalendars_HolidayCalendarId] FOREIGN KEY ([HolidayCalendarId]) REFERENCES [hr].[HolidayCalendars] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    ALTER TABLE [attendance].[FaceVerificationAttempts] DROP CONSTRAINT [CK_FaceVerificationAttempts_Completion];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    ALTER TABLE [attendance].[FaceVerificationAttempts] DROP CONSTRAINT [CK_FaceVerificationAttempts_Outcome];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[WorkSessions]') AND [c].[name] = N'PolicyVersionId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[WorkSessions] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [attendance].[WorkSessions] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD [BusinessDate] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD [HolidayCalendarId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD [SiteRosterAssignmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD [TemporaryShiftChangeId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[PunchAssessments]') AND [c].[name] = N'PolicyVersionId');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[PunchAssessments] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [attendance].[PunchAssessments] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'ShiftVersionId');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [ShiftVersionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'PolicyVersionId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'PayableMinutes');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [PayableMinutes] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'OvertimeMinutes');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [OvertimeMinutes] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DailyAttendanceSummaries]') AND [c].[name] = N'LateMinutes');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [attendance].[DailyAttendanceSummaries] ALTER COLUMN [LateMinutes] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    ALTER TABLE [attendance].[DailyAttendanceSummaries] ADD [InputEvidenceReference] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    CREATE INDEX [IX_RawPunchEvents_HolidayCalendarId] ON [attendance].[RawPunchEvents] ([HolidayCalendarId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    CREATE INDEX [IX_RawPunchEvents_SiteRosterAssignmentId] ON [attendance].[RawPunchEvents] ([SiteRosterAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    CREATE INDEX [IX_RawPunchEvents_TemporaryShiftChangeId] ON [attendance].[RawPunchEvents] ([TemporaryShiftChangeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[FaceVerificationAttempts] ADD CONSTRAINT [CK_FaceVerificationAttempts_Completion] CHECK (([Outcome] = 1 AND [CompletedAtUtc] IS NULL AND [LivenessPassed] IS NULL AND [MatchPassed] IS NULL AND [ResultReasonCode] IS NULL) OR ([Outcome] BETWEEN 2 AND 4 AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [IssuedAtUtc] AND [CompletedAtUtc] < [ExpiresAtUtc] AND [LivenessPassed] IS NOT NULL AND [MatchPassed] IS NOT NULL AND [ResultReasonCode] IS NOT NULL) OR ([Outcome] = 5 AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [ExpiresAtUtc] AND [LivenessPassed] IS NOT NULL AND [MatchPassed] IS NOT NULL AND [LivenessPassed] = 0 AND [MatchPassed] = 0 AND [ResultReasonCode] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[FaceVerificationAttempts] ADD CONSTRAINT [CK_FaceVerificationAttempts_Outcome] CHECK ([Outcome] BETWEEN 1 AND 5)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[DailyAttendanceSummaries] ADD CONSTRAINT [CK_DailyAttendanceSummaries_PolicyOutputs] CHECK (([PolicyVersionId] IS NULL AND [PayableMinutes] IS NULL AND [LateMinutes] IS NULL AND [OvertimeMinutes] IS NULL AND [Status] IN (1,9)) OR ([PolicyVersionId] IS NOT NULL AND [PayableMinutes] IS NOT NULL AND [LateMinutes] IS NOT NULL AND [OvertimeMinutes] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_HolidayCalendars_HolidayCalendarId] FOREIGN KEY ([HolidayCalendarId]) REFERENCES [hr].[HolidayCalendars] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_ShiftPopulationAssignments_SiteRosterAssignmentId] FOREIGN KEY ([SiteRosterAssignmentId]) REFERENCES [attendance].[ShiftPopulationAssignments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_TemporaryShiftChanges_TemporaryShiftChangeId] FOREIGN KEY ([TemporaryShiftChangeId]) REFERENCES [attendance].[TemporaryShiftChanges] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009083807_AddAttendanceCaptureAndFactualSummaries'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009083807_AddAttendanceCaptureAndFactualSummaries', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[AttendancePeriodLockEvents]') AND [c].[name] = N'ApprovalRequestId');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[AttendancePeriodLockEvents] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [attendance].[AttendancePeriodLockEvents] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    ALTER TABLE [attendance].[AttendancePeriodLockEvents] ADD [ProtectedEvidenceReference] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[AttendancePeriodLockEvents] ADD CONSTRAINT [CK_AttendancePeriodLockEvents_Provenance] CHECK (([ApprovalRequestId] IS NOT NULL AND [ProtectedEvidenceReference] IS NULL) OR ([ApprovalRequestId] IS NULL AND [ProtectedEvidenceReference] IS NOT NULL AND [IsLocked] = 1));');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AttendanceCarryForwardAdjustments_ApprovalDecisionId] ON [attendance].[AttendanceCarryForwardAdjustments] ([ApprovalDecisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AttendanceCarryForwardAdjustments_CorrectionId] ON [attendance].[AttendanceCarryForwardAdjustments] ([CorrectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    CREATE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_CorrectionId] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [CorrectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    CREATE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_FirstStartsAtUtc_LastEndsAtUtc] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [FirstStartsAtUtc], [LastEndsAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_Revision] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [SourceBusinessDate], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    CREATE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_SourceSummaryId] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [SourceBusinessDate], [SourceSummaryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    CREATE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_SupersedesAdjustmentId] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [SourceBusinessDate], [SupersedesAdjustmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    CREATE INDEX [IX_AttendanceCarryForwardAdjustments_EmployeeId_TargetMonthStart] ON [attendance].[AttendanceCarryForwardAdjustments] ([EmployeeId], [TargetMonthStart]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    CREATE INDEX [IX_AttendanceCarryForwardAdjustments_SourcePeriodLockEventId] ON [attendance].[AttendanceCarryForwardAdjustments] ([SourcePeriodLockEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneVersions]') AND [c].[name] = N'ApprovalRequestId');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneVersions] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [attendance].[ZoneVersions] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    ALTER TABLE [attendance].[ZoneVersions] ADD [DirectAuthorityEvidenceReference] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneEvents]') AND [c].[name] = N'Longitude');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneEvents] DROP CONSTRAINT ' + @var12 + ';');
    ALTER TABLE [attendance].[ZoneEvents] ALTER COLUMN [Longitude] decimal(10,7) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    DECLARE @var13 nvarchar(max);
    SELECT @var13 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneEvents]') AND [c].[name] = N'Latitude');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneEvents] DROP CONSTRAINT ' + @var13 + ';');
    ALTER TABLE [attendance].[ZoneEvents] ALTER COLUMN [Latitude] decimal(10,7) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    DECLARE @var14 nvarchar(max);
    SELECT @var14 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneEvents]') AND [c].[name] = N'AccuracyMeters');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneEvents] DROP CONSTRAINT ' + @var14 + ';');
    ALTER TABLE [attendance].[ZoneEvents] ALTER COLUMN [AccuracyMeters] decimal(10,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    ALTER TABLE [attendance].[ZoneEvents] ADD [LocationExpiresAtUtc] datetimeoffset(7) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    ALTER TABLE [attendance].[ZoneEvents] ADD [LocationPrivateReference] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    DECLARE @var15 nvarchar(max);
    SELECT @var15 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneAssignments]') AND [c].[name] = N'ApprovalRequestId');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneAssignments] DROP CONSTRAINT ' + @var15 + ';');
    ALTER TABLE [attendance].[ZoneAssignments] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    ALTER TABLE [attendance].[ZoneAssignments] ADD [DirectAuthorityEvidenceReference] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    DECLARE @var16 nvarchar(max);
    SELECT @var16 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[OvertimeRequests]') AND [c].[name] = N'RateRuleCode');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[OvertimeRequests] DROP CONSTRAINT ' + @var16 + ';');
    ALTER TABLE [attendance].[OvertimeRequests] ALTER COLUMN [RateRuleCode] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    DECLARE @var17 nvarchar(max);
    SELECT @var17 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[OvertimeRequests]') AND [c].[name] = N'PolicyVersionId');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[OvertimeRequests] DROP CONSTRAINT ' + @var17 + ';');
    ALTER TABLE [attendance].[OvertimeRequests] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    DECLARE @var18 nvarchar(max);
    SELECT @var18 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DutyMonitoringWindows]') AND [c].[name] = N'RetentionPolicyVersionId');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DutyMonitoringWindows] DROP CONSTRAINT ' + @var18 + ';');
    ALTER TABLE [attendance].[DutyMonitoringWindows] ALTER COLUMN [RetentionPolicyVersionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [AcceptedCheckInId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [ConfigurationVersionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [RegisteredDeviceId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [WorkSessionKey] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE TABLE [attendance].[OvertimeEligibilityVersions] (
        [Id] uniqueidentifier NOT NULL,
        [Target] int NOT NULL,
        [EmployeeId] uniqueidentifier NULL,
        [Revision] int NOT NULL,
        [SupersedesVersionId] uniqueidentifier NULL,
        [EffectiveFrom] date NOT NULL,
        [IsEnabled] bit NOT NULL,
        [RecordedBy] nvarchar(200) NOT NULL,
        [RecordedAtUtc] datetimeoffset(7) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [ProtectedEvidenceReference] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_OvertimeEligibilityVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OvertimeEligibilityVersions_RecordedAtUtc_Utc] CHECK ([RecordedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RecordedAtUtc]) = 0),
        CONSTRAINT [CK_OvertimeEligibilityVersions_Revision] CHECK (([Revision] = 1 AND [SupersedesVersionId] IS NULL) OR ([Revision] > 1 AND [SupersedesVersionId] IS NOT NULL)),
        CONSTRAINT [CK_OvertimeEligibilityVersions_Target] CHECK (([Target] = 1 AND [EmployeeId] IS NULL) OR ([Target] = 2 AND [EmployeeId] IS NOT NULL)),
        CONSTRAINT [FK_OvertimeEligibilityVersions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OvertimeEligibilityVersions_OvertimeEligibilityVersions_SupersedesVersionId] FOREIGN KEY ([SupersedesVersionId]) REFERENCES [attendance].[OvertimeEligibilityVersions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE TABLE [attendance].[ZoneAssignmentEnds] (
        [Id] uniqueidentifier NOT NULL,
        [ZoneAssignmentId] uniqueidentifier NOT NULL,
        [EffectiveToUtc] datetimeoffset(7) NOT NULL,
        [Actor] nvarchar(200) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [RecordedAtUtc] datetimeoffset(7) NOT NULL,
        [AuthorityEvidenceReference] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_ZoneAssignmentEnds] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ZoneAssignmentEnds_EffectiveToUtc_Utc] CHECK ([EffectiveToUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveToUtc]) = 0),
        CONSTRAINT [CK_ZoneAssignmentEnds_Future] CHECK ([EffectiveToUtc] >= [RecordedAtUtc]),
        CONSTRAINT [CK_ZoneAssignmentEnds_RecordedAtUtc_Utc] CHECK ([RecordedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RecordedAtUtc]) = 0),
        CONSTRAINT [FK_ZoneAssignmentEnds_ZoneAssignments_ZoneAssignmentId] FOREIGN KEY ([ZoneAssignmentId]) REFERENCES [attendance].[ZoneAssignments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE TABLE [attendance].[ZoneLocationDeletions] (
        [Id] uniqueidentifier NOT NULL,
        [ZoneEventId] uniqueidentifier NOT NULL,
        [ExpiresAtUtc] datetimeoffset(7) NOT NULL,
        [DeletedAtUtc] datetimeoffset(7) NOT NULL,
        [ReferenceSha256] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_ZoneLocationDeletions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ZoneLocationDeletions_DeletedAtUtc_Utc] CHECK ([DeletedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeletedAtUtc]) = 0),
        CONSTRAINT [CK_ZoneLocationDeletions_ExpiresAtUtc_Utc] CHECK ([ExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [ExpiresAtUtc]) = 0),
        CONSTRAINT [CK_ZoneLocationDeletions_Expiry] CHECK ([DeletedAtUtc] >= [ExpiresAtUtc]),
        CONSTRAINT [FK_ZoneLocationDeletions_ZoneEvents_ZoneEventId] FOREIGN KEY ([ZoneEventId]) REFERENCES [attendance].[ZoneEvents] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE TABLE [attendance].[ZoneMonitoringCheckpoints] (
        [Id] uniqueidentifier NOT NULL,
        [DutyMonitoringWindowId] uniqueidentifier NOT NULL,
        [LastReceivedAtUtc] datetimeoffset(7) NULL,
        [LastPosition] int NULL,
        [LocationId] uniqueidentifier NULL,
        [LocationPrivateReference] nvarchar(500) NULL,
        [LocationExpiresAtUtc] datetimeoffset(7) NULL,
        [LiveVisibleUntilUtc] datetimeoffset(7) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ZoneMonitoringCheckpoints] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ZoneMonitoringCheckpoints_LastReceivedAtUtc_Utc] CHECK ([LastReceivedAtUtc] IS NULL OR DATEPART(TZOFFSET, [LastReceivedAtUtc]) = 0),
        CONSTRAINT [CK_ZoneMonitoringCheckpoints_LiveVisibleUntilUtc_Utc] CHECK ([LiveVisibleUntilUtc] IS NULL OR DATEPART(TZOFFSET, [LiveVisibleUntilUtc]) = 0),
        CONSTRAINT [CK_ZoneMonitoringCheckpoints_LocationExpiresAtUtc_Utc] CHECK ([LocationExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [LocationExpiresAtUtc]) = 0),
        CONSTRAINT [CK_ZoneMonitoringCheckpoints_Position] CHECK (([LastPosition] IS NULL AND [LastReceivedAtUtc] IS NULL AND [LocationId] IS NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [LiveVisibleUntilUtc] IS NULL) OR ([LastPosition] IS NOT NULL AND [LastPosition] BETWEEN 1 AND 3 AND [LastReceivedAtUtc] IS NOT NULL AND [LocationId] IS NOT NULL AND [LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LiveVisibleUntilUtc] IS NOT NULL AND [LiveVisibleUntilUtc] > [LastReceivedAtUtc] AND [LocationExpiresAtUtc] >= [LiveVisibleUntilUtc])),
        CONSTRAINT [FK_ZoneMonitoringCheckpoints_DutyMonitoringWindows_DutyMonitoringWindowId] FOREIGN KEY ([DutyMonitoringWindowId]) REFERENCES [attendance].[DutyMonitoringWindows] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE TABLE [attendance].[ZoneMonitoringConfigurationVersions] (
        [Id] uniqueidentifier NOT NULL,
        [SiteId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [EffectiveFromUtc] datetimeoffset(7) NOT NULL,
        [EffectiveToUtc] datetimeoffset(7) NOT NULL,
        [MaximumAccuracyMeters] decimal(10,2) NOT NULL,
        [WarningDistanceMeters] decimal(10,2) NOT NULL,
        [MinimumIntervalSeconds] int NOT NULL,
        [RetentionDays] int NOT NULL,
        [MaximumMonitoringSeconds] int NOT NULL,
        [LiveVisibilitySeconds] int NOT NULL,
        [VisibleNotice] nvarchar(2000) NOT NULL,
        [ApprovedBy] nvarchar(200) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [ApprovedAtUtc] datetimeoffset(7) NOT NULL,
        [AuthorityEvidenceReference] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_ZoneMonitoringConfigurationVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ZoneMonitoringConfigurationVersions_ApprovedAtUtc_Utc] CHECK ([ApprovedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ApprovedAtUtc]) = 0),
        CONSTRAINT [CK_ZoneMonitoringConfigurationVersions_EffectiveFromUtc_Utc] CHECK ([EffectiveFromUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveFromUtc]) = 0),
        CONSTRAINT [CK_ZoneMonitoringConfigurationVersions_EffectiveToUtc_Utc] CHECK ([EffectiveToUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveToUtc]) = 0),
        CONSTRAINT [CK_ZoneMonitoringConfigurationVersions_Values] CHECK ([VersionNumber] > 0 AND [MaximumAccuracyMeters] > 0 AND [MaximumAccuracyMeters] <= 100000 AND [WarningDistanceMeters] BETWEEN 0 AND 100000 AND [MinimumIntervalSeconds] BETWEEN 1 AND 86400 AND [RetentionDays] BETWEEN 1 AND 3650 AND [MaximumMonitoringSeconds] BETWEEN 1 AND 172800 AND [MinimumIntervalSeconds] <= [MaximumMonitoringSeconds] AND [LiveVisibilitySeconds] BETWEEN 1 AND 86400 AND [LiveVisibilitySeconds] >= [MinimumIntervalSeconds] AND [LiveVisibilitySeconds] <= [MaximumMonitoringSeconds]),
        CONSTRAINT [CK_ZoneMonitoringConfigurationVersions_Window] CHECK ([EffectiveToUtc] > [EffectiveFromUtc]),
        CONSTRAINT [FK_ZoneMonitoringConfigurationVersions_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [attendance].[Sites] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[ZoneVersions] ADD CONSTRAINT [CK_ZoneVersions_Authority] CHECK ([ApprovalRequestId] IS NOT NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE INDEX [IX_ZoneEvents_LocationExpiresAtUtc_Id] ON [attendance].[ZoneEvents] ([LocationExpiresAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_LocationExpiresAtUtc_Utc] CHECK ([LocationExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [LocationExpiresAtUtc]) = 0);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_LocationRetention] CHECK (([LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [AccuracyMeters] IS NOT NULL) OR ([LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LocationExpiresAtUtc] > [ReceivedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL));');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[ZoneAssignments] ADD CONSTRAINT [CK_ZoneAssignments_Authority] CHECK ([ApprovalRequestId] IS NOT NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE INDEX [IX_DutyMonitoringWindows_AcceptedCheckInId] ON [attendance].[DutyMonitoringWindows] ([AcceptedCheckInId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE INDEX [IX_DutyMonitoringWindows_ConfigurationVersionId] ON [attendance].[DutyMonitoringWindows] ([ConfigurationVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE INDEX [IX_DutyMonitoringWindows_RegisteredDeviceId] ON [attendance].[DutyMonitoringWindows] ([RegisteredDeviceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [CK_DutyMonitoringWindows_Provenance] CHECK (([RetentionPolicyVersionId] IS NOT NULL AND [ConfigurationVersionId] IS NULL AND [AcceptedCheckInId] IS NULL AND [WorkSessionKey] IS NULL AND [RegisteredDeviceId] IS NULL) OR ([RetentionPolicyVersionId] IS NULL AND [ConfigurationVersionId] IS NOT NULL AND [AcceptedCheckInId] IS NOT NULL AND [WorkSessionKey] IS NOT NULL AND [RegisteredDeviceId] IS NOT NULL));');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE INDEX [IX_OvertimeEligibilityVersions_EmployeeId] ON [attendance].[OvertimeEligibilityVersions] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE INDEX [IX_OvertimeEligibilityVersions_SupersedesVersionId] ON [attendance].[OvertimeEligibilityVersions] ([SupersedesVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE INDEX [IX_OvertimeEligibilityVersions_Target_EmployeeId_EffectiveFrom_Revision] ON [attendance].[OvertimeEligibilityVersions] ([Target], [EmployeeId], [EffectiveFrom], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OvertimeEligibilityVersions_Target_EmployeeId_Revision] ON [attendance].[OvertimeEligibilityVersions] ([Target], [EmployeeId], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ZoneAssignmentEnds_ZoneAssignmentId] ON [attendance].[ZoneAssignmentEnds] ([ZoneAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ZoneLocationDeletions_ZoneEventId] ON [attendance].[ZoneLocationDeletions] ([ZoneEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ZoneMonitoringCheckpoints_DutyMonitoringWindowId] ON [attendance].[ZoneMonitoringCheckpoints] ([DutyMonitoringWindowId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE INDEX [IX_ZoneMonitoringConfigurationVersions_SiteId_EffectiveFromUtc] ON [attendance].[ZoneMonitoringConfigurationVersions] ([SiteId], [EffectiveFromUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ZoneMonitoringConfigurationVersions_SiteId_VersionNumber] ON [attendance].[ZoneMonitoringConfigurationVersions] ([SiteId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [FK_DutyMonitoringWindows_RawPunchEvents_AcceptedCheckInId] FOREIGN KEY ([AcceptedCheckInId]) REFERENCES [attendance].[RawPunchEvents] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [FK_DutyMonitoringWindows_RegisteredDevices_RegisteredDeviceId] FOREIGN KEY ([RegisteredDeviceId]) REFERENCES [auth].[RegisteredDevices] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [FK_DutyMonitoringWindows_ZoneMonitoringConfigurationVersions_ConfigurationVersionId] FOREIGN KEY ([ConfigurationVersionId]) REFERENCES [attendance].[ZoneMonitoringConfigurationVersions] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012081503_AddOvertimeEligibilityAndDutyZones'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012081503_AddOvertimeEligibilityAndDutyZones', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012082833_RetainZoneWarningTransitions'
)
BEGIN
    ALTER TABLE [attendance].[ZoneMonitoringCheckpoints] ADD [LastWarning] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012082833_RetainZoneWarningTransitions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012082833_RetainZoneWarningTransitions', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114635_AddSiteReportResponsesAndShiftReminders'
)
BEGIN
    ALTER TABLE [attendance].[ShiftVersions] ADD [MissingCheckoutReminderAfterMinutes] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114635_AddSiteReportResponsesAndShiftReminders'
)
BEGIN
    ALTER TABLE [attendance].[SiteReports] ADD CONSTRAINT [AK_SiteReports_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114635_AddSiteReportResponsesAndShiftReminders'
)
BEGIN
    ALTER TABLE [attendance].[SiteReportReviews] ADD CONSTRAINT [AK_SiteReportReviews_SiteReportId_Id] UNIQUE ([SiteReportId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114635_AddSiteReportResponsesAndShiftReminders'
)
BEGIN
    CREATE TABLE [attendance].[SiteReportResponses] (
        [Id] uniqueidentifier NOT NULL,
        [SiteReportId] uniqueidentifier NOT NULL,
        [Sequence] int NOT NULL,
        [ReturnedReviewId] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [SubmittedByAccountId] uniqueidentifier NOT NULL,
        [SubmittedAtUtc] datetimeoffset(7) NOT NULL,
        [PrivateEvidenceReference] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_SiteReportResponses] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SiteReportResponses_Sequence] CHECK ([Sequence] > 0),
        CONSTRAINT [CK_SiteReportResponses_SubmittedAtUtc_Utc] CHECK ([SubmittedAtUtc] IS NULL OR DATEPART(TZOFFSET, [SubmittedAtUtc]) = 0),
        CONSTRAINT [FK_SiteReportResponses_SiteReportReviews_SiteReportId_ReturnedReviewId] FOREIGN KEY ([SiteReportId], [ReturnedReviewId]) REFERENCES [attendance].[SiteReportReviews] ([SiteReportId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SiteReportResponses_SiteReports_EmployeeId_SiteReportId] FOREIGN KEY ([EmployeeId], [SiteReportId]) REFERENCES [attendance].[SiteReports] ([EmployeeId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SiteReportResponses_UserAccounts_EmployeeId_SubmittedByAccountId] FOREIGN KEY ([EmployeeId], [SubmittedByAccountId]) REFERENCES [auth].[UserAccounts] ([EmployeeId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114635_AddSiteReportResponsesAndShiftReminders'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[ShiftVersions] ADD CONSTRAINT [CK_ShiftVersions_MissingCheckoutReminderAfterMinutes] CHECK ([MissingCheckoutReminderAfterMinutes] IS NULL OR [MissingCheckoutReminderAfterMinutes] > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114635_AddSiteReportResponsesAndShiftReminders'
)
BEGIN
    CREATE INDEX [IX_SiteReportResponses_EmployeeId_SiteReportId] ON [attendance].[SiteReportResponses] ([EmployeeId], [SiteReportId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114635_AddSiteReportResponsesAndShiftReminders'
)
BEGIN
    CREATE INDEX [IX_SiteReportResponses_EmployeeId_SubmittedByAccountId] ON [attendance].[SiteReportResponses] ([EmployeeId], [SubmittedByAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114635_AddSiteReportResponsesAndShiftReminders'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SiteReportResponses_ReturnedReviewId] ON [attendance].[SiteReportResponses] ([ReturnedReviewId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114635_AddSiteReportResponsesAndShiftReminders'
)
BEGIN
    CREATE INDEX [IX_SiteReportResponses_SiteReportId_ReturnedReviewId] ON [attendance].[SiteReportResponses] ([SiteReportId], [ReturnedReviewId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114635_AddSiteReportResponsesAndShiftReminders'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SiteReportResponses_SiteReportId_Sequence] ON [attendance].[SiteReportResponses] ([SiteReportId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114635_AddSiteReportResponsesAndShiftReminders'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012114635_AddSiteReportResponsesAndShiftReminders', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    DECLARE @var19 nvarchar(max);
    SELECT @var19 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[PolicyVersions]') AND [c].[name] = N'ApprovalRequestId');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [hr].[PolicyVersions] DROP CONSTRAINT ' + @var19 + ';');
    ALTER TABLE [hr].[PolicyVersions] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    ALTER TABLE [hr].[PolicyVersions] ADD [Authority] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    DECLARE @var20 nvarchar(max);
    SELECT @var20 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[PolicyAssignments]') AND [c].[name] = N'ApprovalRequestId');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [hr].[PolicyAssignments] DROP CONSTRAINT ' + @var20 + ';');
    ALTER TABLE [hr].[PolicyAssignments] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    ALTER TABLE [hr].[PolicyAssignments] ADD [Authority] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    DECLARE @var21 nvarchar(max);
    SELECT @var21 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[PolicyAssignmentEnds]') AND [c].[name] = N'ApprovalRequestId');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [hr].[PolicyAssignmentEnds] DROP CONSTRAINT ' + @var21 + ';');
    ALTER TABLE [hr].[PolicyAssignmentEnds] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    ALTER TABLE [hr].[PolicyAssignmentEnds] ADD [Authority] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    ALTER TABLE [hr].[Policies] ADD [Revision] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    ALTER TABLE [hr].[LeaveTypes] ADD [Kind] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    DECLARE @var22 nvarchar(max);
    SELECT @var22 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[LeaveRequests]') AND [c].[name] = N'PolicyVersionId');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [hr].[LeaveRequests] DROP CONSTRAINT ' + @var22 + ';');
    ALTER TABLE [hr].[LeaveRequests] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    ALTER TABLE [hr].[LeaveRequests] ADD [PreviousRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    ALTER TABLE [hr].[LeaveRequests] ADD [SubmissionKey] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    EXEC(N'ALTER TABLE [hr].[PolicyVersions] ADD CONSTRAINT [CK_PolicyVersion_Authority] CHECK (([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL))');
    EXEC(N'ALTER TABLE [hr].[PolicyAssignments] ADD CONSTRAINT [CK_PolicyAssignment_Authority] CHECK (([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL))');
    EXEC(N'ALTER TABLE [hr].[PolicyAssignmentEnds] ADD CONSTRAINT [CK_PolicyAssignmentEnd_Authority] CHECK (([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LeaveRequests_EmployeeId_SubmissionKey] ON [hr].[LeaveRequests] ([EmployeeId], [SubmissionKey]) WHERE [SubmissionKey] IS NOT NULL');
    EXEC(N'CREATE UNIQUE INDEX [IX_LeaveRequests_PreviousRequestId] ON [hr].[LeaveRequests] ([PreviousRequestId]) WHERE [PreviousRequestId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    CREATE INDEX [IX_LeaveRequestDays_Date_LeaveRequestId] ON [hr].[LeaveRequestDays] ([Date], [LeaveRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LeaveRequestDays_LeaveRequestId_Date] ON [hr].[LeaveRequestDays] ([LeaveRequestId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    CREATE INDEX [IX_LeaveRequestDays_ShiftVersionId] ON [hr].[LeaveRequestDays] ([ShiftVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    ALTER TABLE [hr].[LeaveRequests] ADD CONSTRAINT [FK_LeaveRequests_LeaveRequests_PreviousRequestId] FOREIGN KEY ([PreviousRequestId]) REFERENCES [hr].[LeaveRequests] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012125614_AddDirectPoliciesAndAdminLeave'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012125614_AddDirectPoliciesAndAdminLeave', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
    CREATE INDEX [IX_AttendancePayrollInputRevisions_ActorUserId] ON [attendance].[AttendancePayrollInputRevisions] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_Revision] ON [attendance].[AttendancePayrollInputRevisions] ([EmployeeId], [PeriodStart], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
    CREATE INDEX [IX_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_SupersedesRevisionId] ON [attendance].[AttendancePayrollInputRevisions] ([EmployeeId], [PeriodStart], [SupersedesRevisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
    CREATE INDEX [IX_CompanyProfiles_HeadOfficeBranchId] ON [hr].[CompanyProfiles] ([HeadOfficeBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
    CREATE INDEX [IX_CompanyProfiles_HeadOfficeCityId] ON [hr].[CompanyProfiles] ([HeadOfficeCityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompanyProfiles_SingletonKey] ON [hr].[CompanyProfiles] ([SingletonKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompanySiteJurisdictionVersions_CompanyProfileId_ConfigurationRevision] ON [hr].[CompanySiteJurisdictionVersions] ([CompanyProfileId], [ConfigurationRevision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CompanySiteJurisdictionVersions_SiteId_EffectiveFrom] ON [hr].[CompanySiteJurisdictionVersions] ([SiteId], [EffectiveFrom]) WHERE [SupersedesVersionId] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CompanySiteJurisdictionVersions_SupersedesVersionId] ON [hr].[CompanySiteJurisdictionVersions] ([SupersedesVersionId]) WHERE [SupersedesVersionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012135725_AddAttendancePayrollInputsAndCompanySettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012135725_AddAttendancePayrollInputsAndCompanySettings', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
    ALTER TABLE [hr].[PolicyVersions] ADD CONSTRAINT [AK_PolicyVersions_PolicyId_Id] UNIQUE ([PolicyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AttendanceLegalReviewDossiers_BindingSha256_Revision] ON [attendance].[AttendanceLegalReviewDossiers] ([BindingSha256], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
    CREATE INDEX [IX_AttendanceLegalReviewDossiers_RecordedByAccountId] ON [attendance].[AttendanceLegalReviewDossiers] ([RecordedByAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
    CREATE INDEX [IX_AttendanceLegalReviewDossiers_SourcePolicyId_SourcePolicyVersionId] ON [attendance].[AttendanceLegalReviewDossiers] ([SourcePolicyId], [SourcePolicyVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
    CREATE INDEX [IX_AttendanceLegalReviewDossiers_SourcePolicyVersionId_PolicySha256_BindingSha256_SupersedesId] ON [attendance].[AttendanceLegalReviewDossiers] ([SourcePolicyVersionId], [PolicySha256], [BindingSha256], [SupersedesId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AttendanceLegalReviewDossiers_SupersedesId] ON [attendance].[AttendanceLegalReviewDossiers] ([SupersedesId]) WHERE [SupersedesId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
    CREATE INDEX [IX_AttendancePayrollAttestations_ActorUserId] ON [attendance].[AttendancePayrollAttestations] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
    CREATE INDEX [IX_AttendancePayrollAttestations_EmployeeId_PeriodStart_SourceRevisionId_PreviousAttestationId] ON [attendance].[AttendancePayrollAttestations] ([EmployeeId], [PeriodStart], [SourceRevisionId], [PreviousAttestationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AttendancePayrollAttestations_EmployeeId_PeriodStart_Step] ON [attendance].[AttendancePayrollAttestations] ([EmployeeId], [PeriodStart], [Step]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AttendancePayrollAttestations_ExternalReceiptKey_Step] ON [attendance].[AttendancePayrollAttestations] ([ExternalReceiptKey], [Step]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013101327_AddOfficeRosterHistory'
)
BEGIN
    CREATE TABLE [attendance].[OfficeRosterAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [ShiftId] uniqueidentifier NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [HolidayCalendarId] uniqueidentifier NULL,
        [RecordedBy] nvarchar(200) NOT NULL,
        [RecordedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_OfficeRosterAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OfficeRosterAssignments_Dates] CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]),
        CONSTRAINT [FK_OfficeRosterAssignments_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OfficeRosterAssignments_HolidayCalendars_HolidayCalendarId] FOREIGN KEY ([HolidayCalendarId]) REFERENCES [hr].[HolidayCalendars] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OfficeRosterAssignments_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [attendance].[Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013101327_AddOfficeRosterHistory'
)
BEGIN
    CREATE TABLE [attendance].[OfficeRosterAssignmentEnds] (
        [Id] uniqueidentifier NOT NULL,
        [OfficeRosterAssignmentId] uniqueidentifier NOT NULL,
        [EffectiveTo] date NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [RecordedBy] nvarchar(200) NOT NULL,
        [RecordedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_OfficeRosterAssignmentEnds] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OfficeRosterAssignmentEnds_Reason] CHECK (LEN(LTRIM(RTRIM([Reason]))) > 0),
        CONSTRAINT [FK_OfficeRosterAssignmentEnds_OfficeRosterAssignments_OfficeRosterAssignmentId] FOREIGN KEY ([OfficeRosterAssignmentId]) REFERENCES [attendance].[OfficeRosterAssignments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013101327_AddOfficeRosterHistory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OfficeRosterAssignmentEnds_OfficeRosterAssignmentId] ON [attendance].[OfficeRosterAssignmentEnds] ([OfficeRosterAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013101327_AddOfficeRosterHistory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OfficeRosterAssignments_BranchId_EffectiveFrom] ON [attendance].[OfficeRosterAssignments] ([BranchId], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013101327_AddOfficeRosterHistory'
)
BEGIN
    CREATE INDEX [IX_OfficeRosterAssignments_HolidayCalendarId] ON [attendance].[OfficeRosterAssignments] ([HolidayCalendarId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013101327_AddOfficeRosterHistory'
)
BEGIN
    CREATE INDEX [IX_OfficeRosterAssignments_ShiftId] ON [attendance].[OfficeRosterAssignments] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013101327_AddOfficeRosterHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013101327_AddOfficeRosterHistory', N'10.0.12');
END;

COMMIT;
GO

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

