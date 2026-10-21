# Executes the user-approved fixture acceptance; never print or serialize live clients.
param([ValidateSet('Access', 'Scheduling', 'SchedulingResume', 'Replay')][string]$Phase = 'Access')
$ErrorActionPreference = 'Stop'
$private = 'C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009'
$manifestPath = Join-Path $private 'attendance-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$summaryPath = Join-Path $private 'access-results.json'
$journal = [Collections.Generic.List[object]]::new()
if ($Phase -eq 'Access') {
    if (Test-Path -LiteralPath $summaryPath) { throw 'Completed access evidence exists. Do not rerun provisioning.' }
    try {
        $access = & (Join-Path $PSScriptRoot 'VerifyAuthenticatedAccess.ps1') -BaseUri https://localhost:7249 -AccountsPath (Join-Path $private 'Test-Accounts.json') -FixtureManifestPath $manifestPath -RunId $manifest.RunId -SkipCertificateCheck -PassThruClients -RunJournal $journal
        $safe = [ordered]@{ Summary = $access.Summary; Grants = $access.Grants; Roles = $access.Roles }
        [IO.File]::WriteAllText($summaryPath, ($safe | ConvertTo-Json -Depth 35), [Text.UTF8Encoding]::new($false))
        Write-Host "Authenticated access: $($access.Summary.Passed) checks passed; private role/assignment evidence retained."
    } finally {
        $journalPath = Join-Path $private ('access-journal-' + [Guid]::NewGuid().ToString('N') + '.json')
        [IO.File]::WriteAllText($journalPath, (ConvertTo-Json -InputObject $journal.ToArray() -Depth 20), [Text.UTF8Encoding]::new($false))
        $access = $null
    }
    return
}
if (-not (Test-Path -LiteralPath $summaryPath)) { throw 'Completed authenticated access evidence is required.' }
. (Join-Path $PSScriptRoot 'LocalAcceptanceHttp.ps1')
$credentials = Get-Content -LiteralPath (Join-Path $private 'Test-Accounts.json') -Raw | ConvertFrom-Json
$admin = New-LocalAcceptanceSession -BaseUri https://localhost:7249 -Account ($credentials | Where-Object Email -eq 'admin@example.invalid') -SkipCertificateCheck
$hr = New-LocalAcceptanceSession -BaseUri https://localhost:7249 -Account ($credentials | Where-Object Email -eq 'hr@example.invalid') -SkipCertificateCheck
$evidence = Join-Path $private 'scheduling-evidence.json'
$scheduling = Join-Path $PSScriptRoot '../group-rosters/Invoke-IsolatedAcceptance.ps1'
$parameters = @{ Client = $admin; AdminClient = $admin; FixturePath = $manifestPath; EvidencePath = $evidence; Phase = $(if ($Phase -eq 'Scheduling') { 'Run' } elseif ($Phase -eq 'SchedulingResume') { 'Resume' } else { 'Replay' }) }
if ((Get-Command $scheduling).Parameters.ContainsKey('DeniedClient')) { $parameters.DeniedClient = $hr }
if ((Get-Command $scheduling).Parameters.ContainsKey('SelfClient')) { $parameters.SelfClient = $hr }
& $scheduling @parameters
