using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBankStatementReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankStatementSnapshots",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProofReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementSnapshots", x => x.Id);
                    table.CheckConstraint("CK_BankStatement_MonthRevision", "DAY([Month]) = 1 AND [Revision] > 0");
                });

            migrationBuilder.CreateTable(
                name: "BankReconciliationProposals",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ProofReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReconciliationProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankReconciliationProposals_BankStatementSnapshots_StatementId",
                        column: x => x.StatementId,
                        principalSchema: "finance",
                        principalTable: "BankStatementSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankReconciliationCloses",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    ProofReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ClosedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClosedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReconciliationCloses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankReconciliationCloses_BankReconciliationProposals_ProposalId",
                        column: x => x.ProposalId,
                        principalSchema: "finance",
                        principalTable: "BankReconciliationProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankReconciliationCloses_BankStatementSnapshots_StatementId",
                        column: x => x.StatementId,
                        principalSchema: "finance",
                        principalTable: "BankStatementSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliationCloses_AccountCode_Currency_Month",
                schema: "finance",
                table: "BankReconciliationCloses",
                columns: new[] { "AccountCode", "Currency", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliationCloses_ProposalId",
                schema: "finance",
                table: "BankReconciliationCloses",
                column: "ProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliationCloses_StatementId",
                schema: "finance",
                table: "BankReconciliationCloses",
                column: "StatementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliationProposals_StatementId_Revision",
                schema: "finance",
                table: "BankReconciliationProposals",
                columns: new[] { "StatementId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementSnapshots_AccountCode_Currency_Month_FileSha256",
                schema: "finance",
                table: "BankStatementSnapshots",
                columns: new[] { "AccountCode", "Currency", "Month", "FileSha256" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementSnapshots_AccountCode_Currency_Month_Revision",
                schema: "finance",
                table: "BankStatementSnapshots",
                columns: new[] { "AccountCode", "Currency", "Month", "Revision" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE TRIGGER [hr].[TR_PayrollJournal_ClosedBankMonth]
                ON [hr].[PayrollJournalEntries] AFTER INSERT AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN [finance].[BankReconciliationCloses] c WITH (UPDLOCK, HOLDLOCK)
                          ON c.Currency = i.Currency
                         AND (c.AccountCode = i.DebitAccountCode OR c.AccountCode = i.CreditAccountCode)
                         AND (i.PostingDate IS NULL OR c.Month = DATEFROMPARTS(YEAR(i.PostingDate), MONTH(i.PostingDate), 1))
                    ) THROW 51016, 'The bank month is reconciled and closed.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER [hr].[TR_PayrollJournal_ClosedBankMonth]");
            migrationBuilder.DropTable(
                name: "BankReconciliationCloses",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "BankReconciliationProposals",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "BankStatementSnapshots",
                schema: "finance");
        }
    }
}
