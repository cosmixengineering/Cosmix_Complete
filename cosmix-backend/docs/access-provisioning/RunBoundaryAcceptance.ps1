param([ValidateSet('GrantSiteB','Run','Replay','Revoke')][string]$Phase = 'GrantSiteB')
$ErrorActionPreference = 'Stop'
$private = 'C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009'
. (Join-Path $PSScriptRoot 'LocalAcceptanceHttp.ps1')
$credentials = Get-Content -LiteralPath (Join-Path $private 'Test-Accounts.json') -Raw | ConvertFrom-Json
$arguments = @{
    Phase = $Phase
    FixturePath = (Join-Path $private 'attendance-manifest.json')
    AccessEvidencePath = (Join-Path $private 'access-results.json')
    SchedulingEvidencePath = (Join-Path $private 'scheduling-evidence.json')
    EvidencePath = (Join-Path $private 'boundary-evidence.json')
}
if ($Phase -eq 'GrantSiteB') {
    $arguments.SuperAdminClient = New-LocalAcceptanceSession -BaseUri https://localhost:7249 -Account ($credentials | Where-Object Email -eq 'superadmin@example.invalid') -SkipCertificateCheck
} elseif ($Phase -eq 'Revoke') {
    $completed = Get-Content -LiteralPath $arguments.EvidencePath -Raw | ConvertFrom-Json
    if (-not $completed.completed -or $completed.lastPhase -ne 'Replay') { throw 'Finish boundary acceptance/replay before revocation.' }
    $admin = New-LocalAcceptanceSession -BaseUri https://localhost:7249 -Account ($credentials | Where-Object Email -eq 'admin@example.invalid') -SkipCertificateCheck
    $super = New-LocalAcceptanceSession -BaseUri https://localhost:7249 -Account ($credentials | Where-Object Email -eq 'superadmin@example.invalid') -SkipCertificateCheck
    . (Join-Path $PSScriptRoot 'LocalAcceptanceRevocation.ps1')
    $manifest = Get-Content -LiteralPath $arguments.FixturePath -Raw | ConvertFrom-Json
    $evidence = Get-Content -LiteralPath $arguments.AccessEvidencePath -Raw | ConvertFrom-Json -Depth 40
    $revocation = Revoke-LocalAcceptanceRosterGrant -Client $super -Manifest $manifest -AccessEvidence $evidence -ExpectedRunId $manifest.RunId -PriorAdminClient $admin
    [IO.File]::WriteAllText((Join-Path $private 'revocation-evidence.json'), ($revocation | ConvertTo-Json -Depth 15), [Text.UTF8Encoding]::new($false))
    Write-Host 'SuperAdmin revoked only the synthetic Site A roster grant; prior Admin session rejected with 401.'
    $fresh = New-LocalAcceptanceSession -BaseUri https://localhost:7249 -Account ($credentials | Where-Object Email -eq 'admin@example.invalid') -SkipCertificateCheck
    & (Join-Path $PSScriptRoot '../group-rosters/Invoke-IsolatedAcceptance.ps1') -Client $fresh -FixturePath $arguments.FixturePath -EvidencePath $arguments.SchedulingEvidencePath -Phase DeniedReplay
    return
} else {
    $arguments.AdminClient = New-LocalAcceptanceSession -BaseUri https://localhost:7249 -Account ($credentials | Where-Object Email -eq 'admin@example.invalid') -SkipCertificateCheck
}
& (Join-Path $PSScriptRoot '../group-rosters/Invoke-BoundaryAcceptance.ps1') @arguments
