BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[EmployeeAssignments]') AND [c].[name] = N'BranchId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [hr].[EmployeeAssignments] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [hr].[EmployeeAssignments] ALTER COLUMN [BranchId] uniqueidentifier NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007151945_CompleteEmployeeHiringApproval', N'10.0.12');

COMMIT;
GO
