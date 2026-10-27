using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class PersistFinancialPostingMetadataAndEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PostingDate",
                schema: "hr",
                table: "PayrollJournalEntries",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountingPeriodId",
                schema: "hr",
                table: "PayrollJournalEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostingProfileId",
                schema: "hr",
                table: "PayrollJournalEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                schema: "hr",
                table: "PayrollJournalEntries",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                schema: "hr",
                table: "PayrollObligations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodStart",
                schema: "hr",
                table: "PayrollObligations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodEndExclusive",
                schema: "hr",
                table: "PayrollObligations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeEntitlementsJson",
                schema: "hr",
                table: "PayrollObligations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalEntries_PostingDate",
                schema: "hr",
                table: "PayrollJournalEntries",
                column: "PostingDate");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalEntries_AccountingPeriodId",
                schema: "hr",
                table: "PayrollJournalEntries",
                column: "AccountingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalEntries_PostingProfileId",
                schema: "hr",
                table: "PayrollJournalEntries",
                column: "PostingProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalEntries_SourceKey",
                schema: "hr",
                table: "PayrollJournalEntries",
                column: "SourceKey");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollObligations_SourceFingerprint",
                schema: "hr",
                table: "PayrollObligations",
                column: "SourceFingerprint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollJournalEntries_PostingDate",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollJournalEntries_AccountingPeriodId",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollJournalEntries_PostingProfileId",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollJournalEntries_SourceKey",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollObligations_SourceFingerprint",
                schema: "hr",
                table: "PayrollObligations");

            migrationBuilder.DropColumn(
                name: "PostingDate",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropColumn(
                name: "AccountingPeriodId",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropColumn(
                name: "PostingProfileId",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                schema: "hr",
                table: "PayrollObligations");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "hr",
                table: "PayrollObligations");

            migrationBuilder.DropColumn(
                name: "PeriodEndExclusive",
                schema: "hr",
                table: "PayrollObligations");

            migrationBuilder.DropColumn(
                name: "EmployeeEntitlementsJson",
                schema: "hr",
                table: "PayrollObligations");
        }
    }
}
