$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')
$root=Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\DemoAttendance20261028'
$fixture=Get-Content -LiteralPath (Join-Path $root 'fixture.json') -Raw|ConvertFrom-Json
$accounts=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\Test-Accounts.json') -Raw|ConvertFrom-Json
$admin=$null; $hr=$null
try {
$hr=New-LocalAcceptanceSession -BaseUri 'https://localhost:7240/' -Account ($accounts|Where-Object Email -eq 'hr@example.invalid') -SkipCertificateCheck
$basic=Invoke-LocalAcceptanceRequest -Client $hr -Method GET -Path ('/api/v1/hr/employees/'+$fixture.EmployeeId+'/current/basic')
$directory=Invoke-LocalAcceptanceRequest -Client $hr -Method GET -Path '/api/v1/hr/employees?search=TEST%20ONLY%20DEMO%20ATTENDANCE%2020261028&page=1&pageSize=25'
$basic|ConvertTo-Json -Depth 15|Set-Content -LiteralPath (Join-Path $root 'frontend-current-basic.json')
$directory|ConvertTo-Json -Depth 15|Set-Content -LiteralPath (Join-Path $root 'frontend-employee-directory.json')
if($basic.StatusCode -ne 200 -or $basic.Body.employeeId -ne $fixture.EmployeeId){throw ('Current/basic prerequisite failed HTTP '+$basic.StatusCode)}
$matches=@($directory.Body.items|Where-Object id -eq $fixture.EmployeeId)
if($directory.StatusCode -ne 200 -or $matches.Count -ne 1 -or $matches[0].name -ne $fixture.Label){throw 'Directory prerequisite failed'}
$result=[pscustomobject]@{EmployeeNumber=$fixture.EmployeeNumber;EmployeeId=$fixture.EmployeeId;CurrentBasicHttp=200;DirectoryHttp=200;DirectoryMatches=1;DirectoryStatus=$matches[0].status;OnboardingStatus=$matches[0].onboardingStatus;FrontendApiPrerequisitesPassed=$true;BrowserUiAcceptancePerformed=$false;FullHiringAcceptancePerformed=$false}
$result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'frontend-prerequisites-result.json')
$result|ConvertTo-Json

} finally {
 foreach($ownedClient in @($admin,$hr)){if($ownedClient -and $ownedClient.WebSession -is [IDisposable]){$ownedClient.WebSession.Dispose()}}
}
