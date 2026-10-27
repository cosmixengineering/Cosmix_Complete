using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class PersistStatutoryEmployeeItemsAndJournalLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                schema: "hr",
                table: "StatutoryContributions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeItemsJson",
                schema: "hr",
                table: "StatutoryContributions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasEmployeeEntries",
                schema: "hr",
                table: "StatutoryContributions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ObligationId",
                schema: "hr",
                table: "StatutoryContributions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StatutoryContributionId",
                schema: "hr",
                table: "PayrollJournalEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scheme",
                schema: "hr",
                table: "PayrollJournalEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryContributions_JournalEntryId",
                schema: "hr",
                table: "StatutoryContributions",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryContributions_ObligationId",
                schema: "hr",
                table: "StatutoryContributions",
                column: "ObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalEntries_StatutoryContributionId",
                schema: "hr",
                table: "PayrollJournalEntries",
                column: "StatutoryContributionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StatutoryContributions_JournalEntryId",
                schema: "hr",
                table: "StatutoryContributions");

            migrationBuilder.DropIndex(
                name: "IX_StatutoryContributions_ObligationId",
                schema: "hr",
                table: "StatutoryContributions");

            migrationBuilder.DropIndex(
                name: "IX_PayrollJournalEntries_StatutoryContributionId",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                schema: "hr",
                table: "StatutoryContributions");

            migrationBuilder.DropColumn(
                name: "EmployeeItemsJson",
                schema: "hr",
                table: "StatutoryContributions");

            migrationBuilder.DropColumn(
                name: "HasEmployeeEntries",
                schema: "hr",
                table: "StatutoryContributions");

            migrationBuilder.DropColumn(
                name: "ObligationId",
                schema: "hr",
                table: "StatutoryContributions");

            migrationBuilder.DropColumn(
                name: "StatutoryContributionId",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropColumn(
                name: "Scheme",
                schema: "hr",
                table: "PayrollJournalEntries");
        }
    }
}
