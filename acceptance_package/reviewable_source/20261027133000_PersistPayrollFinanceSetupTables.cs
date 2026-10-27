using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class PersistPayrollFinanceSetupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollLedgerAccounts",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLedgerAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollAccountingPeriods",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEndExclusive = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollAccountingPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPostingProfiles",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveToExclusive = table.Column<DateOnly>(type: "date", nullable: true),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 30000, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPostingProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeStatutoryCoverages",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveToExclusive = table.Column<DateOnly>(type: "date", nullable: true),
                    Schemes = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EvidenceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PrivateProofReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeStatutoryCoverages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLedgerAccounts_Code",
                schema: "hr",
                table: "PayrollLedgerAccounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLedgerAccounts_Kind",
                schema: "hr",
                table: "PayrollLedgerAccounts",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLedgerAccounts_IsActive",
                schema: "hr",
                table: "PayrollLedgerAccounts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAccountingPeriods_Currency_PeriodStart_PeriodEndExclusive",
                schema: "hr",
                table: "PayrollAccountingPeriods",
                columns: new[] { "Currency", "PeriodStart", "PeriodEndExclusive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAccountingPeriods_State",
                schema: "hr",
                table: "PayrollAccountingPeriods",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPostingProfiles_Currency_EffectiveFrom",
                schema: "hr",
                table: "PayrollPostingProfiles",
                columns: new[] { "Currency", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPostingProfiles_State",
                schema: "hr",
                table: "PayrollPostingProfiles",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeStatutoryCoverages_EmployeeId_EffectiveFrom",
                schema: "hr",
                table: "EmployeeStatutoryCoverages",
                columns: new[] { "EmployeeId", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeStatutoryCoverages",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PayrollPostingProfiles",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PayrollAccountingPeriods",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PayrollLedgerAccounts",
                schema: "hr");
        }
    }
}
