using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class CompleteEmployeeHiringApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "BranchId",
                schema: "hr",
                table: "EmployeeAssignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [hr].[EmployeeAssignments] WHERE [BranchId] IS NULL)
                    THROW 51002, 'Cannot require offices while Site Staff assignments have no office. Use a reviewed forward migration.', 1;
                """);
            migrationBuilder.AlterColumn<Guid>(
                name: "BranchId",
                schema: "hr",
                table: "EmployeeAssignments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
