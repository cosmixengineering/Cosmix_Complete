<#
Prepared narrow acceptance orchestrator. Human approval of the exact manifest is required.
Upgrade keeps a verified backup; HTTP uses the unchanged private settings/keys and three synthetic accounts.
No reset, fake policy/provider, historic duty fixture, worker enablement or production access.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ApprovedManifestSha256,
    [switch]$SkipCertificateCheck,
    [switch]$ResumeAfterGrants
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$privateRoot = [IO.Path]::GetFullPath('C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009')
$manifestFull = [IO.Path]::GetFullPath($ManifestPath)
if (-not $manifestFull.StartsWith((Join-Path $repository 'docs\attendance-completion')+'\', [StringComparison]::OrdinalIgnoreCase) -or
    (Get-FileHash -LiteralPath $manifestFull -Algorithm SHA256).Hash -ine $ApprovedManifestSha256) { throw 'Exact reviewed manifest required.' }
$manifest = Get-Content -LiteralPath $manifestFull -Raw | ConvertFrom-Json -Depth 30
foreach ($entry in @(
    @{ File=$PSCommandPath; Hash=$manifest.RunnerSha256 },
    @{ File=(Join-Path $PSScriptRoot 'Upgrade-AttendanceAcceptance.ps1'); Hash=$manifest.UpgradeRunnerSha256 },
    @{ File=(Join-Path $PSScriptRoot 'Invoke-AttendanceWebAcceptance.ps1'); Hash=$manifest.WebRunnerSha256 }
)) {
    if ([string]$entry.Hash -cnotmatch '^[A-F0-9]{64}$' -or (Get-FileHash -LiteralPath $entry.File -Algorithm SHA256).Hash -cne $entry.Hash) {
        throw 'Reviewed acceptance runner changed.'
    }
}
# Offline inspection also verifies private file ACLs, settings, fixed instance and role descriptions.
$inspection = & (Join-Path $PSScriptRoot 'Invoke-AttendanceWebAcceptance.ps1') -ManifestPath $manifestFull -Mode Inspect -ResumeAfterGrants:$ResumeAfterGrants
if ($inspection.Mode -cne 'OfflineInspect') { throw 'Offline inspection failed.' }
if ($manifest.RunId -cnotmatch '^ATT[A-Z0-9]{12,28}$' -or $manifest.ExistingPrivateRoot -cne $privateRoot) { throw 'Unexpected run target.' }
$runRoot = Join-Path $privateRoot ('runs\' + $manifest.RunId)
$executionRoot = $runRoot
if ($ResumeAfterGrants) {
    # Inspect has verified the exact stopped journal, original schema receipt and unchanged scope.
    $executionRoot = Join-Path $runRoot 'web-continuation-1'
    if (Test-Path -LiteralPath $executionRoot) { throw 'Web continuation already exists; reconcile it instead of repeating.' }
} elseif (Test-Path -LiteralPath $runRoot) { throw 'Run already exists. Reconcile its evidence; this orchestrator never restarts a partial run.' }
if ([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port -contains 7249) {
    throw 'Acceptance port is occupied; no unrelated process will be stopped.'
}
$assembly = Join-Path $repository 'src\Cosmix.Api\bin\Release\net10.0\Cosmix.Api.dll'
function Assert-Release {
    $runtimeDirectory = Split-Path -Parent $assembly
    if ((Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -cne $manifest.ApiAssemblySha256) { throw 'Reviewed Release assembly changed.' }
    foreach ($name in @('Cosmix.Domain.dll','Cosmix.Application.dll','Cosmix.Infrastructure.dll')) {
        $hash = $manifest.RuntimeAssemblySha256.$name
        if ([string]$hash -cnotmatch '^[A-F0-9]{64}$' -or (Get-FileHash -LiteralPath (Join-Path $runtimeDirectory $name) -Algorithm SHA256).Hash -cne $hash) {
            throw 'Reviewed Release dependency changed.'
        }
    }
}
Assert-Release
$process = $null
try {
    if (-not $ResumeAfterGrants) {
        & (Join-Path $PSScriptRoot 'Upgrade-AttendanceAcceptance.ps1') -ManifestPath $manifestFull -ApprovedManifestSha256 $ApprovedManifestSha256
    }
    # The ordinary lifecycle worker remains present. Refuse any differing pending status, including
    # future ones, so this host cannot reconcile unrelated fixture history during an acceptance run.
    $preflight = @'
SET NOCOUNT ON;
IF DB_NAME() <> N'Cosmix_Testing' THROW 51000, 'Wrong test database.', 1;
IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) <> 17 OR
   (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory) <> N'20261012082833_RetainZoneWarningTransitions'
    THROW 51000, 'Expected completed seventeen-migration schema.', 1;
IF EXISTS (SELECT 1 FROM attendance.DailyAttendanceSummaries)
    THROW 51000, 'Unexpected summary before the single factual revision.', 1;
IF EXISTS (SELECT 1 FROM hr.EmployeeStatusChanges c JOIN hr.Employees e ON e.Id=c.EmployeeId WHERE c.NewStatus<>e.Status)
    THROW 51000, 'Lifecycle state requires separate review before starting host.', 1;
IF EXISTS (SELECT 1 FROM jobs.OutboxMessages) THROW 51000, 'Unexpected job data before owned host start.', 1;
SELECT N'OWNED_HOST_PREFLIGHT_OK';
'@
    $output = & (Get-Command sqlcmd -ErrorAction Stop).Source -S '(localdb)\MSSQLLocalDB' -E -C -d Cosmix_Testing -b -l 15 -t 30 -Q $preflight 2>&1
    if ($LASTEXITCODE -ne 0 -or ($output | Out-String) -notmatch 'OWNED_HOST_PREFLIGHT_OK') { throw 'Read-only host preflight failed. Retain the upgrade evidence; no host was started.' }
    $settingsPath = Join-Path $privateRoot 'settings.json'
    if ((Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash -cne $manifest.SettingsSha256) { throw 'Retained settings changed.' }
    Assert-Release
    if ($ResumeAfterGrants) {
        if (Test-Path -LiteralPath $executionRoot) { throw 'Web continuation appeared during preflight; refusing overwrite.' }
        $null = [IO.Directory]::CreateDirectory($executionRoot)
        # Inherit the already verified private run directory's restricted ACL.
    }
    $stdout = Join-Path $executionRoot 'host.stdout.log'; $stderr = Join-Path $executionRoot 'host.stderr.log'
    $environment = @{
        COSMIX_LOCAL_TESTING_SETTINGS=$settingsPath; ASPNETCORE_ENVIRONMENT='Development'; DOTNET_ENVIRONMENT='Development'
        AttendanceJobs__Enabled='false'; AttendanceZones__Enabled='false'; AllowedHosts='localhost'
        AttendanceZones__RetentionEnabled='false'; ZoneLocationStorage__Enabled='false'
    }
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"'+$assembly+'"'),'--urls','https://localhost:7249') `
        -WorkingDirectory (Join-Path $repository 'src/Cosmix.Api') -Environment $environment -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $startedAt = $process.StartTime.ToUniversalTime()
    $receipt = [ordered]@{ ProcessId=$process.Id; StartedAtUtc=$startedAt.ToString('O'); Stdout=$stdout; Stderr=$stderr;
        BaseUri='https://localhost:7249'; RunId=$manifest.RunId; SettingsSha256=$manifest.SettingsSha256; AttendanceJobsEnabled=$false;
        ManifestSha256=$ApprovedManifestSha256; ResumeAfterGrants=[bool]$ResumeAfterGrants }
    $stream = [IO.File]::Open((Join-Path $executionRoot 'host.json'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try { $bytes=[Text.Encoding]::UTF8.GetBytes(($receipt | ConvertTo-Json)); $stream.Write($bytes) } finally { $stream.Dispose() }
    $deadline = [DateTime]::UtcNow.AddSeconds(40); $ready=$false
    while ([DateTime]::UtcNow -lt $deadline) {
        $process.Refresh(); if ($process.HasExited) { throw 'Owned host exited; inspect private logs.' }
        try {
            $probe = Invoke-RestMethod -Uri 'https://localhost:7249/api/v1/auth/web/local-testing' -TimeoutSec 2 -SkipCertificateCheck:$SkipCertificateCheck
            if ($probe.enabled -eq $true -and $probe.database -ceq 'Cosmix_Testing') { $ready=$true; break }
        } catch { Start-Sleep -Milliseconds 200 }
    }
    if (-not $ready) { throw 'Guarded owned HTTPS host did not become ready.' }
    & (Join-Path $PSScriptRoot 'Invoke-AttendanceWebAcceptance.ps1') -ManifestPath $manifestFull -Mode Execute `
        -ApprovedManifestSha256 $ApprovedManifestSha256 -SkipCertificateCheck:$SkipCertificateCheck -ResumeAfterGrants:$ResumeAfterGrants
} finally {
    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            $actual = Get-Process -Id $process.Id -ErrorAction Stop
            if ($actual.ProcessName -cne 'dotnet' -or $actual.StartTime.ToUniversalTime() -ne $startedAt) {
                throw 'Owned process identity changed; refusing to stop another process.'
            }
            Stop-Process -Id $process.Id -ErrorAction Stop
        }
    }
}
