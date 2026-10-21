BEGIN TRANSACTION;
CREATE TABLE [hr].[EmployeeChanges] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Section] int NOT NULL,
    [Revision] int NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [BeforePrivateReference] nvarchar(1000) NOT NULL,
    [AfterPrivateReference] nvarchar(1000) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [RecordedByUserId] nvarchar(200) NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmployeeChanges_Approval] CHECK ([Section] = 1 OR [ApprovalRequestId] IS NOT NULL),
    CONSTRAINT [CK_EmployeeChanges_Revision] CHECK ([Revision] > 0),
    CONSTRAINT [CK_EmployeeChanges_Section] CHECK ([Section] IN (1, 2, 3, 4, 5, 6)),
    CONSTRAINT [FK_EmployeeChanges_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeChanges_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_EmployeeChanges_ApprovalRequestId] ON [hr].[EmployeeChanges] ([ApprovalRequestId]) WHERE [ApprovalRequestId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_EmployeeChanges_EmployeeId_Section_Revision] ON [hr].[EmployeeChanges] ([EmployeeId], [Section], [Revision]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261008074438_AddEmployeeChangeJournal', N'10.0.12');

COMMIT;
GO
