namespace NOIR.Application.UnitTests.Infrastructure;

public class ErpSchemaMigrationTests
{
    [Fact]
    public void Up_ShouldOnlyAddTablesAndIndexes()
    {
        var migration = new NOIR.Infrastructure.Migrations.App.AddErpModuleSchemas
        {
            ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer"
        };

        migration.UpOperations.ShouldNotBeEmpty();
        migration.UpOperations.ShouldAllBe(operation =>
            operation is Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation
            || operation is Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation);
    }

    [Fact]
    public void Up_ShouldCreateExistingErpModelsIncludingInvoicePaymentStorage()
    {
        var migration = new NOIR.Infrastructure.Migrations.App.AddErpModuleSchemas
        {
            ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer"
        };
        var tables = migration.UpOperations
            .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation>()
            .ToDictionary(table => table.Name);

        tables.Count.ShouldBe(34);
        foreach (var name in new[] { "StockTransfers", "StockIndents", "Accounts", "JournalEntries", "Boqs", "SalesInvoices", "PaymentRecords" })
        {
            tables.ShouldContainKey(name);
            tables[name].Columns.ShouldContain(column => column.Name == "TenantId");
        }

        tables["PaymentRecords"].Columns.Single(column => column.Name == "Method").ClrType.ShouldBe(typeof(string));
    }
}
