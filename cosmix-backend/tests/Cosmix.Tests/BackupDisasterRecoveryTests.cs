using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Cosmix.Tests;

public sealed class RecoverySqlFactAttribute : FactAttribute
{
    public RecoverySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMIX_DR_SQL_CONNECTION")))
            Skip = "Requires an explicitly selected local SQL recovery-test instance.";
    }
}

[Trait("Category", "SqlIntegration")]
public sealed class BackupDisasterRecoveryTests
{
    [RecoverySqlFact]
    public async Task Database_backup_and_recovery_drill_succeeds_and_corrupt_backup_is_rejected()
    {
        var selected = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COSMIX_DR_SQL_CONNECTION")!);
        Assert.StartsWith("(localdb)\\", selected.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("master", selected.InitialCatalog, ignoreCase: true);
        var sourceDb = $"Cosmix_DR_Source_{Guid.NewGuid():N}";
        var drillDb = $"Cosmix_DR_Drill_{Guid.NewGuid():N}";
        var tempDir = Path.Combine(Path.GetTempPath(), "Cosmix_DR_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var backupFile = Path.Combine(tempDir, "cosmix_payroll_backup.bak");
        var corruptFile = Path.Combine(tempDir, "cosmix_payroll_corrupt.bak");
        var drillMdf = Path.Combine(tempDir, $"{drillDb}.mdf");
        var drillLdf = Path.Combine(tempDir, $"{drillDb}_log.ldf");

        var masterConn = selected.ConnectionString;
        var sourceCreated = false;
        var restoreCreated = false;

        try
        {
            await using var conn = new SqlConnection(masterConn);
            await conn.OpenAsync();

            // Own every database in the drill; never read a business or historical acceptance database.
            await using (var create = new SqlCommand($"CREATE DATABASE [{sourceDb}]", conn))
            {
                await create.ExecuteNonQueryAsync();
                sourceCreated = true;
            }
            await using (var seed = new SqlCommand($"CREATE TABLE [{sourceDb}].dbo.RecoveryProof (Id int PRIMARY KEY, Amount decimal(18,2) NOT NULL); INSERT INTO [{sourceDb}].dbo.RecoveryProof VALUES (1,123.45)", conn))
            {
                await seed.ExecuteNonQueryAsync();
            }

            // 1. Create consistent SQL Server backup with CHECKSUM
            var backupSw = Stopwatch.StartNew();
            await using var backupCmd = new SqlCommand(
                $"BACKUP DATABASE [{sourceDb}] TO DISK = '{SqlText(backupFile)}' WITH COPY_ONLY, INIT, CHECKSUM, FORMAT, STATS = 10", conn);
            await backupCmd.ExecuteNonQueryAsync();
            backupSw.Stop();

            Assert.True(File.Exists(backupFile), "Backup file was not created.");
            var backupSize = new FileInfo(backupFile).Length;
            Assert.True(backupSize > 0, "Backup file is empty.");

            // 2. Compute SHA-256 integrity hash
            string backupHash;
            using (var stream = File.OpenRead(backupFile))
            {
                backupHash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            }
            Assert.Matches("^[A-F0-9]{64}$", backupHash);

            // 3. Inspect backup headers via RESTORE HEADERONLY & VERIFYONLY
            await using (var verifyCmd = new SqlCommand($"RESTORE VERIFYONLY FROM DISK = '{SqlText(backupFile)}' WITH CHECKSUM", conn))
            {
                await verifyCmd.ExecuteNonQueryAsync();
            }

            // Read logical file names
            string logicalData = sourceDb, logicalLog = sourceDb + "_log";
            await using (var fileListCmd = new SqlCommand($"RESTORE FILELISTONLY FROM DISK = '{SqlText(backupFile)}'", conn))
            await using (var reader = await fileListCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var logicalName = reader.GetString(0);
                    var type = reader.GetString(2);
                    if (type == "D") logicalData = logicalName;
                    else if (type == "L") logicalLog = logicalName;
                }
            }

            // 4. Execute Restoration to isolated Drill Catalog
            var restoreSw = Stopwatch.StartNew();
            var restoreSql = $@"
                RESTORE DATABASE [{drillDb}] 
                FROM DISK = '{SqlText(backupFile)}' 
                WITH MOVE '{SqlText(logicalData)}' TO '{SqlText(drillMdf)}',
                     MOVE '{SqlText(logicalLog)}' TO '{SqlText(drillLdf)}',
                     RECOVERY, CHECKSUM";

            await using (var restoreCmd = new SqlCommand(restoreSql, conn))
            {
                await restoreCmd.ExecuteNonQueryAsync();
                restoreCreated = true;
            }

            await using (var restored = new SqlCommand($"SELECT Amount FROM [{drillDb}].dbo.RecoveryProof WHERE Id=1", conn))
            {
                Assert.Equal(123.45m, (decimal)(await restored.ExecuteScalarAsync())!);
            }
            restoreSw.Stop();

            // 5. Verify database integrity of restored catalog via DBCC CHECKDB
            await using (var checkDbCmd = new SqlCommand($"DBCC CHECKDB ([{drillDb}]) WITH NO_INFOMSGS, ALL_ERRORMSGS", conn))
            {
                await checkDbCmd.ExecuteNonQueryAsync();
            }

            // 6. Test Corrupt Backup Detection:
            // Create a damaged backup file by zeroing out intermediate bytes
            File.Copy(backupFile, corruptFile);
            using (var fs = new FileStream(corruptFile, FileMode.Open, FileAccess.ReadWrite))
            {
                fs.Seek(4096, SeekOrigin.Begin);
                fs.Write(new byte[8192]); // Overwrite header/page data with zeroes
            }

            // Attempting to verify or restore corrupt backup MUST fail with SqlException
            var corruptEx = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await using var badCmd = new SqlCommand($"RESTORE VERIFYONLY FROM DISK = '{SqlText(corruptFile)}' WITH CHECKSUM", conn);
                await badCmd.ExecuteNonQueryAsync();
            });
            Assert.True(corruptEx.Number is 3013 or 3203 or 3241 or 3187 or 8928, 
                $"Expected corruption-related SQL error, got error number {corruptEx.Number}: {corruptEx.Message}");

            Trace.WriteLine($"=== Disaster Recovery Drill Summary ===");
            Trace.WriteLine($"Source Database: {sourceDb}");
            Trace.WriteLine($"Backup Duration: {backupSw.ElapsedMilliseconds} ms, File Size: {backupSize / 1024} KB");
            Trace.WriteLine($"Backup SHA-256: {backupHash}");
            Trace.WriteLine($"Restore Duration (RTO): {restoreSw.ElapsedMilliseconds} ms");
            Trace.WriteLine($"Integrity Check: DBCC CHECKDB PASSED cleanly.");
            Trace.WriteLine($"Corrupt Backup Rejected: Confirmed (Error {corruptEx.Number}).");
        }
        finally
        {
            // Cleanup drill database and files
            try
            {
                await using var cleanupConn = new SqlConnection(masterConn);
                await cleanupConn.OpenAsync();
                foreach (var name in new[] { restoreCreated ? drillDb : null, sourceCreated ? sourceDb : null }.OfType<string>())
                {
                    await using var dropCmd = new SqlCommand(
                        $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];", cleanupConn);
                    await dropCmd.ExecuteNonQueryAsync();
                }
            }
            catch { }

            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
            catch { }
        }
    }

    private static string SqlText(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
