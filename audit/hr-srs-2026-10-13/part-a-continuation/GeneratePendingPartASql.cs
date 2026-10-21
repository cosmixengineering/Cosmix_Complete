#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Security.Cryptography;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
const string from="20261008074438_AddEmployeeChangeJournal",to="20261013121357_CompleteHrFoundationPartA";
using var db=new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
 .UseSqlServer("Server=offline.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true")
 .AddInterceptors(new NoConnections()).Options);
var assembly=db.GetService<IMigrationsAssembly>();
var graph=assembly.Migrations.Keys.ToArray();
if(Array.IndexOf(graph,from)!=11 || Array.IndexOf(graph,to)!=22)throw new Exception("Frozen expected migration prefix changed");
var script=db.GetService<IMigrator>().GenerateScript(from,to,MigrationsSqlGenerationOptions.Idempotent);
if(script.Contains("DROP TABLE") || script.Contains("DELETE FROM"))throw new Exception("Pending range has destructive source operation requiring separate review");
var path=Path.Combine("audit","hr-srs-2026-10-13","part-a-continuation","Current12-to-FrozenA23.sql");
File.WriteAllText(path,script,new System.Text.UTF8Encoding(false));
Console.WriteLine($"Prepared 11 pending migrations from observed12 to frozen23; no connection. SHA256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}");
var prefix=graph.Take(23).Select(id=>new {MigrationId=id,ProductVersion=assembly.CreateMigration(assembly.Migrations[id],db.Database.ProviderName!).TargetModel.GetProductVersion()}).ToArray();
if(prefix.Any(x=>string.IsNullOrWhiteSpace(x.ProductVersion)))throw new Exception("Missing migration target version");
var manifest=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cosmix","PartAContinuation","Database20261013","FrozenA23-Migrations.json");
File.WriteAllText(manifest,System.Text.Json.JsonSerializer.Serialize(prefix,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}),new System.Text.UTF8Encoding(false));
Console.WriteLine($"Frozen prefix manifest23 prepared; SHA256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest)))}");
sealed class NoConnections:DbConnectionInterceptor {
 public override InterceptionResult ConnectionOpening(DbConnection c,ConnectionEventData e,InterceptionResult r)=>throw new Exception("Offline generation cannot open SQL");
 public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c,ConnectionEventData e,InterceptionResult r,CancellationToken ct=default)=>throw new Exception("Offline generation cannot open SQL");
}
