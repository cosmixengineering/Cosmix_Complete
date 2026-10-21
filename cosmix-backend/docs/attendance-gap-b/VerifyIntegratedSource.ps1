param(
    [Parameter(Mandatory = $true)][string]$ArtifactsPath,
    [string[]]$Only = @()
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifacts = [IO.Path]::GetFullPath($ArtifactsPath)
if ($artifacts.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use a separate build-output directory, not the existing source checkout.'
}
if (Test-Path -LiteralPath $artifacts) { throw 'Choose a fresh output directory; prior results are retained.' }
$checks = @(
    'docs/attendance-gap-b/B01VerifyOfficeSource.cs',
    'docs/attendance-gap-b/B01VerifyOfficeBoundary.cs',
    'docs/attendance-gap-b/B01VerifyOfficeModel.cs',
    'docs/attendance-gap-b/B01VerifyOfficePersistence.cs',
    'docs/attendance-gap-b/VerifyCaptureAuthorityTime.cs',
    'docs/attendance-capture/VerifyCaptureService.cs',
    'docs/attendance-gap-b/B02VerifyRules.cs',
    'docs/attendance-gap-b/B02VerifyEvaluator.cs',
    'docs/attendance-gap-b/B02VerifyPolicyReader.cs',
    'docs/attendance-gap-b/B02VerifyManagement.cs',
    'docs/attendance-gap-b/B02VerifyPersistence.cs',
    'docs/attendance-corrections/VerifyCorrectionService.cs',
    'docs/attendance-corrections/VerifyCarryForwardService.cs',
    'docs/attendance-corrections/VerifyAuditReads.cs',
    'docs/attendance-gap-b/B03VerifyRegisteredContracts.cs',
    'docs/attendance-policies/VerifyDailyCalculation.cs',
    'docs/attendance-policies/VerifyDailyCalculationQueries.cs',
    'docs/attendance-policies/VerifyOfficePayrollSources.cs',
    'docs/attendance-policies/VerifyOfficePayrollJurisdictionRegistration.cs',
    'docs/attendance-policies/VerifyPayrollInputs.cs',
    'docs/attendance-completion/VerifyPayrollInputService.cs',
    'docs/attendance-completion/VerifyOvertimeService.cs',
    'docs/attendance-completion/notifications/VerifyMissingCheckoutReminders.cs',
    'docs/attendance-completion/notifications/VerifyReminderQueries.cs',
    'docs/attendance-gap-b/B06VerifyOffline.cs',
    'docs/attendance-completion/VerifySiteReports.cs',
    'docs/attendance-completion/VerifySiteReportQueries.cs',
    'docs/attendance-gap-b/B07VerifyQueueContract.cs',
    'docs/attendance-completion/VerifyAttendanceComposition.cs',
    'docs/attendance-gap-b/VerifyRegisteredContracts.cs',
    'docs/attendance-gap-b/VerifyBMigration.cs'
)
if ($Only.Count) {
    foreach ($name in $Only) {
        if (-not ($checks | Where-Object { [IO.Path]::GetFileNameWithoutExtension($_) -eq $name })) {
            throw "Unknown integrated check: $name"
        }
    }
    $checks = @($checks | Where-Object { $Only -contains [IO.Path]::GetFileNameWithoutExtension($_) })
}
$null = New-Item -ItemType Directory -Path $artifacts
$temp = Join-Path $artifacts 'process-temp'
$null = New-Item -ItemType Directory -Path $temp
$priorTemp = $env:TEMP
$priorTmp = $env:TMP
$results = [Collections.Generic.List[object]]::new()
try {
    # Per-process temporary/output overrides do not alter application configuration or SQL targets.
    $env:TEMP = $temp
    $env:TMP = $temp
    Push-Location $root
    try {
        & dotnet build (Join-Path $root 'Cosmix.Backend.sln') --configuration Release `
            --artifacts-path (Join-Path $artifacts 'solution') --maxcpucount:1 -p:RestoreLockedMode=true `
            2>&1 | Tee-Object -FilePath (Join-Path $artifacts 'release-build.log')
        if ($LASTEXITCODE -ne 0) { throw 'Integrated Release build failed.' }
        foreach ($file in $checks) {
            $name = [IO.Path]::GetFileNameWithoutExtension($file)
            $extra = if ($name -eq 'B02VerifyManagement') { @('--require-catalog') } else { @() }
            Write-Host "RUN: $file"
            # Runfile apps must not share entrypoint/output identities with another check.
            & dotnet run --file (Join-Path $root $file) --configuration Release `
                --artifacts-path (Join-Path $artifacts $name) --no-launch-profile -- @extra `
                2>&1 | Tee-Object -FilePath (Join-Path $artifacts ($name + '.log'))
            $code = $LASTEXITCODE
            $results.Add([pscustomobject]@{
                File = $file; Sha256 = (Get-FileHash -LiteralPath (Join-Path $root $file) -Algorithm SHA256).Hash
                ExitCode = $code; Log = $name + '.log'; CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
            })
            $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $artifacts 'checks.json') -Encoding utf8
            if ($code -ne 0) { throw "$name failed; do not label the integrated suite accepted." }
        }
    } finally { Pop-Location }
} finally {
    $env:TEMP = $priorTemp
    $env:TMP = $priorTmp
}
Write-Host "PASS: $($results.Count) integrated standalone source checks. No SQL, authenticated HTTP, real face/device or client acceptance is established."
