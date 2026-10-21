# Prepared only. Inspect is offline; Inventory/Preflight execute SELECT-only inspection when root runs them.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ApprovedManifestSha256,
    [Parameter(Mandatory)][string]$SourceServer,
    [ValidateSet('Inspect','Inventory','Preflight')][string]$Mode = 'Inspect',
    [ValidateSet('Baseline','Target')][string]$Stage = 'Baseline'
)
$ErrorActionPreference = 'Stop'
# Pin the library before loading code. Read only the explicit non-secret manifest.
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
if ($Mode -ceq 'Inspect') {
    [pscustomobject]@{ Mode = 'OfflineInspect'; ManifestSha256 = $context.ManifestSha256; SqlExecuted = $false;
        PrivateFilesRead = $false; WritesApproved = $false; Target = $context.Manifest.TargetMigration }
    return
}
$connection = Open-HumaSql $context
try {
    $transaction = $connection.BeginTransaction([Data.IsolationLevel]::Serializable)
    try {
        $inventory = Get-HumaInventory $connection $context $transaction
        if ($Mode -ceq 'Preflight') {
            Assert-HumaMigrationTarget $context
            Assert-HumaState $inventory $context $Stage
        }
        # Rollback only releases the read transaction/locks; no DML/DDL/backup is issued.
        $transaction.Rollback()
    } finally { $transaction.Dispose() }
    if ($Mode -ceq 'Inventory') {
        ConvertTo-Json -InputObject $inventory -Depth 40
    } else {
        [pscustomobject]@{ Mode = 'ReadOnlyPreflight'; Stage = $Stage; ManifestSha256 = $context.ManifestSha256;
            MigrationCount = $inventory.History.Count; EmployeeCount = $inventory.Employees.Count;
            AccountCount = $inventory.Accounts.Count; WritesApproved = $false }
    }
} finally { $connection.Dispose() }
