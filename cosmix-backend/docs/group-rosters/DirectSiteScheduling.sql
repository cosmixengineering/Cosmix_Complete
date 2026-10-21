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

