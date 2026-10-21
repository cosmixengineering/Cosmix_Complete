BEGIN TRANSACTION;
ALTER TABLE [auth].[MobileOtpChallenges] DROP CONSTRAINT [CK_MobileOtpChallenges_Purpose];

ALTER TABLE [hr].[EmployeeSubmissions] DROP CONSTRAINT [CK_EmployeeSubmissions_MobileEnrollment];

ALTER TABLE [hr].[Employees] DROP CONSTRAINT [CK_Employees_MobileEnrollment];

ALTER TABLE [auth].[UserSessions] ADD [SmsOtpChallengeId] uniqueidentifier NULL;

ALTER TABLE [auth].[Roles] ADD [SystemRole] int NOT NULL DEFAULT 0;

ALTER TABLE [auth].[MobileOtpChallenges] ADD CONSTRAINT [AK_MobileOtpChallenges_UserAccountId_Id] UNIQUE ([UserAccountId], [Id]);

CREATE UNIQUE INDEX [IX_UserSessions_SmsOtpChallengeId] ON [auth].[UserSessions] ([SmsOtpChallengeId]) WHERE [SmsOtpChallengeId] IS NOT NULL;

CREATE INDEX [IX_UserSessions_UserAccountId_SmsOtpChallengeId] ON [auth].[UserSessions] ([UserAccountId], [SmsOtpChallengeId]);

ALTER TABLE [auth].[UserSessions] ADD CONSTRAINT [CK_UserSessions_SmsProof] CHECK ([SmsOtpChallengeId] IS NULL OR ([Client] = 1 AND [MfaChallengeId] IS NULL AND [RegisteredDeviceId] IS NULL));

CREATE UNIQUE INDEX [IX_Roles_SystemRole] ON [auth].[Roles] ([SystemRole]) WHERE [SystemRole] <> 0;

ALTER TABLE [auth].[Roles] ADD CONSTRAINT [CK_Roles_SystemRole] CHECK ([SystemRole] IN (0, 1, 2));

ALTER TABLE [auth].[MobileOtpChallenges] ADD CONSTRAINT [CK_MobileOtpChallenges_Purpose] CHECK (([Purpose] = 'MobileLogin' AND [Platform] IN ('android','ios')) OR ([Purpose] = 'WebLogin' AND [Platform] = 'web'));

ALTER TABLE [hr].[EmployeeSubmissions] ADD CONSTRAINT [CK_EmployeeSubmissions_MobileEnrollment] CHECK ([MobileAccessRequested] = 0 OR [ApprovedLoginPhoneNumber] IS NOT NULL);

ALTER TABLE [hr].[Employees] ADD CONSTRAINT [CK_Employees_MobileEnrollment] CHECK ([MobileAccessRequested] = 0 OR [ApprovedLoginPhoneNumber] IS NOT NULL);

ALTER TABLE [auth].[UserSessions] ADD CONSTRAINT [FK_UserSessions_MobileOtpChallenges_UserAccountId_SmsOtpChallengeId] FOREIGN KEY ([UserAccountId], [SmsOtpChallengeId]) REFERENCES [auth].[MobileOtpChallenges] ([UserAccountId], [Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007080926_AddWebSmsAuthenticationAndProtectedRoles', N'10.0.12');

COMMIT;
GO
