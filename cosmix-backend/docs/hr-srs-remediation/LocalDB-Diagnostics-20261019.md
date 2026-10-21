# LocalDB Connectivity Diagnosis - 19 October 2026

## Final Finding

`Cosmix_Testing` exists and is `ONLINE`, `MULTI_USER`. It is the previously
inspected database, not a replacement. A direct read-only connection to the
already-running Windows-session LocalDB process verified:

| Property | Observed value |
| --- | --- |
| Named instance / executable arguments | `MSSQLLocalDB`, SQL Server 17.0.4025.3 |
| Windows process | 29836, started 2026-10-19 16:58:18 +05:00 |
| Server | `Software-PC1\LOCALDB#F9CAED57` |
| Existing pipe | `np:\\.\pipe\LOCALDB#F9CAED57\tsql\query` |
| Process, SQL login and catalog owner | `COSMIXRND\Huma.Khan` |
| Catalog match count | 1 |
| State / access | `ONLINE` / `MULTI_USER` |
| Database family | `D9C34627-18A2-41CF-A804-821E39516303` |
| Data file | `C:\Users\Huma.Khan\Cosmix_Testing.mdf` |
| Log file | `C:\Users\Huma.Khan\Cosmix_Testing_log.ldf` |

The family and both file paths match the earlier process 24152 / pipe
`LOCALDB#2E40033B`. No business data was needed for this confirmation.
Pipe names and process IDs are observations, not permanent configuration values.

## Diagnostic Evidence

1. The old process log recorded RANU internal idle timeout at 16:46:49, exit code
   291. That shutdown is not evidence that a database was deleted or corrupted.
2. Windows Application events 19244-19248, provider `SQLLocalDB 17.0`, event 528,
   at 16:52:15-16:52:30 identify SSMS process 29224. The reported call is
   `WaitForMultipleObjects`, error 575. Windows defines this as
   `ERROR_APP_INIT_FAILURE`; it does not identify the underlying failing module,
   file or configuration by itself. See [Microsoft error-code reference](https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--500-999-).
3. SSMS and diagnostic tools run as the same Windows account. Nevertheless,
   the tools' named-instance view differs from the normal Windows-session view.
   The tools' named lookup returned process 9388 / pipe `LOCALDB#637C1A7D`, with
   only system catalogs, and subsequently reported Stopped while normal-session
   process 29836 was running. An earlier tools-side start returned process 21700.
4. A read-only file handle for the nominal LocalDB `error.log` resolved to
   `C:\Users\Huma.Khan\AppData\Local\Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\error.log`.
   Registry inspection also found package-LocalCache LocalDB data directories.
   This is direct evidence of the redirected file view. The diagnostic child
   returned no package identity itself; do not infer its identity solely from
   this path. Microsoft documents per-app AppData/registry redirection in
   [packaged desktop app behavior](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes).
5. The user independently reported Running/ready/recovery-complete from normal
   Windows PowerShell. The parent then inspected the existing SQL process command
   line and owner, and connected to its explicit pipe without starting anything.
   The final metadata results above supersede the earlier tools-view-only
   statement that no Cosmix catalog was registered.

## Cause and Limits

The misleading missing-database diagnosis resulted from treating two execution
contexts' named-instance views as interchangeable. The current database is
present and accessible on the normal-session process.

The exact cause of the earlier SSMS error 50 / Windows error 575 remains
unestablished. The user-supplied current log reports a healthy startup. A transient
startup failure is possible, not proven. Do not claim a configuration repair,
database recovery, or corruption fix. No such change was made.

## Operations Performed

- Read instance inventory, process ownership/arguments, file metadata/path
  resolution, registry metadata, SQL logs and Windows Application events.
- Started only the existing tools-visible `MSSQLLocalDB` earlier; a subsequent
  named SQL connection auto-started that same existing instance. Normal engine
  startup maintains its own system files/logs. No manual database-file operation
  or configuration edit was performed. After the context mismatch was recognized,
  final checks used only the already-running normal-session explicit pipe.
- Read `sys.databases`, `sys.database_recovery_status`, `sys.master_files` and
  server/login metadata in `master`. No other business database was substituted.
- No create/delete/reset/attach/detach/restore, migration, manual MDF/LDF change,
  credentials/role change, HR fixture write or payment operation.
- The verified normal-session process was left running. No SQL process was
  stopped by this diagnostic. LocalDB may subsequently stop on its own idle timer.

## Safe Next Step

Continue using normal-session SSMS `(localdb)\MSSQLLocalDB` with Windows
authentication. If startup fails again, retain that session's contemporaneous log
and Application event, then diagnose before changing anything. Do not reset the
instance or attach the existing files to the tools-visible empty catalog.

The C01 acceptance profile still pins the old inspected pipe/PID. This diagnostic
does not silently rewrite that guard or authorize replay of a partially prepared
test. Rebind/review the test target against this exact retained database before a
future bounded authenticated test. No authenticated HR/SQL/concurrency acceptance
was executed in this read-only diagnostic phase.

Microsoft's [LocalDB reference](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb)
documents per-user instances, direct-pipe connectivity and diagnostic log locations.
