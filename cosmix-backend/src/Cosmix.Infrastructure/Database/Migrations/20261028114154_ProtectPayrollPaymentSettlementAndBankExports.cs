using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ProtectPayrollPaymentSettlementAndBankExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankExportId",
                schema: "hr",
                table: "SalaryPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationProofReference",
                schema: "hr",
                table: "SalaryPayments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalApprovalProofReference",
                schema: "hr",
                table: "SalaryPayments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReversalIsFailure",
                schema: "hr",
                table: "SalaryPayments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalJournalEntryId",
                schema: "hr",
                table: "SalaryPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                schema: "hr",
                table: "SalaryPayments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalRequestProofReference",
                schema: "hr",
                table: "SalaryPayments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversalRequestedAt",
                schema: "hr",
                table: "SalaryPayments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalRequestedByUserId",
                schema: "hr",
                table: "SalaryPayments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAt",
                schema: "hr",
                table: "SalaryPayments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversedByUserId",
                schema: "hr",
                table: "SalaryPayments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "hr",
                table: "SalaryPayments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "PayrollBankFormatProfiles",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FormatVersion = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 15000, nullable: false),
                    TemplateProofReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApprovalProofReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollBankFormatProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollBankExports",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormatProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PrivateFileReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PrivateManifestReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PaymentCount = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollBankExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBankExports_PayrollBankFormatProfiles_FormatProfileId",
                        column: x => x.FormatProfileId,
                        principalSchema: "hr",
                        principalTable: "PayrollBankFormatProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollBankExports_PayrollObligations_ObligationId",
                        column: x => x.ObligationId,
                        principalSchema: "hr",
                        principalTable: "PayrollObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollBankExportRenewals",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PrivateProofReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollBankExportRenewals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBankExportRenewals_PayrollBankExports_ExportId",
                        column: x => x.ExportId,
                        principalSchema: "hr",
                        principalTable: "PayrollBankExports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryPayments_BankExportId",
                schema: "hr",
                table: "SalaryPayments",
                column: "BankExportId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryPayments_ReversalJournalEntryId",
                schema: "hr",
                table: "SalaryPayments",
                column: "ReversalJournalEntryId",
                unique: true,
                filter: "[ReversalJournalEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBankExportRenewals_ExportId_CreatedAt",
                schema: "hr",
                table: "PayrollBankExportRenewals",
                columns: new[] { "ExportId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBankExports_FormatProfileId",
                schema: "hr",
                table: "PayrollBankExports",
                column: "FormatProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBankExports_ObligationId",
                schema: "hr",
                table: "PayrollBankExports",
                column: "ObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBankFormatProfiles_Name_FormatVersion",
                schema: "hr",
                table: "PayrollBankFormatProfiles",
                columns: new[] { "Name", "FormatVersion" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SalaryPayments_PayrollBankExports_BankExportId",
                schema: "hr",
                table: "SalaryPayments",
                column: "BankExportId",
                principalSchema: "hr",
                principalTable: "PayrollBankExports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalaryPayments_PayrollJournalEntries_ReversalJournalEntryId",
                schema: "hr",
                table: "SalaryPayments",
                column: "ReversalJournalEntryId",
                principalSchema: "hr",
                principalTable: "PayrollJournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalaryPayments_PayrollBankExports_BankExportId",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SalaryPayments_PayrollJournalEntries_ReversalJournalEntryId",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropTable(
                name: "PayrollBankExportRenewals",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PayrollBankExports",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PayrollBankFormatProfiles",
                schema: "hr");

            migrationBuilder.DropIndex(
                name: "IX_SalaryPayments_BankExportId",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropIndex(
                name: "IX_SalaryPayments_ReversalJournalEntryId",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "BankExportId",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "ConfirmationProofReference",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "ReversalApprovalProofReference",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "ReversalIsFailure",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "ReversalJournalEntryId",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "ReversalRequestProofReference",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "ReversalRequestedAt",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "ReversalRequestedByUserId",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "ReversedByUserId",
                schema: "hr",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "hr",
                table: "SalaryPayments");
        }
    }
}
