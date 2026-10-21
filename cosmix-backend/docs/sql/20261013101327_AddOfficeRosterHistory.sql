BEGIN TRANSACTION;
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

CREATE UNIQUE INDEX [IX_OfficeRosterAssignmentEnds_OfficeRosterAssignmentId] ON [attendance].[OfficeRosterAssignmentEnds] ([OfficeRosterAssignmentId]);

CREATE UNIQUE INDEX [IX_OfficeRosterAssignments_BranchId_EffectiveFrom] ON [attendance].[OfficeRosterAssignments] ([BranchId], [EffectiveFrom]);

CREATE INDEX [IX_OfficeRosterAssignments_HolidayCalendarId] ON [attendance].[OfficeRosterAssignments] ([HolidayCalendarId]);

CREATE INDEX [IX_OfficeRosterAssignments_ShiftId] ON [attendance].[OfficeRosterAssignments] ([ShiftId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261013101327_AddOfficeRosterHistory', N'10.0.12');

COMMIT;
GO

