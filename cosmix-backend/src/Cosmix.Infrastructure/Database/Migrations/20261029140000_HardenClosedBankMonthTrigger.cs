using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations;

[DbContext(typeof(CosmixDbContext))]
[Migration("20261029140000_HardenClosedBankMonthTrigger")]
public partial class HardenClosedBankMonthTrigger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [hr].[TR_PayrollJournal_ClosedBankMonth]
            ON [hr].[PayrollJournalEntries] AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    JOIN [finance].[BankReconciliationCloses] c WITH (UPDLOCK, HOLDLOCK)
                      ON c.Currency = i.Currency
                     AND (c.AccountCode = i.DebitAccountCode OR c.AccountCode = i.CreditAccountCode)
                     AND (i.PostingDate IS NULL OR c.Month = DATEFROMPARTS(YEAR(i.PostingDate), MONTH(i.PostingDate), 1))
                ) OR EXISTS (
                    SELECT 1 FROM deleted d
                    JOIN [finance].[BankReconciliationCloses] c WITH (UPDLOCK, HOLDLOCK)
                      ON c.Currency = d.Currency
                     AND (c.AccountCode = d.DebitAccountCode OR c.AccountCode = d.CreditAccountCode)
                     AND (d.PostingDate IS NULL OR c.Month = DATEFROMPARTS(YEAR(d.PostingDate), MONTH(d.PostingDate), 1))
                ) THROW 51016, 'The bank month is reconciled and closed.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [hr].[TR_PayrollJournal_ClosedBankMonth]
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
}
