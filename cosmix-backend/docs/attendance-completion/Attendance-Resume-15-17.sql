
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

BEGIN TRANSACTION;
DECLARE @var8 nvarchar(max);
SELECT @var8 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneVersions]') AND [c].[name] = N'ApprovalRequestId');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneVersions] DROP CONSTRAINT ' + @var8 + ';');
ALTER TABLE [attendance].[ZoneVersions] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [attendance].[ZoneVersions] ADD [DirectAuthorityEvidenceReference] nvarchar(500) NULL;

DECLARE @var9 nvarchar(max);
SELECT @var9 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneEvents]') AND [c].[name] = N'Longitude');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneEvents] DROP CONSTRAINT ' + @var9 + ';');
ALTER TABLE [attendance].[ZoneEvents] ALTER COLUMN [Longitude] decimal(10,7) NULL;

DECLARE @var10 nvarchar(max);
SELECT @var10 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneEvents]') AND [c].[name] = N'Latitude');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneEvents] DROP CONSTRAINT ' + @var10 + ';');
ALTER TABLE [attendance].[ZoneEvents] ALTER COLUMN [Latitude] decimal(10,7) NULL;

DECLARE @var11 nvarchar(max);
SELECT @var11 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneEvents]') AND [c].[name] = N'AccuracyMeters');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneEvents] DROP CONSTRAINT ' + @var11 + ';');
ALTER TABLE [attendance].[ZoneEvents] ALTER COLUMN [AccuracyMeters] decimal(10,2) NULL;

ALTER TABLE [attendance].[ZoneEvents] ADD [LocationExpiresAtUtc] datetimeoffset(7) NULL;

ALTER TABLE [attendance].[ZoneEvents] ADD [LocationPrivateReference] nvarchar(500) NULL;

DECLARE @var12 nvarchar(max);
SELECT @var12 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[ZoneAssignments]') AND [c].[name] = N'ApprovalRequestId');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[ZoneAssignments] DROP CONSTRAINT ' + @var12 + ';');
ALTER TABLE [attendance].[ZoneAssignments] ALTER COLUMN [ApprovalRequestId] uniqueidentifier NULL;

ALTER TABLE [attendance].[ZoneAssignments] ADD [DirectAuthorityEvidenceReference] nvarchar(500) NULL;

DECLARE @var13 nvarchar(max);
SELECT @var13 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[OvertimeRequests]') AND [c].[name] = N'RateRuleCode');
IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[OvertimeRequests] DROP CONSTRAINT ' + @var13 + ';');
ALTER TABLE [attendance].[OvertimeRequests] ALTER COLUMN [RateRuleCode] nvarchar(100) NULL;

DECLARE @var14 nvarchar(max);
SELECT @var14 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[OvertimeRequests]') AND [c].[name] = N'PolicyVersionId');
IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[OvertimeRequests] DROP CONSTRAINT ' + @var14 + ';');
ALTER TABLE [attendance].[OvertimeRequests] ALTER COLUMN [PolicyVersionId] uniqueidentifier NULL;

DECLARE @var15 nvarchar(max);
SELECT @var15 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[attendance].[DutyMonitoringWindows]') AND [c].[name] = N'RetentionPolicyVersionId');
IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [attendance].[DutyMonitoringWindows] DROP CONSTRAINT ' + @var15 + ';');
ALTER TABLE [attendance].[DutyMonitoringWindows] ALTER COLUMN [RetentionPolicyVersionId] uniqueidentifier NULL;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [AcceptedCheckInId] uniqueidentifier NULL;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [ConfigurationVersionId] uniqueidentifier NULL;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [RegisteredDeviceId] uniqueidentifier NULL;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD [WorkSessionKey] uniqueidentifier NULL;

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

EXEC(N'ALTER TABLE [attendance].[ZoneVersions] ADD CONSTRAINT [CK_ZoneVersions_Authority] CHECK ([ApprovalRequestId] IS NOT NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL);');

CREATE INDEX [IX_ZoneEvents_LocationExpiresAtUtc_Id] ON [attendance].[ZoneEvents] ([LocationExpiresAtUtc], [Id]);

EXEC(N'ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_LocationExpiresAtUtc_Utc] CHECK ([LocationExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [LocationExpiresAtUtc]) = 0);');

EXEC(N'ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_LocationRetention] CHECK (([LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [AccuracyMeters] IS NOT NULL) OR ([LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LocationExpiresAtUtc] > [ReceivedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL));');

EXEC(N'ALTER TABLE [attendance].[ZoneAssignments] ADD CONSTRAINT [CK_ZoneAssignments_Authority] CHECK ([ApprovalRequestId] IS NOT NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL);');

CREATE INDEX [IX_DutyMonitoringWindows_AcceptedCheckInId] ON [attendance].[DutyMonitoringWindows] ([AcceptedCheckInId]);

CREATE INDEX [IX_DutyMonitoringWindows_ConfigurationVersionId] ON [attendance].[DutyMonitoringWindows] ([ConfigurationVersionId]);

CREATE INDEX [IX_DutyMonitoringWindows_RegisteredDeviceId] ON [attendance].[DutyMonitoringWindows] ([RegisteredDeviceId]);

EXEC(N'ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [CK_DutyMonitoringWindows_Provenance] CHECK (([RetentionPolicyVersionId] IS NOT NULL AND [ConfigurationVersionId] IS NULL AND [AcceptedCheckInId] IS NULL AND [WorkSessionKey] IS NULL AND [RegisteredDeviceId] IS NULL) OR ([RetentionPolicyVersionId] IS NULL AND [ConfigurationVersionId] IS NOT NULL AND [AcceptedCheckInId] IS NOT NULL AND [WorkSessionKey] IS NOT NULL AND [RegisteredDeviceId] IS NOT NULL));');

CREATE INDEX [IX_OvertimeEligibilityVersions_EmployeeId] ON [attendance].[OvertimeEligibilityVersions] ([EmployeeId]);

CREATE INDEX [IX_OvertimeEligibilityVersions_SupersedesVersionId] ON [attendance].[OvertimeEligibilityVersions] ([SupersedesVersionId]);

CREATE INDEX [IX_OvertimeEligibilityVersions_Target_EmployeeId_EffectiveFrom_Revision] ON [attendance].[OvertimeEligibilityVersions] ([Target], [EmployeeId], [EffectiveFrom], [Revision]);

CREATE UNIQUE INDEX [IX_OvertimeEligibilityVersions_Target_EmployeeId_Revision] ON [attendance].[OvertimeEligibilityVersions] ([Target], [EmployeeId], [Revision]);

CREATE UNIQUE INDEX [IX_ZoneAssignmentEnds_ZoneAssignmentId] ON [attendance].[ZoneAssignmentEnds] ([ZoneAssignmentId]);

CREATE UNIQUE INDEX [IX_ZoneLocationDeletions_ZoneEventId] ON [attendance].[ZoneLocationDeletions] ([ZoneEventId]);

CREATE UNIQUE INDEX [IX_ZoneMonitoringCheckpoints_DutyMonitoringWindowId] ON [attendance].[ZoneMonitoringCheckpoints] ([DutyMonitoringWindowId]);

CREATE INDEX [IX_ZoneMonitoringConfigurationVersions_SiteId_EffectiveFromUtc] ON [attendance].[ZoneMonitoringConfigurationVersions] ([SiteId], [EffectiveFromUtc]);

CREATE UNIQUE INDEX [IX_ZoneMonitoringConfigurationVersions_SiteId_VersionNumber] ON [attendance].[ZoneMonitoringConfigurationVersions] ([SiteId], [VersionNumber]);

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [FK_DutyMonitoringWindows_RawPunchEvents_AcceptedCheckInId] FOREIGN KEY ([AcceptedCheckInId]) REFERENCES [attendance].[RawPunchEvents] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [FK_DutyMonitoringWindows_RegisteredDevices_RegisteredDeviceId] FOREIGN KEY ([RegisteredDeviceId]) REFERENCES [auth].[RegisteredDevices] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [FK_DutyMonitoringWindows_ZoneMonitoringConfigurationVersions_ConfigurationVersionId] FOREIGN KEY ([ConfigurationVersionId]) REFERENCES [attendance].[ZoneMonitoringConfigurationVersions] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261012081503_AddOvertimeEligibilityAndDutyZones', N'10.0.12');

COMMIT;

GO

BEGIN TRANSACTION;
ALTER TABLE [attendance].[ZoneMonitoringCheckpoints] ADD [LastWarning] bit NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261012082833_RetainZoneWarningTransitions', N'10.0.12');

COMMIT;

GO
