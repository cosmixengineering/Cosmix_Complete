BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM [hr].[EmployeeDocuments]) THROW 51002, 'Existing employee documents require an approved privacy-classification migration before this upgrade.', 1;

ALTER TABLE [hr].[EmployeeDocuments] ADD [Privacy] int NOT NULL DEFAULT 0;

CREATE TABLE [hr].[EmployeeHiringDrafts] (
    [EmployeeId] uniqueidentifier NOT NULL,
    [DepartmentId] uniqueidentifier NULL,
    [DesignationId] uniqueidentifier NULL,
    [StaffCategoryId] uniqueidentifier NULL,
    [EmploymentTypeId] uniqueidentifier NULL,
    [BranchId] uniqueidentifier NULL,
    [ReportingManagerEmployeeId] uniqueidentifier NULL,
    [ProposedShiftId] uniqueidentifier NULL,
    [ProfileReference] nvarchar(500) NULL,
    [FinancialReference] nvarchar(500) NULL,
    [MedicalReference] nvarchar(500) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeHiringDrafts] PRIMARY KEY ([EmployeeId]),
    CONSTRAINT [CK_EmployeeHiringDrafts_Manager] CHECK ([ReportingManagerEmployeeId] IS NULL OR [ReportingManagerEmployeeId] <> [EmployeeId]),
    CONSTRAINT [FK_EmployeeHiringDrafts_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [hr].[Branches] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [hr].[Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_Designations_DesignationId] FOREIGN KEY ([DesignationId]) REFERENCES [hr].[Designations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_Employees_ReportingManagerEmployeeId] FOREIGN KEY ([ReportingManagerEmployeeId]) REFERENCES [hr].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_EmploymentTypes_EmploymentTypeId] FOREIGN KEY ([EmploymentTypeId]) REFERENCES [hr].[EmploymentTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_Shifts_ProposedShiftId] FOREIGN KEY ([ProposedShiftId]) REFERENCES [attendance].[Shifts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeHiringDrafts_StaffCategories_StaffCategoryId] FOREIGN KEY ([StaffCategoryId]) REFERENCES [hr].[StaffCategories] ([Id]) ON DELETE NO ACTION
);

ALTER TABLE [hr].[EmployeeDocuments] ADD CONSTRAINT [CK_EmployeeDocuments_Privacy] CHECK ([Privacy] IN (0, 1));

CREATE INDEX [IX_EmployeeHiringDrafts_BranchId] ON [hr].[EmployeeHiringDrafts] ([BranchId]);

CREATE INDEX [IX_EmployeeHiringDrafts_DepartmentId] ON [hr].[EmployeeHiringDrafts] ([DepartmentId]);

CREATE INDEX [IX_EmployeeHiringDrafts_DesignationId] ON [hr].[EmployeeHiringDrafts] ([DesignationId]);

CREATE INDEX [IX_EmployeeHiringDrafts_EmploymentTypeId] ON [hr].[EmployeeHiringDrafts] ([EmploymentTypeId]);

CREATE INDEX [IX_EmployeeHiringDrafts_ProposedShiftId] ON [hr].[EmployeeHiringDrafts] ([ProposedShiftId]);

CREATE INDEX [IX_EmployeeHiringDrafts_ReportingManagerEmployeeId] ON [hr].[EmployeeHiringDrafts] ([ReportingManagerEmployeeId]);

CREATE INDEX [IX_EmployeeHiringDrafts_StaffCategoryId] ON [hr].[EmployeeHiringDrafts] ([StaffCategoryId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007141249_AddEmployeeHiringDraftsAndDocumentPrivacy', N'10.0.12');

COMMIT;
GO
