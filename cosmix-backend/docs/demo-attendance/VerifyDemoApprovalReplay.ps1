$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')
$root=Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\DemoAttendance20261028'
$fixture=Get-Content -LiteralPath (Join-Path $root 'fixture.json') -Raw|ConvertFrom-Json
$approved=Get-Content -LiteralPath (Join-Path $root 'approved-correction.json') -Raw|ConvertFrom-Json
$reviewed=Get-Content -LiteralPath (Join-Path $root 'hr-reviewed-evidence.json') -Raw|ConvertFrom-Json
$accounts=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\Test-Accounts.json') -Raw|ConvertFrom-Json
$admin=$null; $hr=$null
try {
$hr=New-LocalAcceptanceSession -BaseUri 'https://localhost:7240/' -Account ($accounts|Where-Object Email -eq 'hr@example.invalid') -SkipCertificateCheck
$body=@{expectedVersion=$reviewed.request.version;decision=1;reason='TEST ONLY: reviewed explicit synthetic duty evidence for the isolated demo. Approve manual duty for integration testing only; no genuine biometric/punch attendance, hiring, salary, payment or legal approval is asserted.'}
$r=Invoke-LocalAcceptanceRequest -Client $hr -Method POST -Path ('/api/v1/attendance/employees/'+$fixture.EmployeeId+'/corrections/'+$approved.request.id+'/decide') -Headers @{'Idempotency-Key'='DEMO-ATT-20261028-approve-v1'} -Body $body
$r|ConvertTo-Json -Depth 20|Set-Content -LiteralPath (Join-Path $root 'approval-replay.json')
if($r.StatusCode -ne 200 -or $r.Body.decisionId -ne $approved.decisionId -or $r.Body.appliedSessionRevisionId -ne $approved.appliedSessionRevisionId -or $r.Body.appliedSummaryRevisionId -ne $approved.appliedSummaryRevisionId){throw ('Approval replay mismatch HTTP '+$r.StatusCode)}
Write-Output 'Approval replay HTTP 200: identical decision/session/summary IDs.'

} finally {
 foreach($ownedClient in @($admin,$hr)){if($ownedClient -and $ownedClient.WebSession -is [IDisposable]){$ownedClient.WebSession.Dispose()}}
}
