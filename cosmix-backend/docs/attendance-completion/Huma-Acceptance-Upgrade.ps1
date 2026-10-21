# Prepared only. Root owns SQL generation/review and all future execution approvals.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ApprovedManifestSha256,
    [Parameter(Mandatory)][string]$SourceServer,
    [ValidateSet('Inspect','Execute')][string]$Mode = 'Inspect',
    [ValidateSet('UpgradeExistingCosmixTesting')][string]$ActionApproval
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7 -or -not $IsWindows) { throw 'Windows PowerShell 7 or later required.' }
$manifestFull = [IO.Path]::GetFullPath($ManifestPath)
$manifestRoot = 'C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend\docs\attendance-completion\'
if (-not $manifestFull.StartsWith($manifestRoot, [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetExtension($manifestFull) -ine '.json') { throw 'Explicit non-secret JSON manifest must be in the owned checkout documentation folder.' }
for ($cursor = $manifestFull; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
    if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked manifest paths are forbidden.' }
    if ([IO.Path]::GetDirectoryName($cursor) -eq $cursor) { break }
}
$manifestItem = Get-Item -LiteralPath $manifestFull -Force
if ($manifestItem.PSIsContainer -or $manifestItem.Length -lt 1 -or $manifestItem.Length -gt 1048576) { throw 'Invalid manifest size/type.' }
$bootstrapBytes = [IO.File]::ReadAllBytes($manifestFull)
if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bootstrapBytes)) -ine $ApprovedManifestSha256) { throw 'Manifest approval hash mismatch.' }
$bootstrap = [Text.Encoding]::UTF8.GetString($bootstrapBytes).TrimStart([char]0xFEFF) | ConvertFrom-Json -AsHashtable -Depth 50
if ((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Huma-Acceptance-Common.ps1') -Algorithm SHA256).Hash -cne
    $bootstrap.ArtifactHashes['docs/attendance-completion/Huma-Acceptance-Common.ps1']) { throw 'Library approval hash mismatch.' }
. (Join-Path $PSScriptRoot 'Huma-Acceptance-Common.ps1')
$context = Read-HumaContext $ManifestPath $ApprovedManifestSha256 $SourceServer
Assert-HumaMigrationTarget $context
$m = $context.Manifest
$sqlPath = Get-HumaPath (Join-Path $context.Repository $m.MigrationScript) $context.Repository -File
$sqlBytes = [IO.File]::ReadAllBytes($sqlPath)
Assert-Huma ((Get-HumaHash $sqlBytes) -ceq $m.ArtifactHashes[$m.MigrationScript]) 'Reviewed SQL bytes changed.'
$sql = [Text.Encoding]::UTF8.GetString($sqlBytes).TrimStart([char]0xFEFF)
# Only a root-reviewed ordinary EF SQL script is accepted. No sqlcmd/interpreter directives.
# These rejection checks are defense in depth, not a replacement for reviewing every batch.
Assert-Huma ($sql -notmatch '(?im)^\s*(:|!!|GO\s+\d)' -and $sql -notmatch '\$\(' -and
    $sql -notmatch '(?i)\b(USE|OPENROWSET|OPENDATASOURCE|OPENQUERY|BACKUP|RESTORE|REVERT|IMPERSONATE)\b' -and
    $sql -notmatch '(?i)\b(CREATE|ALTER|DROP)\s+DATABASE\b|\bEXEC(UTE)?\s+AS\b|\b(xp_|sp_configure|sp_addlinkedserver)' -and
    $sql -notmatch '(?i)\b(master|msdb|tempdb|Cosmix_Development)\b' -and
    $sql -notmatch '(?i)(?<![\w@])(?:\[[^\]]+\]|"[^"]+"|[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?:\[[^\]]+\]|"[^"]+"|[A-Za-z_][A-Za-z0-9_]*)?\s*\.\s*(?:\[[^\]]+\]|"[^"]+"|[A-Za-z_][A-Za-z0-9_]*)') 'SQL contains target switching, cross-database references or unsupported directives; root must narrow/review it.'
$batches = @([regex]::Split($sql, '(?im)^\s*GO\s*(?:--[^\r\n]*)?\r?$') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
Assert-Huma ($batches.Count -gt 0 -and $m.SqlReview.BatchCount -eq $batches.Count -and
    $m.SqlReview.FromMigration -ceq '20261008074438_AddEmployeeChangeJournal' -and
    $m.SqlReview.ToMigration -ceq $m.TargetMigration -and $m.SqlReview.SingleDatabase -eq $true -and
    $m.SqlReview.NoFixtureOrCredentialChanges -eq $true -and $m.SqlReview.NoGoInsideStringsOrComments -eq $true -and
    $m.SqlReview.TransactionBoundariesReviewed -eq $true) 'Explicit root SQL scope/batch/transaction review is unresolved.'
if ($Mode -ceq 'Inspect') {
    [pscustomobject]@{ Mode = 'OfflineUpgradeInspect'; PendingMigrations = $m.TargetMigrationCount - 12;
        TargetMigration = $m.TargetMigration; BatchCount = $batches.Count; SqlExecuted = $false; WritesApproved = $false }
    return
}
Assert-Huma ($ActionApproval -ceq 'UpgradeExistingCosmixTesting') 'Explicit action approval required in addition to the exact manifest SHA.'
Assert-HumaWriteApproval $context Upgrade
$connection = $null; $run = $null; $phase = 'Preflight'; $completedBatches = 0
function Invoke-UpgradeCommand([string]$Text, [string]$BackupPath = '', [string]$BackupName = '') {
    $command = $connection.CreateCommand()
    $command.CommandText = $Text; $command.CommandTimeout = 180
    if ($BackupPath) { $null = $command.Parameters.AddWithValue('@backup', $BackupPath) }
    if ($BackupName) { $null = $command.Parameters.AddWithValue('@name', $BackupName) }
    try { $null = $command.ExecuteNonQuery() }
    finally { $command.Dispose() }
}
try {
    $connection = Open-HumaSql $context
    $transaction = $connection.BeginTransaction([Data.IsolationLevel]::Serializable)
    try {
        $before = Get-HumaInventory $connection $context $transaction
        Assert-HumaState $before $context Baseline
        $transaction.Rollback()
    } finally { $transaction.Dispose() }
    $run = Start-HumaAttempt $context upgrade
    Write-HumaNewJson (Join-Path $run 'baseline-inventory.json') $before
    $backup = Get-HumaPath (Join-Path $run 'Cosmix_Testing-copy-only.bak') $m.ArtifactRoot
    Assert-Huma (-not (Test-Path -LiteralPath $backup)) 'Backup path already exists; no overwrite or append allowed.'
    $phase = 'Backup'
    $null = Assert-HumaSqlIdentity $connection $context
    Invoke-UpgradeCommand 'BACKUP DATABASE [Cosmix_Testing] TO DISK=@backup WITH COPY_ONLY,CHECKSUM,NOFORMAT,NOINIT,NAME=@name;' $backup $m.RunId
    $phase = 'VerifyBackup'
    $command = $connection.CreateCommand()
    $command.CommandText = 'RESTORE HEADERONLY FROM DISK=@backup;'
    $command.CommandTimeout = 180
    $null = $command.Parameters.AddWithValue('@backup', $backup)
    try {
        $reader = $command.ExecuteReader()
        try {
            Assert-Huma ($reader.Read()) 'Missing backup header.'
            Assert-Huma ([string]$reader['DatabaseName'] -ceq 'Cosmix_Testing' -and [string]$reader['BackupName'] -ceq $m.RunId -and
                [int]$reader['BackupType'] -eq 1 -and [bool]$reader['IsCopyOnly'] -and [bool]$reader['HasBackupChecksums'] -and
                -not [bool]$reader['IsDamaged'] -and -not $reader.Read()) 'Backup header is not the single owned copy-only checksummed database backup.'
        } finally { $reader.Dispose() }
    } finally { $command.Dispose() }
    Invoke-UpgradeCommand 'RESTORE VERIFYONLY FROM DISK=@backup WITH FILE=1,CHECKSUM;' $backup
    $null = Get-HumaPath $backup $m.ArtifactRoot
    $backupItem = Get-Item -LiteralPath $backup -Force
    Assert-Huma (-not $backupItem.PSIsContainer -and $backupItem.Length -gt 0) 'Verified backup file is missing or empty.'
    Assert-HumaPrivate $backup
    $backupHash = (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
    Write-HumaNewJson (Join-Path $run 'verified-backup.json') @{ Database = 'Cosmix_Testing'; SourceServer = $context.SourceServer;
        Path = $backup; Sha256 = $backupHash; CopyOnly = $true; Checksum = $true; RestoreVerifyOnly = $true;
        ManifestSha256 = $context.ManifestSha256; AtUtc = [DateTimeOffset]::UtcNow.ToString('O') }
    $phase = 'RecheckBaseline'
    # The backup is already durable. Hold baseline table locks until execution starts.
    # Operator must keep all other writers stopped for the complete schema window.
    $transaction = $connection.BeginTransaction([Data.IsolationLevel]::Serializable)
    try {
        Assert-HumaState (Get-HumaInventory $connection $context $transaction) $context Baseline
        $transaction.Rollback()
    } finally { $transaction.Dispose() }
    $null = Read-HumaContext $ManifestPath $ApprovedManifestSha256 $SourceServer
    Assert-HumaMigrationTarget $context
    Assert-HumaHash $sqlPath $m.ArtifactHashes[$m.MigrationScript]
    Assert-HumaHash $backup $backupHash
    Assert-Huma ($m.Approvals.ExclusiveSqlWindow -eq $true) 'Human confirmation that other hosts/writers are stopped is required.'
    $phase = 'ApplyReviewedBatches'
    for ($index = 0; $index -lt $batches.Count; $index++) {
        $null = Assert-HumaSqlIdentity $connection $context
        Write-HumaNewJson (Join-Path $run ('batch-intent-{0:D4}.json' -f ($index + 1))) @{
            Batch = $index + 1; ManifestSha256 = $context.ManifestSha256; AtUtc = [DateTimeOffset]::UtcNow.ToString('O') }
        Invoke-UpgradeCommand $batches[$index]
        $completedBatches++
    }
    $phase = 'Postflight'
    $openTransactions = @(Read-HumaRows $connection 'SELECT @@TRANCOUNT AS TransactionCount;')[0].TransactionCount
    Assert-Huma ($openTransactions -eq 0) 'Reviewed script left an open transaction; retain partial evidence for root reconciliation.'
    $after = Get-HumaInventory $connection $context
    Write-HumaNewJson (Join-Path $run 'target-inventory.json') $after
    Assert-HumaState $after $context Target
    Write-HumaNewJson (Join-Path $run 'schema-result.json') @{ RunId = $m.RunId; Database = 'Cosmix_Testing';
        ActualServerName = $m.ActualServerName; Owner = $m.Owner; ManifestSha256 = $context.ManifestSha256;
        TargetMigration = $m.TargetMigration; TargetMigrationCount = $m.TargetMigrationCount;
        BackupSha256 = $backupHash; CompletedBatches = $completedBatches; AtUtc = [DateTimeOffset]::UtcNow.ToString('O') }
    Write-Output 'Exact reviewed existing-database schema phase verified; retain the private backup and receipts.'
} catch {
    # Dispose rolls back only any still-open transaction; earlier committed batches stay intact.
    if ($run) {
        Write-HumaNewJson (Join-Path $run 'stopped.json') @{ Phase = $phase; CompletedBatches = $completedBatches;
            ManifestSha256 = $context.ManifestSha256; AtUtc = [DateTimeOffset]::UtcNow.ToString('O');
            Recovery = 'Preserve everything. Root must inspect history/schema/data and approve an explicit recovery plan; no automatic restore or rerun.' }
    }
    throw 'Guarded upgrade stopped. No automatic restore, downgrade or retry. Preserve the attempt marker, backup and partial state; provider details suppressed.'
} finally { if ($connection) { $connection.Dispose() }; $sqlBytes = $null; $sql = $null; $batches = $null }
