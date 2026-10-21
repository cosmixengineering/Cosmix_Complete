BEGIN TRANSACTION;
ALTER TABLE [attendance].[ShiftVersions] ADD [MissingCheckoutReminderAfterMinutes] int NULL;

ALTER TABLE [attendance].[SiteReports] ADD CONSTRAINT [AK_SiteReports_EmployeeId_Id] UNIQUE ([EmployeeId], [Id]);

ALTER TABLE [attendance].[SiteReportReviews] ADD CONSTRAINT [AK_SiteReportReviews_SiteReportId_Id] UNIQUE ([SiteReportId], [Id]);

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

EXEC(N'ALTER TABLE [attendance].[ShiftVersions] ADD CONSTRAINT [CK_ShiftVersions_MissingCheckoutReminderAfterMinutes] CHECK ([MissingCheckoutReminderAfterMinutes] IS NULL OR [MissingCheckoutReminderAfterMinutes] > 0)');

CREATE INDEX [IX_SiteReportResponses_EmployeeId_SiteReportId] ON [attendance].[SiteReportResponses] ([EmployeeId], [SiteReportId]);

CREATE INDEX [IX_SiteReportResponses_EmployeeId_SubmittedByAccountId] ON [attendance].[SiteReportResponses] ([EmployeeId], [SubmittedByAccountId]);

CREATE UNIQUE INDEX [IX_SiteReportResponses_ReturnedReviewId] ON [attendance].[SiteReportResponses] ([ReturnedReviewId]);

CREATE INDEX [IX_SiteReportResponses_SiteReportId_ReturnedReviewId] ON [attendance].[SiteReportResponses] ([SiteReportId], [ReturnedReviewId]);

CREATE UNIQUE INDEX [IX_SiteReportResponses_SiteReportId_Sequence] ON [attendance].[SiteReportResponses] ([SiteReportId], [Sequence]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261012114635_AddSiteReportResponsesAndShiftReminders', N'10.0.12');

COMMIT;
GO

