using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeHiringDraftsAndDocumentPrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Older metadata has no medical classification. Do not silently expose it as ordinary HR files.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [hr].[EmployeeDocuments]) THROW 51002, 'Existing employee documents require an approved privacy-classification migration before this upgrade.', 1;");
            migrationBuilder.AddColumn<int>(
                name: "Privacy",
                schema: "hr",
                table: "EmployeeDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EmployeeHiringDrafts",
                schema: "hr",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DesignationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StaffCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmploymentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportingManagerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProposedShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProfileReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FinancialReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MedicalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeHiringDrafts", x => x.EmployeeId);
                    table.CheckConstraint("CK_EmployeeHiringDrafts_Manager", "[ReportingManagerEmployeeId] IS NULL OR [ReportingManagerEmployeeId] <> [EmployeeId]");
                    table.ForeignKey(
                        name: "FK_EmployeeHiringDrafts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "hr",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeHiringDrafts_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "hr",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeHiringDrafts_Designations_DesignationId",
                        column: x => x.DesignationId,
                        principalSchema: "hr",
                        principalTable: "Designations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeHiringDrafts_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeHiringDrafts_Employees_ReportingManagerEmployeeId",
                        column: x => x.ReportingManagerEmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeHiringDrafts_EmploymentTypes_EmploymentTypeId",
                        column: x => x.EmploymentTypeId,
                        principalSchema: "hr",
                        principalTable: "EmploymentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeHiringDrafts_Shifts_ProposedShiftId",
                        column: x => x.ProposedShiftId,
                        principalSchema: "attendance",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeHiringDrafts_StaffCategories_StaffCategoryId",
                        column: x => x.StaffCategoryId,
                        principalSchema: "hr",
                        principalTable: "StaffCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeDocuments_Privacy",
                schema: "hr",
                table: "EmployeeDocuments",
                sql: "[Privacy] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHiringDrafts_BranchId",
                schema: "hr",
                table: "EmployeeHiringDrafts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHiringDrafts_DepartmentId",
                schema: "hr",
                table: "EmployeeHiringDrafts",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHiringDrafts_DesignationId",
                schema: "hr",
                table: "EmployeeHiringDrafts",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHiringDrafts_EmploymentTypeId",
                schema: "hr",
                table: "EmployeeHiringDrafts",
                column: "EmploymentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHiringDrafts_ProposedShiftId",
                schema: "hr",
                table: "EmployeeHiringDrafts",
                column: "ProposedShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHiringDrafts_ReportingManagerEmployeeId",
                schema: "hr",
                table: "EmployeeHiringDrafts",
                column: "ReportingManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHiringDrafts_StaffCategoryId",
                schema: "hr",
                table: "EmployeeHiringDrafts",
                column: "StaffCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeHiringDrafts",
                schema: "hr");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeDocuments_Privacy",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "Privacy",
                schema: "hr",
                table: "EmployeeDocuments");
        }
    }
}
