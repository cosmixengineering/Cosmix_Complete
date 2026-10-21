using System.Data.Common;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

// Offline model comparison only. It cannot open a SQL connection or write a migration.
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=continuation-model.invalid;Database=NeverConnect;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var assembly = db.GetService<IMigrationsAssembly>();
var latest = assembly.Migrations.Last();
Console.WriteLine($"Compiled migration count: {assembly.Migrations.Count}; latest: {latest.Key}");
var migration = assembly.CreateMigration(latest.Value, db.Database.ProviderName!);
var before = db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, designTime: true);
var after = db.GetService<IDesignTimeModel>().Model;
var delta = db.GetService<IMigrationsModelDiffer>().GetDifferences(before.GetRelationalModel(), after.GetRelationalModel());
foreach (var change in delta)
{
    Console.WriteLine(change switch
    {
        CreateTableOperation x => $"CREATE TABLE {x.Schema}.{x.Name}; columns: {string.Join(", ", x.Columns.Select(c => $"{c.Name}:{c.ColumnType}:{(c.IsNullable ? "nullable" : "required")}"))}",
        AddColumnOperation x => $"ADD COLUMN {x.Schema}.{x.Table}.{x.Name}:{x.ColumnType}:{(x.IsNullable ? "nullable" : "required")}",
        CreateIndexOperation x => $"CREATE {(x.IsUnique ? "UNIQUE " : "")}INDEX {x.Schema}.{x.Table}.{x.Name}({string.Join(",", x.Columns)})",
        AddForeignKeyOperation x => $"ADD FK {x.Schema}.{x.Table}.{x.Name} -> {x.PrincipalSchema}.{x.PrincipalTable}",
        _ => $"REVIEW REQUIRED: {change.GetType().Name}"
    });
}
if (guard.Attempts != 0) throw new Exception("Unexpected SQL connection attempt");
if (delta.Any(x => x is DropTableOperation or DropColumnOperation or InsertDataOperation or UpdateDataOperation or DeleteDataOperation or SqlOperation))
    throw new Exception("Destructive, seed or raw SQL operations require independent review; no writes performed");
Console.WriteLine($"Model operations: {delta.Count}. No host, SQL connection, schema, fixture, migration or private-file writes.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts { get; private set; }
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new Exception("SQL forbidden in offline model inspection"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    { Attempts++; throw new Exception("SQL forbidden in offline model inspection"); }
}
