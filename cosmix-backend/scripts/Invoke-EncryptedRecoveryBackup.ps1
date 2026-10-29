#requires -Version 7.0
<# Backup a coordinated, immutable recovery set. SQL .bak and private-evidence snapshots
must be created during the same quiesced maintenance window. Live application files,
application keys and signing credentials are deliberately not discovered or copied.
RESTIC_REPOSITORY/RESTIC_PASSWORD_FILE and remote credentials come from private scheduler
environment. This command never initializes, forgets or prunes a repository. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SqlBackupFile,
    [Parameter(Mandatory)][string]$PrivateEvidenceSnapshotDirectory,
    [Parameter(Mandatory)][string]$RecoveryManifest,
    [Parameter(Mandatory)][string[]]$KeyCustodyPaths,
    [Parameter(Mandatory)][string]$EvidenceOutput,
    [string]$ResticPath = 'restic',
    [switch]$LocalDrill
)
$ErrorActionPreference = 'Stop'
function Safe-Path([string]$Path, [bool]$Directory) {
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.PSIsContainer -ne $Directory) { throw 'Recovery input has the wrong type.' }
    for ($cursor = $item; $null -ne $cursor;) {
        if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked recovery paths are not allowed.' }
        $cursor = if ($cursor -is [IO.DirectoryInfo]) { $cursor.Parent } else { $cursor.Directory }
    }
    return $item.FullName
}
function Contains-Path([string]$Parent, [string]$Child) {
    return $Child.Equals($Parent, [StringComparison]::OrdinalIgnoreCase) -or
        $Child.StartsWith($Parent.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}
$sql = Safe-Path $SqlBackupFile $false
$private = Safe-Path $PrivateEvidenceSnapshotDirectory $true
$manifestPath = Safe-Path $RecoveryManifest $false
if ([IO.Path]::GetExtension($sql) -ne '.bak' -or (Get-Item -LiteralPath $sql).Length -eq 0) { throw 'A nonempty SQL backup is required.' }
if ((Get-Item -LiteralPath $manifestPath).Length -gt 65536) { throw 'Recovery manifest is too large.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.coordinatedSnapshot -ne $true -or
    $manifest.sqlRestoreVerified -ne $true -or $manifest.keyCustodyVerified -ne $true -or
    [string]::IsNullOrWhiteSpace($manifest.keyCustodyReference) -or
    $manifest.sqlBackupSha256 -cnotmatch '^[a-f0-9]{64}$' -or
    (Get-FileHash -LiteralPath $sql -Algorithm SHA256).Hash.ToLowerInvariant() -cne $manifest.sqlBackupSha256) {
    throw 'Reviewed recovery manifest and matching restore-verified SQL backup are required.'
}
$taken = [DateTimeOffset]::Parse($manifest.snapshotAtUtc)
if ($taken -gt [DateTimeOffset]::UtcNow -or $taken -lt [DateTimeOffset]::UtcNow.AddDays(-2)) { throw 'Recovery set is stale or future dated.' }
if (-not $env:RESTIC_REPOSITORY -or -not $env:RESTIC_PASSWORD_FILE) { throw 'Private restic repository and password-file configuration required.' }
if (-not $LocalDrill -and $env:RESTIC_REPOSITORY -notmatch '^(sftp:|s3:https://|azure:|gs:|b2:|rest:https://)') {
    throw 'Production backup requires an approved remote encrypted repository; local repositories are drill-only.'
}
$password = Safe-Path $env:RESTIC_PASSWORD_FILE $false
if ($KeyCustodyPaths.Count -eq 0) { throw 'Separate application-key custody locations must be declared.' }
$protectedPaths = @($password) + @($KeyCustodyPaths | ForEach-Object {
    $item = Get-Item -LiteralPath $_ -Force
    Safe-Path $item.FullName $item.PSIsContainer
})
foreach ($path in $protectedPaths) {
    if ((Contains-Path $private $path) -or (Contains-Path $path $private) -or
        (Contains-Path $path $sql) -or (Contains-Path $path $manifestPath) -or $path -eq $sql -or $path -eq $manifestPath) {
        throw 'Recovery data and encryption/key custody must be separate.'
    }
}
# Reject junctions inside the snapshot too: following them could accidentally back up key directories.
if (Get-ChildItem -LiteralPath $private -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint} | Select-Object -First 1) {
    throw 'Recovery snapshot contains a link.'
}
$output = [IO.Path]::GetFullPath($EvidenceOutput)
if (Test-Path -LiteralPath $output) { throw 'Evidence output must be a new file.' }
$outputParent = Safe-Path ([IO.Path]::GetDirectoryName($output)) $true
if (Contains-Path $private $output) { throw 'Evidence output must be outside the immutable snapshot.' }
$exe = (Get-Command $ResticPath -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$lock = [IO.File]::Open($manifestPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
$sqlLock = [IO.File]::Open($sql, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
try {
    $raw = @(& $exe backup --json --tag cosmix-payroll-recovery -- $sql $private $manifestPath 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Encrypted backup failed or skipped files; no successful recovery evidence recorded.' }
    $summary = @($raw | ForEach-Object { try { [string]$_ | ConvertFrom-Json } catch {} } | Where-Object {$_.message_type -eq 'summary'})
    if ($summary.Count -ne 1 -or $summary[0].snapshot_id -cnotmatch '^[a-f0-9]{64}$') { throw 'Backup completion could not be verified.' }
    $null = & $exe check --read-data 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'Encrypted repository integrity check failed.' }
    $report = [ordered]@{ schemaVersion=1; completedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');
        snapshotId=$summary[0].snapshot_id; mode=$(if($LocalDrill){'LocalDrill'}else{'RemoteBackup'});
        repositoryIntegrityChecked=$true; providerGeographyVerified=$false; restoredByThisRun=$false;
        manifestSha256=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant() }
    $stream = [IO.File]::Open($output, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $bytes=[Text.Encoding]::UTF8.GetBytes(($report | ConvertTo-Json)); $stream.Write($bytes) } finally { $stream.Dispose() }
    'Encrypted recovery snapshot and repository integrity verified. Restore acceptance is a separate check.'
} finally { $sqlLock.Dispose(); $lock.Dispose(); $raw = $null }
