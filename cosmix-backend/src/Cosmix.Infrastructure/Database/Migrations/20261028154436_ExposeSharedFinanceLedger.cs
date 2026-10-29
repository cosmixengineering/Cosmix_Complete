using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ExposeSharedFinanceLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema("finance");
            migrationBuilder.Sql("""
                CREATE VIEW [finance].[GeneralLedgerEntries]
                AS
                SELECT j.[Id] AS [JournalId], CAST(N'Payroll' AS nvarchar(40)) AS [SourceType],
                    COALESCE(j.[SourceKey], N'LEGACY-' + CONVERT(nvarchar(36),j.[Id])) AS [SourceKey],
                    j.[PostingDate], j.[Currency], legs.[AccountCode], legs.[Debit], legs.[Credit], j.[ObligationId]
                FROM [hr].[PayrollJournalEntries] j
                CROSS APPLY (VALUES
                    (j.[DebitAccountCode], j.[Amount], CAST(0 AS decimal(18,2))),
                    (j.[CreditAccountCode], CAST(0 AS decimal(18,2)), j.[Amount])
                    ) legs([AccountCode], [Debit], [Credit]);
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW [finance].[GeneralLedgerEntries];");

        }
    }
}
