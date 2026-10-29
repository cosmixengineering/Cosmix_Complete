$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')
$root=Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\DemoAttendance20261028'
$fixture=Get-Content -LiteralPath (Join-Path $root 'fixture.json') -Raw|ConvertFrom-Json
$accounts=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\Test-Accounts.json') -Raw|ConvertFrom-Json
$hr=$null
try {
 $hr=New-LocalAcceptanceSession -BaseUri 'https://localhost:7240/' -Account ($accounts|Where-Object Email -eq 'hr@example.invalid') -SkipCertificateCheck
 $r=Invoke-LocalAcceptanceRequest -Client $hr -Method GET -Path ('/api/v1/attendance/employees/'+$fixture.EmployeeId+'/calendar?from=2026-10-01&to=2026-10-31')
 $r|ConvertTo-Json -Depth 25|Set-Content -LiteralPath (Join-Path $root 'full-october-calendar.json')
 $result=[pscustomobject]@{HttpStatus=$r.StatusCode;ReturnedDates=if($r.StatusCode -eq 200){@($r.Body).Count}else{0};ProblemTitle=if($r.StatusCode -ne 200){$r.Body.title}else{$null};All31DatesReturned=($r.StatusCode -eq 200 -and @($r.Body).Count -eq 31);Runtime='Local API https://localhost:7240; runtime identity recorded separately'}
 $result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'full-october-calendar-result.json')
 $result|ConvertTo-Json
}finally{
 if($hr){try{$logout=Invoke-LocalAcceptanceRequest -Client $hr -Method POST -Path '/api/v1/auth/web/logout';Write-Output ('Owned calendar-check session logout HTTP '+$logout.StatusCode)}finally{if($hr.WebSession -is [IDisposable]){$hr.WebSession.Dispose()}}}
}
