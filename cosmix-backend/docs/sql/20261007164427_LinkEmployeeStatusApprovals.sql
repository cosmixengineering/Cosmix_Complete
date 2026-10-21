BEGIN TRANSACTION;
ALTER TABLE [hr].[EmployeeStatusChanges] ADD [ApprovalRequestId] uniqueidentifier NULL;

CREATE UNIQUE INDEX [IX_EmployeeStatusChanges_ApprovalRequestId] ON [hr].[EmployeeStatusChanges] ([ApprovalRequestId]) WHERE [ApprovalRequestId] IS NOT NULL;

ALTER TABLE [hr].[EmployeeStatusChanges] ADD CONSTRAINT [FK_EmployeeStatusChanges_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [workflow].[ApprovalRequests] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007164427_LinkEmployeeStatusApprovals', N'10.0.12');

COMMIT;
GO
