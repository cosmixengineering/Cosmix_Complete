BEGIN TRANSACTION;
ALTER TABLE [auth].[UserAccounts] DROP CONSTRAINT [CK_UserAccounts_Active];

ALTER TABLE [auth].[UserAccounts] ADD [MobileAccessEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [auth].[UserAccounts] ADD [PhoneVerifiedAt] datetimeoffset NULL;

ALTER TABLE [auth].[UserAccounts] ADD [WebAccessEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [hr].[EmployeeSubmissions] ADD [ApprovedLoginPhoneNumber] varchar(16) COLLATE Latin1_General_100_BIN2 NULL;

ALTER TABLE [hr].[EmployeeSubmissions] ADD [MobileAccessRequested] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [hr].[Employees] ADD [ApprovedLoginPhoneNumber] varchar(16) COLLATE Latin1_General_100_BIN2 NULL;

ALTER TABLE [hr].[Employees] ADD [MobileAccessRequested] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE TABLE [auth].[AuthRateBuckets] (
    [Key] nvarchar(150) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [WindowStartsAt] datetimeoffset NOT NULL,
    [WindowEndsAt] datetimeoffset NOT NULL,
    [Count] int NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_AuthRateBuckets] PRIMARY KEY ([Key]),
    CONSTRAINT [CK_AuthRateBuckets_Count] CHECK ([Count] >= 0),
    CONSTRAINT [CK_AuthRateBuckets_Window] CHECK ([WindowEndsAt] > [WindowStartsAt])
);

CREATE TABLE [auth].[LoginPhoneBindings] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [PhoneNumber] varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ApprovedSubmissionId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [VerifiedAt] datetimeoffset NULL,
    [RevokedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_LoginPhoneBindings] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_LoginPhoneBindings_UserAccountId_Id] UNIQUE ([UserAccountId], [Id]),
    CONSTRAINT [CK_LoginPhoneBindings_Dates] CHECK (([VerifiedAt] IS NULL OR [VerifiedAt] >= [CreatedAt]) AND ([RevokedAt] IS NULL OR ([RevokedAt] >= [CreatedAt] AND ([VerifiedAt] IS NULL OR [RevokedAt] >= [VerifiedAt])))),
    CONSTRAINT [CK_LoginPhoneBindings_Phone] CHECK (DATALENGTH([PhoneNumber]) BETWEEN 3 AND 16 AND LEFT([PhoneNumber], 1) = '+' AND SUBSTRING([PhoneNumber], 2, 1) BETWEEN '1' AND '9' AND SUBSTRING([PhoneNumber], 2, 15) NOT LIKE '%[^0-9]%'),
    CONSTRAINT [FK_LoginPhoneBindings_EmployeeSubmissions_ApprovedSubmissionId] FOREIGN KEY ([ApprovedSubmissionId]) REFERENCES [hr].[EmployeeSubmissions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LoginPhoneBindings_UserAccounts_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [auth].[UserAccounts] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[MobileOtpChallenges] (
    [Id] uniqueidentifier NOT NULL,
    [UserAccountId] uniqueidentifier NOT NULL,
    [PhoneBindingId] uniqueidentifier NOT NULL,
    [Purpose] varchar(30) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [SecurityVersion] int NOT NULL,
    [DeviceHash] varchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Platform] nvarchar(40) NOT NULL,
    [DeviceName] nvarchar(150) NOT NULL,
    [Verifier] varchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [IssuedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [MaximumAttempts] int NOT NULL,
    [FailedAttempts] int NOT NULL,
    [ConsumedAt] datetimeoffset NULL,
    [RevokedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_MobileOtpChallenges] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_MobileOtpChallenges_Attempts] CHECK ([SecurityVersion] > 0 AND [MaximumAttempts] > 0 AND [FailedAttempts] BETWEEN 0 AND [MaximumAttempts] AND ([ConsumedAt] IS NULL OR [FailedAttempts] < [MaximumAttempts])),
    CONSTRAINT [CK_MobileOtpChallenges_Dates] CHECK ([ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt])) AND ([RevokedAt] IS NULL OR ([RevokedAt] >= [IssuedAt] AND ([ConsumedAt] IS NULL OR [RevokedAt] >= [ConsumedAt])))),
    CONSTRAINT [CK_MobileOtpChallenges_Purpose] CHECK ([Purpose] = 'MobileLogin'),
    CONSTRAINT [FK_MobileOtpChallenges_LoginPhoneBindings_UserAccountId_PhoneBindingId] FOREIGN KEY ([UserAccountId], [PhoneBindingId]) REFERENCES [auth].[LoginPhoneBindings] ([UserAccountId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [auth].[OtpDeliveries] (
    [Id] uniqueidentifier NOT NULL,
    [ChallengeId] uniqueidentifier NOT NULL,
    [ProtectedPayload] varchar(8000) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [Attempts] int NOT NULL,
    [Status] int NOT NULL,
    [ClaimedAt] datetimeoffset NULL,
    [LeaseUntil] datetimeoffset NULL,
    [SubmittedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_OtpDeliveries] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_OtpDeliveries_Claim] CHECK (([Attempts] = 0 AND [ClaimedAt] IS NULL AND [LeaseUntil] IS NULL) OR ([Attempts] = 1 AND [ClaimedAt] IS NOT NULL AND [LeaseUntil] IS NOT NULL AND [ClaimedAt] >= [CreatedAt] AND [ClaimedAt] < [ExpiresAt] AND [LeaseUntil] > [ClaimedAt] AND [LeaseUntil] <= [ExpiresAt])),
    CONSTRAINT [CK_OtpDeliveries_Lifetime] CHECK ([ExpiresAt] > [CreatedAt]),
    CONSTRAINT [CK_OtpDeliveries_Payload] CHECK (([Status] IN (0,4) AND [ProtectedPayload] IS NOT NULL AND DATALENGTH([ProtectedPayload]) > 0) OR ([Status] IN (1,2,3) AND [ProtectedPayload] IS NULL)),
    CONSTRAINT [CK_OtpDeliveries_Status] CHECK ([Status] IN (0,1,2,3,4) AND ([Status] <> 0 OR [Attempts] = 0) AND ([Status] NOT IN (1,4) OR [Attempts] = 1)),
    CONSTRAINT [CK_OtpDeliveries_Submitted] CHECK (([Status] = 1 AND [SubmittedAt] IS NOT NULL AND [ClaimedAt] IS NOT NULL AND [LeaseUntil] IS NOT NULL AND [SubmittedAt] >= [ClaimedAt] AND [SubmittedAt] < [LeaseUntil] AND [SubmittedAt] < [ExpiresAt]) OR ([Status] <> 1 AND [SubmittedAt] IS NULL)),
    CONSTRAINT [FK_OtpDeliveries_MobileOtpChallenges_ChallengeId] FOREIGN KEY ([ChallengeId]) REFERENCES [auth].[MobileOtpChallenges] ([Id]) ON DELETE NO ACTION
);

ALTER TABLE [auth].[UserAccounts] ADD CONSTRAINT [CK_UserAccounts_Active] CHECK ([Status] <> 3 OR (([PasswordHash] IS NOT NULL OR [PhoneVerifiedAt] IS NOT NULL) AND [ActivatedAt] IS NOT NULL AND [InitialRoleAssignmentId] IS NOT NULL));

ALTER TABLE [auth].[UserAccounts] ADD CONSTRAINT [CK_UserAccounts_PhoneVerified] CHECK ([PhoneVerifiedAt] IS NULL OR ([ActivatedAt] IS NOT NULL AND [PhoneVerifiedAt] >= [CreatedAt]));

ALTER TABLE [auth].[UserAccounts] ADD CONSTRAINT [CK_UserAccounts_WebAccess] CHECK ([WebAccessEnabled] = 0 OR [PasswordHash] IS NOT NULL);

ALTER TABLE [hr].[EmployeeSubmissions] ADD CONSTRAINT [CK_EmployeeSubmissions_MobileEnrollment] CHECK (([MobileAccessRequested] = 0 AND [ApprovedLoginPhoneNumber] IS NULL) OR ([MobileAccessRequested] = 1 AND [ApprovedLoginPhoneNumber] IS NOT NULL));

ALTER TABLE [hr].[Employees] ADD CONSTRAINT [CK_Employees_MobileEnrollment] CHECK (([MobileAccessRequested] = 0 AND [ApprovedLoginPhoneNumber] IS NULL) OR ([MobileAccessRequested] = 1 AND [ApprovedLoginPhoneNumber] IS NOT NULL));

CREATE INDEX [IX_AuthRateBuckets_WindowEndsAt] ON [auth].[AuthRateBuckets] ([WindowEndsAt]);

CREATE INDEX [IX_LoginPhoneBindings_ApprovedSubmissionId] ON [auth].[LoginPhoneBindings] ([ApprovedSubmissionId]);

CREATE UNIQUE INDEX [IX_LoginPhoneBindings_PhoneNumber] ON [auth].[LoginPhoneBindings] ([PhoneNumber]) WHERE [RevokedAt] IS NULL;

CREATE UNIQUE INDEX [IX_LoginPhoneBindings_UserAccountId] ON [auth].[LoginPhoneBindings] ([UserAccountId]) WHERE [RevokedAt] IS NULL;

CREATE INDEX [IX_MobileOtpChallenges_PhoneBindingId_ExpiresAt] ON [auth].[MobileOtpChallenges] ([PhoneBindingId], [ExpiresAt]);

CREATE INDEX [IX_MobileOtpChallenges_UserAccountId_IssuedAt] ON [auth].[MobileOtpChallenges] ([UserAccountId], [IssuedAt]);

CREATE INDEX [IX_MobileOtpChallenges_UserAccountId_PhoneBindingId] ON [auth].[MobileOtpChallenges] ([UserAccountId], [PhoneBindingId]);

CREATE UNIQUE INDEX [IX_OtpDeliveries_ChallengeId] ON [auth].[OtpDeliveries] ([ChallengeId]);

CREATE INDEX [IX_OtpDeliveries_Status_ExpiresAt_CreatedAt] ON [auth].[OtpDeliveries] ([Status], [ExpiresAt], [CreatedAt]);

CREATE INDEX [IX_OtpDeliveries_Status_LeaseUntil] ON [auth].[OtpDeliveries] ([Status], [LeaseUntil]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261006144249_AddSharedMobileAuthentication', N'10.0.12');

COMMIT;
GO
