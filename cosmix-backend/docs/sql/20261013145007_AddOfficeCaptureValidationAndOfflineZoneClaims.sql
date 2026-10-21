BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[ZoneEvents] DROP CONSTRAINT [CK_ZoneEvents_LocationRetention];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[ZoneEvents] ADD [WrittenClaimExpiresAtUtc] datetimeoffset(7) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[ZoneEvents] ADD [WrittenClaimPrivateReference] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[WorkSessions] ADD [BranchId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[WorkSessions] ADD [EmployeeAssignmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[WorkSessions] ADD [HolidayCalendarId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[WorkSessions] ADD [OfficeGeofenceVersionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[WorkSessions] ADD [OfficeRosterAssignmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD [BranchId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD [OfficeGeofenceVersionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD [OfficeRosterAssignmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[OfficeRosterAssignments] ADD CONSTRAINT [AK_OfficeRosterAssignments_BranchId_Id] UNIQUE ([BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE TABLE [attendance].[CaptureValidationPolicyVersions] (
        [Id] uniqueidentifier NOT NULL,
        [Revision] int NOT NULL,
        [SupersedesVersionId] uniqueidentifier NULL,
        [EffectiveFromUtc] datetimeoffset(7) NOT NULL,
        [PublishedAtUtc] datetimeoffset(7) NOT NULL,
        [PublishedBy] nvarchar(200) NOT NULL,
        [ApprovalReference] nvarchar(200) NOT NULL,
        [SettingsJson] nvarchar(max) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [SourceFingerprint] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [ProtectedEvidenceReference] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_CaptureValidationPolicyVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CaptureValidationPolicyVersions_Id_Revision_SourceFingerprint] UNIQUE ([Id], [Revision], [SourceFingerprint]),
        CONSTRAINT [CK_CaptureValidationPolicyVersions_EffectiveFromUtc_Utc] CHECK ([EffectiveFromUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveFromUtc]) = 0),
        CONSTRAINT [CK_CaptureValidationPolicyVersions_Hash] CHECK (LEN([SourceFingerprint]) = 64 AND [SourceFingerprint] NOT LIKE '%[^0-9A-F]%'),
        CONSTRAINT [CK_CaptureValidationPolicyVersions_PublishedAtUtc_Utc] CHECK ([PublishedAtUtc] IS NULL OR DATEPART(TZOFFSET, [PublishedAtUtc]) = 0),
        CONSTRAINT [CK_CaptureValidationPolicyVersions_Revision] CHECK (([Revision] = 1 AND [SupersedesVersionId] IS NULL) OR ([Revision] > 1 AND [SupersedesVersionId] IS NOT NULL)),
        CONSTRAINT [CK_CaptureValidationPolicyVersions_Settings] CHECK (ISJSON([SettingsJson]) = 1 AND LEN([SettingsJson]) <= 32768),
        CONSTRAINT [CK_CaptureValidationPolicyVersions_Window] CHECK ([EffectiveFromUtc] >= [PublishedAtUtc]),
        CONSTRAINT [FK_CaptureValidationPolicyVersions_CaptureValidationPolicyVersions_SupersedesVersionId] FOREIGN KEY ([SupersedesVersionId]) REFERENCES [attendance].[CaptureValidationPolicyVersions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE TABLE [attendance].[OfficeGeofenceVersions] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [Latitude] decimal(10,7) NOT NULL,
        [Longitude] decimal(10,7) NOT NULL,
        [RadiusMeters] decimal(10,2) NOT NULL,
        [MaximumAccuracyMeters] decimal(10,2) NOT NULL,
        [JurisdictionCode] varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [ApprovalReference] nvarchar(500) NOT NULL,
        [RecordedBy] nvarchar(200) NOT NULL,
        [RecordedAtUtc] datetimeoffset(7) NOT NULL,
        CONSTRAINT [PK_OfficeGeofenceVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_OfficeGeofenceVersions_BranchId_Id] UNIQUE ([BranchId], [Id]),
        CONSTRAINT [CK_OfficeGeofenceVersions_Geometry] CHECK ([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND [RadiusMeters] > 0 AND [MaximumAccuracyMeters] > 0),
        CONSTRAINT [CK_OfficeGeofenceVersions_RecordedUtc] CHECK (DATEPART(TZOFFSET,[RecordedAtUtc]) = 0),
        CONSTRAINT [CK_OfficeGeofenceVersions_Version] CHECK ([VersionNumber] > 0),
        CONSTRAINT [FK_OfficeGeofenceVersions_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE TABLE [attendance].[CaptureValidationFinalEvidence] (
        [Id] uniqueidentifier NOT NULL,
        [RawPunchEventId] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Action] int NOT NULL,
        [IsOffline] bit NOT NULL,
        [RegisteredDeviceId] nvarchar(200) NOT NULL,
        [RequestSha256] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [ReceivedAtUtc] datetimeoffset(7) NOT NULL,
        [EvaluatedAtUtc] datetimeoffset(7) NOT NULL,
        [AssessedAtUtc] datetimeoffset(7) NOT NULL,
        [CandidateOutcome] int NOT NULL,
        [CandidateReasonCode] nvarchar(100) NOT NULL,
        [FinalOutcome] int NOT NULL,
        [FinalReasonCode] nvarchar(100) NOT NULL,
        [PolicyVersionId] uniqueidentifier NULL,
        [PolicyRevision] int NULL,
        [SourceFingerprint] char(64) COLLATE Latin1_General_100_BIN2 NULL,
        [CodesJson] nvarchar(max) NOT NULL,
        [RelatedRawPunchIdsJson] nvarchar(max) NOT NULL,
        [DeviceObservationCode] nvarchar(100) NULL,
        CONSTRAINT [PK_CaptureValidationFinalEvidence] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CaptureValidationFinalEvidence_Acceptance] CHECK ([FinalOutcome] NOT IN (2, 3) OR ([CandidateOutcome] IN (2, 3) AND [PolicyVersionId] IS NOT NULL)),
        CONSTRAINT [CK_CaptureValidationFinalEvidence_Action] CHECK ([Action] IN (1, 2)),
        CONSTRAINT [CK_CaptureValidationFinalEvidence_AssessedAtUtc_Utc] CHECK ([AssessedAtUtc] IS NULL OR DATEPART(TZOFFSET, [AssessedAtUtc]) = 0),
        CONSTRAINT [CK_CaptureValidationFinalEvidence_Clock] CHECK ([ReceivedAtUtc] <= [EvaluatedAtUtc] AND [EvaluatedAtUtc] <= [AssessedAtUtc]),
        CONSTRAINT [CK_CaptureValidationFinalEvidence_EvaluatedAtUtc_Utc] CHECK ([EvaluatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [EvaluatedAtUtc]) = 0),
        CONSTRAINT [CK_CaptureValidationFinalEvidence_Json] CHECK (ISJSON([CodesJson]) = 1 AND ISJSON([RelatedRawPunchIdsJson]) = 1),
        CONSTRAINT [CK_CaptureValidationFinalEvidence_Offline] CHECK ([IsOffline] = 0 OR ([CandidateOutcome] = 1 AND [FinalOutcome] = 1)),
        CONSTRAINT [CK_CaptureValidationFinalEvidence_Outcome] CHECK ([CandidateOutcome] BETWEEN 1 AND 5 AND [FinalOutcome] BETWEEN 1 AND 5),
        CONSTRAINT [CK_CaptureValidationFinalEvidence_Policy] CHECK (([PolicyVersionId] IS NULL AND [PolicyRevision] IS NULL AND [SourceFingerprint] IS NULL) OR ([PolicyVersionId] IS NOT NULL AND [PolicyRevision] IS NOT NULL AND [SourceFingerprint] IS NOT NULL AND [PolicyRevision] > 0 AND LEN([SourceFingerprint]) = 64)),
        CONSTRAINT [CK_CaptureValidationFinalEvidence_ReceivedAtUtc_Utc] CHECK ([ReceivedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ReceivedAtUtc]) = 0),
        CONSTRAINT [FK_CaptureValidationFinalEvidence_CaptureValidationPolicyVersions_PolicyVersionId_PolicyRevision_SourceFingerprint] FOREIGN KEY ([PolicyVersionId], [PolicyRevision], [SourceFingerprint]) REFERENCES [attendance].[CaptureValidationPolicyVersions] ([Id], [Revision], [SourceFingerprint]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CaptureValidationFinalEvidence_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CaptureValidationFinalEvidence_RawPunchEvents_EmployeeId_RawPunchEventId] FOREIGN KEY ([EmployeeId], [RawPunchEventId]) REFERENCES [attendance].[RawPunchEvents] ([EmployeeId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE INDEX [IX_ZoneEvents_WrittenClaimExpiresAtUtc_Id] ON [attendance].[ZoneEvents] ([WrittenClaimExpiresAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_LocationRetention] CHECK (([LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [AccuracyMeters] IS NOT NULL) OR ([LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LocationExpiresAtUtc] > [ReceivedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL) OR ([WrittenClaimPrivateReference] IS NOT NULL AND [WrittenClaimExpiresAtUtc] IS NOT NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_WrittenClaim] CHECK (([WrittenClaimPrivateReference] IS NULL AND [WrittenClaimExpiresAtUtc] IS NULL) OR ([WrittenClaimPrivateReference] IS NOT NULL AND [WrittenClaimExpiresAtUtc] IS NOT NULL AND [Source] = 3 AND [IsOffline] = 1 AND [DeviceCapturedAtUtc] IS NOT NULL AND [DeviceCapturedAtUtc] <= [ReceivedAtUtc] AND [WrittenClaimExpiresAtUtc] > [ReceivedAtUtc] AND [WrittenClaimExpiresAtUtc] > [DeviceCapturedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_WrittenClaimExpiresAtUtc_Utc] CHECK ([WrittenClaimExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [WrittenClaimExpiresAtUtc]) = 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE INDEX [IX_WorkSessions_BranchId_OfficeGeofenceVersionId] ON [attendance].[WorkSessions] ([BranchId], [OfficeGeofenceVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE INDEX [IX_WorkSessions_BranchId_OfficeRosterAssignmentId] ON [attendance].[WorkSessions] ([BranchId], [OfficeRosterAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE INDEX [IX_WorkSessions_EmployeeId_EmployeeAssignmentId] ON [attendance].[WorkSessions] ([EmployeeId], [EmployeeAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE INDEX [IX_WorkSessions_HolidayCalendarId] ON [attendance].[WorkSessions] ([HolidayCalendarId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [CK_WorkSessions_OfficeContext] CHECK (([BranchId] IS NULL AND [OfficeRosterAssignmentId] IS NULL AND [OfficeGeofenceVersionId] IS NULL) OR ([BranchId] IS NOT NULL AND [SiteId] IS NULL AND [OfficeRosterAssignmentId] IS NOT NULL AND [EmployeeAssignmentId] IS NOT NULL AND [OfficeGeofenceVersionId] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE INDEX [IX_RawPunchEvents_BranchId_OfficeGeofenceVersionId] ON [attendance].[RawPunchEvents] ([BranchId], [OfficeGeofenceVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE INDEX [IX_RawPunchEvents_BranchId_OfficeRosterAssignmentId] ON [attendance].[RawPunchEvents] ([BranchId], [OfficeRosterAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    EXEC(N'ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [CK_RawPunchEvents_OfficeContext] CHECK (([BranchId] IS NULL AND [OfficeRosterAssignmentId] IS NULL AND [OfficeGeofenceVersionId] IS NULL) OR ([BranchId] IS NOT NULL AND [BusinessDate] IS NOT NULL AND [SiteId] IS NULL AND [GeofenceVersionId] IS NULL AND [SiteRosterAssignmentId] IS NULL AND [TemporaryShiftChangeId] IS NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE INDEX [IX_CaptureValidationFinalEvidence_EmployeeId_RawPunchEventId] ON [attendance].[CaptureValidationFinalEvidence] ([EmployeeId], [RawPunchEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE INDEX [IX_CaptureValidationFinalEvidence_EmployeeId_ReceivedAtUtc] ON [attendance].[CaptureValidationFinalEvidence] ([EmployeeId], [ReceivedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE INDEX [IX_CaptureValidationFinalEvidence_PolicyVersionId_PolicyRevision_SourceFingerprint] ON [attendance].[CaptureValidationFinalEvidence] ([PolicyVersionId], [PolicyRevision], [SourceFingerprint]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CaptureValidationFinalEvidence_RawPunchEventId] ON [attendance].[CaptureValidationFinalEvidence] ([RawPunchEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE INDEX [IX_CaptureValidationPolicyVersions_EffectiveFromUtc_Revision] ON [attendance].[CaptureValidationPolicyVersions] ([EffectiveFromUtc], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CaptureValidationPolicyVersions_Revision] ON [attendance].[CaptureValidationPolicyVersions] ([Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CaptureValidationPolicyVersions_SupersedesVersionId] ON [attendance].[CaptureValidationPolicyVersions] ([SupersedesVersionId]) WHERE [SupersedesVersionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OfficeGeofenceVersions_BranchId_EffectiveFrom] ON [attendance].[OfficeGeofenceVersions] ([BranchId], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OfficeGeofenceVersions_BranchId_VersionNumber] ON [attendance].[OfficeGeofenceVersions] ([BranchId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_OfficeGeofenceVersions_BranchId_OfficeGeofenceVersionId] FOREIGN KEY ([BranchId], [OfficeGeofenceVersionId]) REFERENCES [attendance].[OfficeGeofenceVersions] ([BranchId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[RawPunchEvents] ADD CONSTRAINT [FK_RawPunchEvents_OfficeRosterAssignments_BranchId_OfficeRosterAssignmentId] FOREIGN KEY ([BranchId], [OfficeRosterAssignmentId]) REFERENCES [attendance].[OfficeRosterAssignments] ([BranchId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [FK_WorkSessions_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [FK_WorkSessions_EmployeeAssignments_EmployeeId_EmployeeAssignmentId] FOREIGN KEY ([EmployeeId], [EmployeeAssignmentId]) REFERENCES [hr].[EmployeeAssignments] ([EmployeeId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [FK_WorkSessions_HolidayCalendars_HolidayCalendarId] FOREIGN KEY ([HolidayCalendarId]) REFERENCES [hr].[HolidayCalendars] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [FK_WorkSessions_OfficeGeofenceVersions_BranchId_OfficeGeofenceVersionId] FOREIGN KEY ([BranchId], [OfficeGeofenceVersionId]) REFERENCES [attendance].[OfficeGeofenceVersions] ([BranchId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    ALTER TABLE [attendance].[WorkSessions] ADD CONSTRAINT [FK_WorkSessions_OfficeRosterAssignments_BranchId_OfficeRosterAssignmentId] FOREIGN KEY ([BranchId], [OfficeRosterAssignmentId]) REFERENCES [attendance].[OfficeRosterAssignments] ([BranchId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims', N'10.0.12');
END;

COMMIT;
GO

