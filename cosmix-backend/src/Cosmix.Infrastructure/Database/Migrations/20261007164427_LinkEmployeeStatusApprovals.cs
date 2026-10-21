using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class LinkEmployeeStatusApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "EmployeeStatusChanges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeStatusChanges_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeStatusChanges",
                column: "ApprovalRequestId",
                unique: true,
                filter: "[ApprovalRequestId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeStatusChanges_ApprovalRequests_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeStatusChanges",
                column: "ApprovalRequestId",
                principalSchema: "workflow",
                principalTable: "ApprovalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [hr].[EmployeeStatusChanges] WHERE [ApprovalRequestId] IS NOT NULL)
                    THROW 51002, 'Cannot remove recorded employee status approval links. Use a reviewed forward migration.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeStatusChanges_ApprovalRequests_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeStatusChanges");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeStatusChanges_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeStatusChanges");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "EmployeeStatusChanges");
        }
    }
}
