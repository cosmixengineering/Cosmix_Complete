using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeChangeJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeChanges",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Section = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BeforePrivateReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AfterPrivateReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeChanges", x => x.Id);
                    table.CheckConstraint("CK_EmployeeChanges_Approval", "[Section] = 1 OR [ApprovalRequestId] IS NOT NULL");
                    table.CheckConstraint("CK_EmployeeChanges_Revision", "[Revision] > 0");
                    table.CheckConstraint("CK_EmployeeChanges_Section", "[Section] IN (1, 2, 3, 4, 5, 6)");
                    table.ForeignKey(
                        name: "FK_EmployeeChanges_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "workflow",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeChanges_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeChanges_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeChanges",
                column: "ApprovalRequestId",
                unique: true,
                filter: "[ApprovalRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeChanges_EmployeeId_Section_Revision",
                schema: "hr",
                table: "EmployeeChanges",
                columns: new[] { "EmployeeId", "Section", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET XACT_ABORT ON;
                IF EXISTS (SELECT 1 FROM [hr].[EmployeeChanges])
                    THROW 51000, 'Employee change history exists. A reviewed recovery plan is required; do not discard recorded changes.', 1;
                """);
            migrationBuilder.DropTable(
                name: "EmployeeChanges",
                schema: "hr");
        }
    }
}
