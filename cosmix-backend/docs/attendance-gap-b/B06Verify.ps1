param(
    [string]$ArtifactsPath,
    [string]$ProjectArtifactsPath
)

$ErrorActionPreference = 'Stop'
$b06Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$b06Artifacts = if ($ArtifactsPath) { [IO.Path]::GetFullPath($ArtifactsPath) } else {
    Join-Path ([IO.Path]::GetTempPath()) ('cosmix-b06-' + [Guid]::NewGuid().ToString('N'))
}
$null = New-Item -ItemType Directory -Path $b06Artifacts -Force
function Get-B06ProjectArtifacts([string]$Name) {
    if ($Name -eq 'solution' -and $ProjectArtifactsPath) { [IO.Path]::GetFullPath($ProjectArtifactsPath) }
    else { Join-Path $b06Artifacts $Name }
}
Write-Host "B06 verification artifacts: $b06Artifacts"
# Reuse the opt-in solution cache; file-based apps must keep distinct output identities.
& dotnet build (Join-Path $b06Root 'Cosmix.Backend.sln') --configuration Release `
    --artifacts-path (Get-B06ProjectArtifacts 'solution') -p:RestoreLockedMode=true `
    2>&1 | Tee-Object -FilePath (Join-Path $b06Artifacts 'release-build.log')
if ($LASTEXITCODE -ne 0) { throw "B06 Release build failed: $b06Artifacts/release-build.log" }
$b06Checks = @(
    @{ Name = 'rules'; File = 'docs/attendance-completion/VerifyZones.cs' },
    @{ Name = 'live-service'; File = 'docs/attendance-completion/VerifyZoneService.cs' },
    @{ Name = 'policy-binding'; File = 'docs/attendance-gap-b/B06VerifyPolicy.cs' },
    @{ Name = 'registered-contracts'; File = 'docs/attendance-gap-b/B06VerifyRegisteredContracts.cs' },
    @{ Name = 'offline-api-queries'; File = 'docs/attendance-gap-b/B06VerifyOffline.cs' },
    @{ Name = 'claim-retention'; File = 'docs/attendance-gap-b/B06VerifyClaimRetention.cs' },
    @{ Name = 'claim-storage'; File = 'docs/attendance-gap-b/B06VerifyClaimStorage.cs'; Args = @('--synthetic-private-files') }
)
foreach ($b06Check in $b06Checks) {
    & dotnet run --file (Join-Path $b06Root $b06Check.File) --configuration Release `
        --artifacts-path (Get-B06ProjectArtifacts $b06Check.Name) --no-launch-profile -- @($b06Check.Args) `
        2>&1 | Tee-Object -FilePath (Join-Path $b06Artifacts ($b06Check.Name + '.log'))
    if ($LASTEXITCODE -ne 0) { throw "B06 $($b06Check.Name) check failed; inspect $b06Artifacts" }
}
Write-Host 'PASS: B06 actual synthetic intake/retry/reconcile/claim-expiry checks. SQL, hosted mobile and deployment acceptance remain separate.'
