BEGIN TRANSACTION;
DROP INDEX [IX_HolidayCalendars_Code_EffectiveFrom] ON [hr].[HolidayCalendars];

ALTER TABLE [hr].[HolidayCalendars] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [hr].[HolidayCalendars] ADD [Revision] int NOT NULL DEFAULT 1;

ALTER TABLE [hr].[HolidayCalendars] ADD [SupersedesCalendarId] uniqueidentifier NULL;

CREATE TABLE [hr].[OrganizationChanges] (
    [Id] uniqueidentifier NOT NULL,
    [AuditEventId] uniqueidentifier NOT NULL,
    [Resource] nvarchar(40) NOT NULL,
    [ResourceId] uniqueidentifier NOT NULL,
    [Operation] nvarchar(100) NOT NULL,
    [Reason] nvarchar(500) NOT NULL,
    [BeforeJson] nvarchar(max) NULL,
    [AfterJson] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_OrganizationChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_OrganizationChanges_Json] CHECK (ISJSON([AfterJson]) = 1 AND ([BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1)),
    CONSTRAINT [FK_OrganizationChanges_AuditEvents_AuditEventId] FOREIGN KEY ([AuditEventId]) REFERENCES [audit].[AuditEvents] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_HolidayCalendars_Code_EffectiveFrom_Revision] ON [hr].[HolidayCalendars] ([Code], [EffectiveFrom], [Revision]);

CREATE UNIQUE INDEX [IX_HolidayCalendars_SupersedesCalendarId] ON [hr].[HolidayCalendars] ([SupersedesCalendarId]) WHERE [SupersedesCalendarId] IS NOT NULL;

ALTER TABLE [hr].[HolidayCalendars] ADD CONSTRAINT [CK_HolidayCalendars_Predecessor] CHECK (([Revision] = 1 AND [SupersedesCalendarId] IS NULL) OR ([Revision] > 1 AND [SupersedesCalendarId] IS NOT NULL));

ALTER TABLE [hr].[HolidayCalendars] ADD CONSTRAINT [CK_HolidayCalendars_Revision] CHECK ([Revision] >= 1);

CREATE UNIQUE INDEX [IX_OrganizationChanges_AuditEventId] ON [hr].[OrganizationChanges] ([AuditEventId]);

CREATE INDEX [IX_OrganizationChanges_Resource_ResourceId] ON [hr].[OrganizationChanges] ([Resource], [ResourceId]);

ALTER TABLE [hr].[HolidayCalendars] ADD CONSTRAINT [FK_HolidayCalendars_HolidayCalendars_SupersedesCalendarId] FOREIGN KEY ([SupersedesCalendarId]) REFERENCES [hr].[HolidayCalendars] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007122140_AddOrganizationMastersAndHolidayRevisions', N'10.0.12');

COMMIT;
GO
