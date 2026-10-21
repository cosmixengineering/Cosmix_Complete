<#
Prepared schema phase only. Execute only after the human approves the exact manifest/hash.
No fixture, permission, key, credential, host, worker or provider changes. No reset/downgrade.
An interrupted run keeps its backup/evidence; inspect it rather than blindly rerunning.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ApprovedManifestSha256
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$privateRoot = [IO.Path]::GetFullPath('C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009')
function Assert-RegularPath([string]$Path, [string]$Root, [switch]$ExistingFile) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($Root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Path is outside the reviewed root.' }
    $cursor = $full
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked acceptance paths are not allowed.' }
        }
        $parent = [IO.Path]::GetDirectoryName($cursor)
        if ($parent -eq $cursor) { break }; $cursor = $parent
    }
    if ($ExistingFile) {
        $item = Get-Item -LiteralPath $full -Force
        if ($item.PSIsContainer -or $item.Length -lt 1 -or $item.Length -gt 16777216) { throw 'Reviewed input is not a bounded regular file.' }
    }
    return $full
}
$ManifestPath = Assert-RegularPath $ManifestPath $repository -ExistingFile
if ((Get-FileHash -LiteralPath $ManifestPath -Algorithm SHA256).Hash -ine $ApprovedManifestSha256) { throw 'Manifest differs from the approved hash.' }
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json -Depth 30
if ($manifest.Database -cne 'Cosmix_Testing' -or $manifest.DataSource -cne '(localdb)\MSSQLLocalDB' -or
    $manifest.ExistingRunId -cne 'ACC202610098E9C6465' -or $manifest.ExistingPrivateRoot -cne $privateRoot -or
    $manifest.RunId -cnotmatch '^ATT[0-9A-Z]{12,28}$' -or
    $manifest.BaselineMigration -cnotin @('20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments','20261009083807_AddAttendanceCaptureAndFactualSummaries') -or
    $manifest.TargetMigration -cne '20261012082833_RetainZoneWarningTransitions') {
    throw 'Manifest is outside the exact isolated migration-13-to-17 scope.'
}
$baselineCount = 13
if ($manifest.BaselineMigration -ceq '20261009083807_AddAttendanceCaptureAndFactualSummaries') {
    # Explicit reconciled continuation only: preserve the failed run and its original verified backup.
    if ($manifest.ReconciledPriorRunId -cne 'ATT202610125683251FC7EF' -or
        $manifest.RunId -ceq $manifest.ReconciledPriorRunId -or
        $manifest.MigrationScript -cne 'docs/attendance-completion/Attendance-Resume-15-17.sql') {
        throw 'Migration-14 continuation requires the exact reconciled prior run and remaining script.'
    }
    $priorRoot = Assert-RegularPath (Join-Path $privateRoot ('runs\' + $manifest.ReconciledPriorRunId)) $privateRoot
    $priorManifest = Get-Content -LiteralPath (Assert-RegularPath (Join-Path $priorRoot 'approved-manifest.json') $privateRoot -ExistingFile) -Raw | ConvertFrom-Json
    if ($priorManifest.RunId -cne $manifest.ReconciledPriorRunId -or $priorManifest.Database -cne 'Cosmix_Testing' -or
        $priorManifest.BaselineMigration -cne '20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments' -or
        $priorManifest.TargetMigration -cne $manifest.TargetMigration -or
        (Test-Path -LiteralPath (Join-Path $priorRoot 'schema-result.json')) -or
        (Test-Path -LiteralPath (Join-Path $priorRoot 'web-execution-started.json'))) {
        throw 'Prior run is not the reconciled schema-only failure.'
    }
    $priorBackup = Get-Content -LiteralPath (Assert-RegularPath (Join-Path $priorRoot 'verified-backup.json') $privateRoot -ExistingFile) -Raw | ConvertFrom-Json
    $priorBackupPath = Assert-RegularPath ([string]$priorBackup.Path) $priorRoot
    if ($priorBackup.Database -cne 'Cosmix_Testing' -or
        (Get-FileHash -LiteralPath $priorBackupPath -Algorithm SHA256).Hash -cne $priorBackup.Sha256) {
        throw 'Original verified backup changed.'
    }
    $baselineCount = 14
}
$baselineMigration = [string]$manifest.BaselineMigration # constrained to fixed migration IDs above
$scriptPath = Assert-RegularPath (Join-Path $repository $manifest.MigrationScript) (Join-Path $repository 'docs/attendance-completion') -ExistingFile
if ((Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash -ine $manifest.MigrationScriptSha256) { throw 'Reviewed migration script changed.' }
$oldManifestPath = Assert-RegularPath (Join-Path $privateRoot 'attendance-manifest.json') $privateRoot -ExistingFile
$oldRevocationPath = Assert-RegularPath (Join-Path $privateRoot 'revocation-evidence.json') $privateRoot -ExistingFile
$fixture = Get-Content -LiteralPath $oldManifestPath -Raw | ConvertFrom-Json -Depth 30
$revocation = Get-Content -LiteralPath $oldRevocationPath -Raw | ConvertFrom-Json -Depth 30
if ($fixture.Database -cne $manifest.Database -or $fixture.RunId -cne $manifest.ExistingRunId -or $fixture.Committed -ne $true -or
    $revocation.RunId -cne $manifest.ExistingRunId -or $revocation.Action -cne 'RevokeAdminRosterOnly' -or
    $revocation.OtherGrantsRetained -ne $true) { throw 'Retained acceptance provenance does not match.' }
$revokedId = ([guid]$revocation.AssignmentId).ToString('D')
$revokedAccount = ([guid]$revocation.AccountId).ToString('D')
$revokedRole = ([guid]$revocation.RoleId).ToString('D')
if (@($revokedId, $revokedAccount, $revokedRole) -contains [guid]::Empty.ToString()) { throw 'Retained revocation identity missing.' }
# No caller-provided SQL, connection string, database name or server is executed.
$preflight = @"
SET NOCOUNT ON;
IF DB_NAME() <> N'Cosmix_Testing' THROW 51000, 'Incorrect target.', 1;
IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) <> $baselineCount OR
   (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory) <> N'$baselineMigration'
  THROW 51000, 'Expected exact reviewed migration baseline.', 1;
IF OBJECT_ID(N'attendance.AttendanceCarryForwardAdjustments') IS NOT NULL OR
   COL_LENGTH(N'attendance.AttendancePeriodLockEvents',N'ProtectedEvidenceReference') IS NOT NULL
  THROW 51000, 'Unexpected partial migration15 schema; reconcile before continuation.', 1;
IF (SELECT COUNT(*) FROM auth.UserAccounts) <> 3 OR (SELECT COUNT(*) FROM hr.Employees) <> 3 OR
   EXISTS (SELECT 1 FROM auth.UserAccounts a JOIN hr.Employees e ON e.Id=a.EmployeeId WHERE CASE WHEN
      (a.LoginName=N'superadmin@example.invalid' AND e.FullLegalName=N'TEST ONLY SuperAdmin' AND e.ApprovedLoginPhoneNumber=N'+12025550101' OR
       a.LoginName=N'admin@example.invalid' AND e.FullLegalName=N'TEST ONLY Admin' AND e.ApprovedLoginPhoneNumber=N'+12025550102' OR
       a.LoginName=N'hr@example.invalid' AND e.FullLegalName=N'TEST ONLY HR' AND e.ApprovedLoginPhoneNumber=N'+12025550103') THEN 0 ELSE 1 END = 1)
  THROW 51000, 'Synthetic identity boundary failed.', 1;
IF NOT EXISTS (SELECT 1 FROM auth.UserRoleAssignments WHERE Id='$revokedId' AND UserAccountId='$revokedAccount'
   AND RoleId='$revokedRole' AND RevokedAt IS NOT NULL) THROW 51000, 'Retained revocation changed.', 1;
IF EXISTS (SELECT 1 FROM attendance.AttendancePeriodLocks) OR EXISTS (SELECT 1 FROM jobs.OutboxMessages)
  THROW 51000, 'Unexpected attendance period or job data; review before upgrade.', 1;
IF EXISTS (SELECT 1 FROM hr.EmployeeStatusChanges c JOIN hr.Employees e ON e.Id=c.EmployeeId WHERE c.NewStatus<>e.Status)
  THROW 51000, 'Lifecycle state requires separate review before upgrade.', 1;
SELECT N'BASELINE_VERIFIED';
"@
$sqlcmd = (Get-Command sqlcmd -ErrorAction Stop).Source
$result = & $sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -d Cosmix_Testing -b -l 15 -t 30 -Q $preflight 2>&1
if ($LASTEXITCODE -ne 0 -or ($result | Out-String) -notmatch 'BASELINE_VERIFIED') { throw 'Read-only baseline verification failed; no upgrade performed. Details suppressed.' }
$runRoot = Assert-RegularPath (Join-Path $privateRoot ('runs\' + $manifest.RunId)) $privateRoot
if (Test-Path -LiteralPath $runRoot) { throw 'Run directory already exists. Reconcile it; never overwrite or blindly rerun.' }
# The existing parent is private. Also restrict the new child explicitly before writing its evidence.
$null = [IO.Directory]::CreateDirectory($runRoot)
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
$acl = [Security.AccessControl.DirectorySecurity]::new()
$acl.SetOwner($sid); $acl.SetAccessRuleProtection($true, $false)
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
Set-Acl -LiteralPath $runRoot -AclObject $acl
function Write-NewJson([string]$Path, [object]$Value) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $bytes = [Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $Value -Depth 30)); $stream.Write($bytes) }
    finally { $stream.Dispose() }
}
Write-NewJson (Join-Path $runRoot 'approved-manifest.json') $manifest
$backupPath = Assert-RegularPath (Join-Path $runRoot 'Cosmix_Testing-before-attendance-17.bak') $privateRoot
$backupLiteral = $backupPath.Replace("'", "''")
$backupSql = "BACKUP DATABASE [Cosmix_Testing] TO DISK=N'$backupLiteral' WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=N'$backupLiteral' WITH CHECKSUM;"
$result = & $sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -d Cosmix_Testing -b -l 15 -t 120 -Q $backupSql 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $backupPath)) { throw 'Backup/verification failed; no migration executed. Preserve this run for review.' }
Write-NewJson (Join-Path $runRoot 'verified-backup.json') ([ordered]@{ Database='Cosmix_Testing'; Path=$backupPath; Sha256=(Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash; VerifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('O') })
# Recheck the reviewed file immediately before execution. It contains only migrations 14 through 17.
if ((Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash -ine $manifest.MigrationScriptSha256) { throw 'Migration script changed after backup.' }
$result = & $sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -d Cosmix_Testing -b -l 15 -t 30 -Q $preflight 2>&1
if ($LASTEXITCODE -ne 0 -or ($result | Out-String) -notmatch 'BASELINE_VERIFIED') { throw 'Baseline changed after backup; no migration executed.' }
$result = & $sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -d Cosmix_Testing -b -l 15 -t 120 -i $scriptPath 2>&1
$migrationExitCode = $LASTEXITCODE
[IO.File]::WriteAllText((Join-Path $runRoot 'migration-output.log'), ($result | Out-String))
if ($migrationExitCode -ne 0) { throw 'Migration execution failed. Preserve the backup/log and inspect applied migration history; never automatically downgrade or rerun.' }
$target = [string]$manifest.TargetMigration # constrained above to a fixed migration suffix and numeric timestamp
$result = & $sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -d Cosmix_Testing -b -l 15 -t 30 -Q "SET NOCOUNT ON; IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) <> 17 OR (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory) <> N'$target' THROW 51000, 'Migration result mismatch.', 1; SELECT N'UPGRADE_VERIFIED';" 2>&1
if ($LASTEXITCODE -ne 0 -or ($result | Out-String) -notmatch 'UPGRADE_VERIFIED') { throw 'Post-upgrade schema verification failed; preserve evidence for review.' }
Write-NewJson (Join-Path $runRoot 'schema-result.json') ([ordered]@{ Database='Cosmix_Testing'; RunId=$manifest.RunId; TargetMigration=$target; ManifestSha256=$ApprovedManifestSha256; CompletedAtUtc=[DateTimeOffset]::UtcNow.ToString('O'); FixturesChanged=$false; PermissionsChanged=$false; KeysChanged=$false })
Write-Output 'Isolated schema phase verified. No fixtures, permissions, credentials, keys, hosts or workers changed.'
